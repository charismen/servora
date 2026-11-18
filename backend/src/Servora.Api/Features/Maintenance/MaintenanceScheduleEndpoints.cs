using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Infrastructure.Activity;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;

namespace Servora.Api.Features.Maintenance;

public static class MaintenanceScheduleEndpoints
{
    public sealed record MaintenanceScheduleDto(
        int Id,
        int ServiceAssetId,
        string AssetDisplayName,
        string? AssetIdentifier,
        decimal? CurrentMeterReading,
        string? MeterUnit,
        string CustomerName,
        int? ServiceTemplateId,
        string? ServiceTemplateName,
        string Title,
        string? Description,
        string TriggerType,
        int? IntervalValue,
        string? IntervalUnit,
        decimal? MeterInterval,
        decimal? LastCompletedMeter,
        decimal? NextDueMeter,
        DateTimeOffset? LastCompletedAt,
        DateTimeOffset? NextDueAt,
        int LeadTimeDays,
        bool AutoCreateTicket,
        int? AssignedTechnicianUserId,
        string? AssignedTechnicianName,
        string DefaultLocationType,
        int? LastGeneratedTicketId,
        string? LastGeneratedTicketNumber,
        string Status,
        bool IsDue,
        bool IsUpcoming,
        DateTimeOffset CreatedAt);

    public sealed record UpsertMaintenanceScheduleRequest(
        int ServiceAssetId,
        int? ServiceTemplateId,
        string Title,
        string? Description,
        MaintenanceTriggerType TriggerType,
        int? IntervalValue,
        MaintenanceIntervalUnit? IntervalUnit,
        decimal? MeterInterval,
        DateTimeOffset? NextDueAt,
        decimal? NextDueMeter,
        int LeadTimeDays = 7,
        bool AutoCreateTicket = false,
        int? AssignedTechnicianUserId = null,
        ServiceLocationType DefaultLocationType = ServiceLocationType.InShop,
        MaintenanceScheduleStatus Status = MaintenanceScheduleStatus.Active);

    public sealed record CompleteCycleRequest(decimal? MeterReading);

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/maintenance-schedules", ListSchedulesAsync)
            .WithName("GetMaintenanceSchedules")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.MaintenanceRead);

        app.MapPost("/maintenance-schedules", CreateScheduleAsync)
            .WithName("CreateMaintenanceSchedule")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.MaintenanceWrite);

        app.MapPut("/maintenance-schedules/{id:int}", UpdateScheduleAsync)
            .WithName("UpdateMaintenanceSchedule")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.MaintenanceWrite);

        app.MapDelete("/maintenance-schedules/{id:int}", DeleteScheduleAsync)
            .WithName("DeleteMaintenanceSchedule")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.MaintenanceDelete);

        app.MapPost("/maintenance-schedules/{id:int}/generate-ticket", GenerateTicketNowAsync)
            .WithName("GenerateMaintenanceTicket")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.MaintenanceWrite);

        app.MapPost("/maintenance-schedules/{id:int}/complete", CompleteCycleAsync)
            .WithName("CompleteMaintenanceCycle")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.MaintenanceWrite);

        app.MapPost("/maintenance-schedules/run-due-check", RunDueCheckAsync)
            .WithName("RunMaintenanceDueCheck")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.MaintenanceWrite);
    }

    private static async Task<IResult> ListSchedulesAsync(
        string? status,
        bool dueOnly,
        int? serviceAssetId,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var query = context.MaintenanceSchedules
            .AsNoTracking()
            .Include(s => s.ServiceAsset)
                .ThenInclude(a => a.Customer)
            .Include(s => s.ServiceTemplate)
            .Include(s => s.AssignedTechnicianUser)
            .Include(s => s.LastGeneratedTicket)
            .Where(s => s.ShopId == workspaceId);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<MaintenanceScheduleStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(s => s.Status == parsedStatus);
        }

        if (serviceAssetId.HasValue)
        {
            query = query.Where(s => s.ServiceAssetId == serviceAssetId.Value);
        }

        var rows = await query
            .OrderBy(s => s.NextDueAt ?? DateTimeOffset.MaxValue)
            .ThenByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        var dtos = rows.Select(s =>
        {
            var isCalDue = s.TriggerType is MaintenanceTriggerType.Calendar or MaintenanceTriggerType.Both
                && s.NextDueAt.HasValue && s.NextDueAt.Value <= now;
            var isMeterDue = s.TriggerType is MaintenanceTriggerType.Meter or MaintenanceTriggerType.Both
                && s.NextDueMeter.HasValue && s.ServiceAsset.MeterReading.HasValue
                && s.ServiceAsset.MeterReading.Value >= s.NextDueMeter.Value;
            var isDue = s.Status == MaintenanceScheduleStatus.Active && (isCalDue || isMeterDue);
            var isUpcoming = s.Status == MaintenanceScheduleStatus.Active && !isDue && (
                (s.NextDueAt.HasValue && s.NextDueAt.Value <= now.AddDays(Math.Max(1, s.LeadTimeDays))) ||
                (s.NextDueMeter.HasValue && s.ServiceAsset.MeterReading.HasValue && s.MeterInterval.HasValue
                    && s.ServiceAsset.MeterReading.Value >= s.NextDueMeter.Value - (s.MeterInterval.Value * 0.1m)));

            return new MaintenanceScheduleDto(
                s.Id,
                s.ServiceAssetId,
                $"{s.ServiceAsset.Brand} {s.ServiceAsset.Model}".Trim(),
                s.ServiceAsset.SerialNumber,
                s.ServiceAsset.MeterReading,
                s.ServiceAsset.MeterUnit,
                s.ServiceAsset.Customer.FullName,
                s.ServiceTemplateId,
                s.ServiceTemplate?.Name,
                s.Title,
                s.Description,
                s.TriggerType.ToString(),
                s.IntervalValue,
                s.IntervalUnit?.ToString(),
                s.MeterInterval,
                s.LastCompletedMeter,
                s.NextDueMeter,
                s.LastCompletedAt,
                s.NextDueAt,
                s.LeadTimeDays,
                s.AutoCreateTicket,
                s.AssignedTechnicianUserId,
                s.AssignedTechnicianUser is null ? null : $"{s.AssignedTechnicianUser.FirstName} {s.AssignedTechnicianUser.LastName}".Trim(),
                s.DefaultLocationType.ToString(),
                s.LastGeneratedTicketId,
                s.LastGeneratedTicket?.TicketNumber,
                s.Status.ToString(),
                isDue,
                isUpcoming,
                s.CreatedAt);
        });

        if (dueOnly)
        {
            dtos = dtos.Where(d => d.IsDue || d.IsUpcoming);
        }

        return Results.Ok(dtos.ToList());
    }

    private static async Task<IResult> CreateScheduleAsync(
        [FromBody] UpsertMaintenanceScheduleRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["title"] = ["Schedule title is required."]
            });
        }

        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var asset = await context.Devices
            .FirstOrDefaultAsync(d => d.Id == request.ServiceAssetId && d.ShopId == workspaceId && d.DeletedAt == null, cancellationToken);
        if (asset is null) return Results.NotFound();

        var nextDueAt = request.NextDueAt;
        if (!nextDueAt.HasValue && request.TriggerType is MaintenanceTriggerType.Calendar or MaintenanceTriggerType.Both && request.IntervalValue is > 0)
        {
            var now = DateTimeOffset.UtcNow;
            nextDueAt = (request.IntervalUnit ?? MaintenanceIntervalUnit.Months) switch
            {
                MaintenanceIntervalUnit.Days => now.AddDays(request.IntervalValue.Value),
                MaintenanceIntervalUnit.Weeks => now.AddDays(request.IntervalValue.Value * 7),
                _ => now.AddMonths(request.IntervalValue.Value)
            };
        }

        var nextDueMeter = request.NextDueMeter;
        if (!nextDueMeter.HasValue && request.TriggerType is MaintenanceTriggerType.Meter or MaintenanceTriggerType.Both && request.MeterInterval is > 0)
        {
            nextDueMeter = (asset.MeterReading ?? 0m) + request.MeterInterval.Value;
        }

        var entity = new MaintenanceSchedule
        {
            ShopId = workspaceId,
            ServiceAssetId = asset.Id,
            ServiceTemplateId = request.ServiceTemplateId,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            TriggerType = request.TriggerType,
            IntervalValue = request.IntervalValue,
            IntervalUnit = request.IntervalUnit,
            MeterInterval = request.MeterInterval,
            LastCompletedMeter = asset.MeterReading,
            NextDueMeter = nextDueMeter,
            NextDueAt = nextDueAt,
            LeadTimeDays = Math.Max(1, request.LeadTimeDays),
            AutoCreateTicket = request.AutoCreateTicket,
            AssignedTechnicianUserId = request.AssignedTechnicianUserId,
            DefaultLocationType = request.DefaultLocationType,
            Status = request.Status
        };

        context.MaintenanceSchedules.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        await activityLogger.LogAsync(
            "MaintenanceSchedule",
            entity.Id,
            "ScheduleCreated",
            $"Created preventive maintenance schedule '{entity.Title}'.",
            cancellationToken: cancellationToken);

        return Results.Created($"/api/v1/maintenance-schedules/{entity.Id}", new { entity.Id });
    }

    private static async Task<IResult> UpdateScheduleAsync(
        int id,
        [FromBody] UpsertMaintenanceScheduleRequest request,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var entity = await context.MaintenanceSchedules
            .FirstOrDefaultAsync(s => s.Id == id && s.ShopId == workspaceId, cancellationToken);

        if (entity is null) return Results.NotFound();

        entity.ServiceAssetId = request.ServiceAssetId;
        entity.ServiceTemplateId = request.ServiceTemplateId;
        entity.Title = request.Title.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.TriggerType = request.TriggerType;
        entity.IntervalValue = request.IntervalValue;
        entity.IntervalUnit = request.IntervalUnit;
        entity.MeterInterval = request.MeterInterval;
        entity.NextDueAt = request.NextDueAt;
        entity.NextDueMeter = request.NextDueMeter;
        entity.LeadTimeDays = Math.Max(1, request.LeadTimeDays);
        entity.AutoCreateTicket = request.AutoCreateTicket;
        entity.AssignedTechnicianUserId = request.AssignedTechnicianUserId;
        entity.DefaultLocationType = request.DefaultLocationType;
        entity.Status = request.Status;

        await context.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteScheduleAsync(
        int id,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var entity = await context.MaintenanceSchedules
            .FirstOrDefaultAsync(s => s.Id == id && s.ShopId == workspaceId, cancellationToken);

        if (entity is null) return Results.NotFound();

        context.MaintenanceSchedules.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GenerateTicketNowAsync(
        int id,
        AppDbContext context,
        IShopContext shopContext,
        PreventiveMaintenanceJob pmJob,
        CancellationToken cancellationToken)
    {
        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var schedule = await context.MaintenanceSchedules
            .Include(s => s.Shop)
            .Include(s => s.ServiceAsset).ThenInclude(a => a.Customer)
            .Include(s => s.ServiceAsset).ThenInclude(a => a.SiteLocation)
            .Include(s => s.ServiceTemplate).ThenInclude(t => t!.ChecklistItems)
            .FirstOrDefaultAsync(s => s.Id == id && s.ShopId == workspaceId, cancellationToken);

        if (schedule is null) return Results.NotFound();

        var now = DateTimeOffset.UtcNow;
        var ticket = await pmJob.CreateTicketFromScheduleAsync(schedule, now, cancellationToken);
        if (ticket is null)
        {
            return Results.Problem("No initial service status configured for this workspace.", statusCode: StatusCodes.Status400BadRequest);
        }

        schedule.LastGeneratedTicketId = ticket.Id;
        PreventiveMaintenanceJob.AdvanceNextDue(schedule, now);
        await context.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/v1/service-tickets/{ticket.Id}", new { ticket.Id, ticket.TicketNumber });
    }

    private static async Task<IResult> CompleteCycleAsync(
        int id,
        [FromBody] CompleteCycleRequest? request,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var schedule = await context.MaintenanceSchedules
            .Include(s => s.ServiceAsset)
            .FirstOrDefaultAsync(s => s.Id == id && s.ShopId == workspaceId, cancellationToken);

        if (schedule is null) return Results.NotFound();

        if (request?.MeterReading.HasValue == true)
        {
            schedule.ServiceAsset.MeterReading = request.MeterReading.Value;
        }

        PreventiveMaintenanceJob.AdvanceNextDue(schedule, DateTimeOffset.UtcNow, request?.MeterReading);
        await context.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { schedule.Id, schedule.NextDueAt, schedule.NextDueMeter });
    }

    private static async Task<IResult> RunDueCheckAsync(
        IShopContext shopContext,
        PreventiveMaintenanceJob pmJob,
        CancellationToken cancellationToken)
    {
        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var result = await pmJob.ProcessDueSchedulesAsync(cancellationToken, workspaceId);
        return Results.Ok(result);
    }
}
