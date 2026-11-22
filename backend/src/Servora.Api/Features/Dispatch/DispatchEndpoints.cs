using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Features.Maintenance;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Features.Settings;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Activity;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;

namespace Servora.Api.Features.Dispatch;

public static class DispatchEndpoints
{
    public sealed record ServiceResourceDto(
        int Id,
        string Name,
        string? Code,
        string ResourceType,
        string Color,
        string? Notes,
        int SortOrder,
        bool IsActive);

    public sealed record UpsertServiceResourceRequest(
        string Name,
        string? Code,
        string ResourceType,
        string? Color,
        string? Notes,
        int SortOrder = 0,
        bool IsActive = true);

    public sealed record DispatchTechnicianDto(
        int UserId,
        string FullName,
        string? Email);

    public sealed record DispatchTicketDto(
        int Id,
        string TicketNumber,
        string CustomerName,
        string CustomerPhone,
        string DeviceSnapshot,
        string Status,
        string StatusColor,
        string Priority,
        string ServiceLocationType,
        string? SiteAddressSnapshot,
        int? AssignedTechnicianUserId,
        string? AssignedTechnicianName,
        int? ServiceResourceId,
        string? ServiceResourceName,
        string? ServiceResourceColor,
        DateTimeOffset? ScheduledVisitStart,
        DateTimeOffset? ScheduledVisitEnd,
        string DispatchStatus,
        DateTimeOffset? VisitStartedAt,
        DateTimeOffset? VisitCompletedAt,
        string? DispatchNotes,
        DateTimeOffset? SlaDueAt,
        bool IsInternalWorkOrder,
        bool HasConflict,
        IReadOnlyList<string> ConflictReasons,
        DateTimeOffset CreatedAt);

    public sealed record DispatchDueMaintenanceDto(
        int Id,
        string Title,
        string AssetName,
        string? AssetIdentifier,
        string? CustomerName,
        string DefaultLocationType,
        DateTimeOffset? NextDueAt,
        int? AssignedTechnicianUserId,
        string? AssignedTechnicianName,
        bool IsOverdue);

    public sealed record DispatchSummaryDto(
        int ScheduledCount,
        int UnscheduledCount,
        int EnRouteOrOnSiteCount,
        int ConflictCount,
        int SlaAtRiskCount,
        int DuePmCount);

    public sealed record DispatchOperationalHoursDto(
        string OpenTime,
        string CloseTime,
        string TimeZone);

    public sealed record DispatchBoardResponse(
        DateTimeOffset From,
        DateTimeOffset To,
        DispatchOperationalHoursDto OperationalHours,
        IReadOnlyList<DispatchTechnicianDto> Technicians,
        IReadOnlyList<ServiceResourceDto> Resources,
        IReadOnlyList<DispatchTicketDto> ScheduledTickets,
        IReadOnlyList<DispatchTicketDto> UnscheduledTickets,
        IReadOnlyList<DispatchDueMaintenanceDto> DueMaintenanceSchedules,
        DispatchSummaryDto Summary);

    public sealed record ScheduleTicketRequest(
        DateTimeOffset ScheduledVisitStart,
        DateTimeOffset? ScheduledVisitEnd,
        int? AssignedTechnicianUserId,
        int? ServiceResourceId,
        string? ServiceLocationType,
        string? SiteAddressSnapshot,
        string? DispatchNotes,
        bool AllowConflict = false);

    public sealed record UpdateDispatchStatusRequest(
        string DispatchStatus,
        string? Note);

    public sealed record ScheduleMaintenanceRequest(
        DateTimeOffset ScheduledVisitStart,
        DateTimeOffset? ScheduledVisitEnd,
        int? AssignedTechnicianUserId,
        int? ServiceResourceId,
        string? ServiceLocationType,
        string? SiteAddressSnapshot,
        string? DispatchNotes,
        bool AllowConflict = false);

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        // Dispatch Board & Scheduling
        app.MapGet("/dispatch/board", GetBoardAsync)
            .WithName("GetDispatchBoard")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsRead);

        app.MapPut("/dispatch/tickets/{id:int}/schedule", ScheduleTicketAsync)
            .WithName("ScheduleDispatchTicket")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapDelete("/dispatch/tickets/{id:int}/schedule", UnscheduleTicketAsync)
            .WithName("UnscheduleDispatchTicket")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapPost("/dispatch/tickets/{id:int}/dispatch-status", UpdateDispatchStatusAsync)
            .WithName("UpdateTicketDispatchStatus")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapPost("/dispatch/maintenance/{scheduleId:int}/schedule", GenerateAndScheduleMaintenanceAsync)
            .WithName("GenerateAndScheduleMaintenanceTicket")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        // Service Resources (Bays / Lifts / Benches / Service Vans)
        app.MapGet("/service-resources", ListResourcesAsync)
            .WithName("GetServiceResources")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsRead);

        app.MapPost("/service-resources", CreateResourceAsync)
            .WithName("CreateServiceResource")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPut("/service-resources/{id:int}", UpdateResourceAsync)
            .WithName("UpdateServiceResource")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapDelete("/service-resources/{id:int}", DeleteResourceAsync)
            .WithName("DeleteServiceResource")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);
    }

    private static async Task<IResult> GetBoardAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? locationType,
        int? technicianId,
        int? resourceId,
        string? priority,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var rangeStart = from ?? new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var rangeEnd = to.HasValue && to.Value > rangeStart ? to.Value : rangeStart.AddDays(1);
        if ((rangeEnd - rangeStart).TotalDays > 35)
        {
            rangeEnd = rangeStart.AddDays(35);
        }

        var profile = await GetShopProfileSettings.ReadSettingsAsync(context, shopContext, cancellationToken);
        var operationalHours = new DispatchOperationalHoursDto(
            profile.OperationalOpenTime,
            profile.OperationalCloseTime,
            profile.OperationalTimeZone);

        var technicians = await context.ShopUsers.AsNoTracking()
            .Where(su => su.ShopId == shopId && su.Status == ShopUserStatus.Active && su.User.IsActive)
            .OrderBy(su => su.User.FirstName)
            .ThenBy(su => su.User.LastName)
            .Select(su => new DispatchTechnicianDto(
                su.UserId,
                (su.User.FirstName + " " + su.User.LastName).Trim(),
                su.User.Email))
            .ToListAsync(cancellationToken);

        var resources = await context.ServiceResources.AsNoTracking()
            .Where(r => r.ShopId == shopId && r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Name)
            .Select(r => new ServiceResourceDto(
                r.Id,
                r.Name,
                r.Code,
                r.ResourceType.ToString(),
                r.Color,
                r.Notes,
                r.SortOrder,
                r.IsActive))
            .ToListAsync(cancellationToken);

        ServiceLocationType? parsedLocation = null;
        if (!string.IsNullOrWhiteSpace(locationType)
            && Enum.TryParse<ServiceLocationType>(locationType, true, out var loc)
            && Enum.IsDefined(loc))
        {
            parsedLocation = loc;
        }

        ServiceTicketPriority? parsedPriority = null;
        if (!string.IsNullOrWhiteSpace(priority)
            && Enum.TryParse<ServiceTicketPriority>(priority, true, out var prio)
            && Enum.IsDefined(prio))
        {
            parsedPriority = prio;
        }

        // Load all active scheduled tickets overlapping range (for accurate conflict calculation even when filtered)
        var rawScheduled = await context.ServiceTickets.AsNoTracking()
            .Where(t => t.ShopId == shopId
                && t.DeletedAt == null
                && !t.Status.IsCancelled
                && t.ScheduledVisitStart != null
                && t.ScheduledVisitStart < rangeEnd
                && (t.ScheduledVisitEnd == null || t.ScheduledVisitEnd > rangeStart))
            .OrderBy(t => t.ScheduledVisitStart)
            .Select(t => new
            {
                t.Id,
                t.TicketNumber,
                t.CustomerNameSnapshot,
                t.CustomerPhoneSnapshot,
                t.DeviceSnapshot,
                Status = t.Status.Name,
                StatusColor = t.Status.Color,
                IsFinal = t.Status.IsFinal,
                t.Priority,
                t.ServiceLocationType,
                t.SiteAddressSnapshot,
                t.AssignedTechnicianUserId,
                AssignedTechnicianName = t.AssignedTechnicianUser == null
                    ? null
                    : (t.AssignedTechnicianUser.FirstName + " " + t.AssignedTechnicianUser.LastName).Trim(),
                t.ServiceResourceId,
                ServiceResourceName = t.ServiceResource == null ? null : t.ServiceResource.Name,
                ServiceResourceColor = t.ServiceResource == null ? null : t.ServiceResource.Color,
                t.ScheduledVisitStart,
                t.ScheduledVisitEnd,
                t.DispatchStatus,
                t.VisitStartedAt,
                t.VisitCompletedAt,
                t.DispatchNotes,
                t.SlaDueAt,
                t.IsInternalWorkOrder,
                t.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var activeSlotsForConflicts = rawScheduled
            .Where(t => !t.IsFinal)
            .Select(t => new ScheduledTicketSlot(
                t.Id,
                t.TicketNumber,
                t.CustomerNameSnapshot,
                t.DeviceSnapshot,
                t.AssignedTechnicianUserId,
                t.ServiceResourceId,
                t.ScheduledVisitStart!.Value,
                t.ScheduledVisitEnd))
            .ToList();

        var scheduledDtos = new List<DispatchTicketDto>(rawScheduled.Count);
        foreach (var item in rawScheduled)
        {
            if (parsedLocation.HasValue && item.ServiceLocationType != parsedLocation.Value) continue;
            if (technicianId.HasValue && item.AssignedTechnicianUserId != technicianId.Value) continue;
            if (resourceId.HasValue && item.ServiceResourceId != resourceId.Value) continue;
            if (parsedPriority.HasValue && item.Priority != parsedPriority.Value) continue;

            var normalizedEnd = DispatchConflictCalculator.NormalizeEnd(item.ScheduledVisitStart!.Value, item.ScheduledVisitEnd);
            var conflicts = item.IsFinal
                ? []
                : DispatchConflictCalculator.EvaluateConflicts(
                    new ScheduleCandidate(
                        item.Id,
                        item.AssignedTechnicianUserId,
                        item.ServiceResourceId,
                        item.ScheduledVisitStart.Value,
                        normalizedEnd),
                    activeSlotsForConflicts);

            var conflictReasons = conflicts
                .Select(c => $"{c.TicketNumber} ({c.ConflictType})")
                .ToList();

            scheduledDtos.Add(new DispatchTicketDto(
                item.Id,
                item.TicketNumber,
                item.CustomerNameSnapshot,
                item.CustomerPhoneSnapshot,
                item.DeviceSnapshot,
                item.Status,
                item.StatusColor,
                item.Priority.ToString(),
                item.ServiceLocationType.ToString(),
                item.SiteAddressSnapshot,
                item.AssignedTechnicianUserId,
                string.IsNullOrWhiteSpace(item.AssignedTechnicianName) ? null : item.AssignedTechnicianName,
                item.ServiceResourceId,
                item.ServiceResourceName,
                item.ServiceResourceColor,
                item.ScheduledVisitStart,
                normalizedEnd,
                item.DispatchStatus.ToString(),
                item.VisitStartedAt,
                item.VisitCompletedAt,
                item.DispatchNotes,
                item.SlaDueAt,
                item.IsInternalWorkOrder,
                conflicts.Count > 0,
                conflictReasons,
                item.CreatedAt));
        }

        // Unscheduled open tickets backlog
        var unscheduledQuery = context.ServiceTickets.AsNoTracking()
            .Where(t => t.ShopId == shopId
                && t.DeletedAt == null
                && !t.Status.IsFinal
                && !t.Status.IsCancelled
                && t.ScheduledVisitStart == null);

        if (parsedLocation.HasValue)
        {
            unscheduledQuery = unscheduledQuery.Where(t => t.ServiceLocationType == parsedLocation.Value);
        }
        if (parsedPriority.HasValue)
        {
            unscheduledQuery = unscheduledQuery.Where(t => t.Priority == parsedPriority.Value);
        }

        var unscheduledRaw = await unscheduledQuery
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.SlaDueAt == null)
            .ThenBy(t => t.SlaDueAt)
            .ThenByDescending(t => t.CreatedAt)
            .Take(80)
            .Select(t => new
            {
                t.Id,
                t.TicketNumber,
                t.CustomerNameSnapshot,
                t.CustomerPhoneSnapshot,
                t.DeviceSnapshot,
                Status = t.Status.Name,
                StatusColor = t.Status.Color,
                t.Priority,
                t.ServiceLocationType,
                t.SiteAddressSnapshot,
                t.AssignedTechnicianUserId,
                AssignedTechnicianName = t.AssignedTechnicianUser == null
                    ? null
                    : (t.AssignedTechnicianUser.FirstName + " " + t.AssignedTechnicianUser.LastName).Trim(),
                t.DispatchStatus,
                t.DispatchNotes,
                t.SlaDueAt,
                t.IsInternalWorkOrder,
                t.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var unscheduledDtos = unscheduledRaw
            .Select(item => new DispatchTicketDto(
                item.Id,
                item.TicketNumber,
                item.CustomerNameSnapshot,
                item.CustomerPhoneSnapshot,
                item.DeviceSnapshot,
                item.Status,
                item.StatusColor,
                item.Priority.ToString(),
                item.ServiceLocationType.ToString(),
                item.SiteAddressSnapshot,
                item.AssignedTechnicianUserId,
                string.IsNullOrWhiteSpace(item.AssignedTechnicianName) ? null : item.AssignedTechnicianName,
                null,
                null,
                null,
                null,
                null,
                item.DispatchStatus.ToString(),
                null,
                null,
                item.DispatchNotes,
                item.SlaDueAt,
                item.IsInternalWorkOrder,
                false,
                [],
                item.CreatedAt))
            .ToList();

        // Due Preventive Maintenance schedules within range (or overdue)
        var pmCutoff = rangeEnd.AddDays(7);
        var rawDuePm = await context.MaintenanceSchedules.AsNoTracking()
            .Where(s => s.ShopId == shopId
                && s.Status == MaintenanceScheduleStatus.Active
                && s.NextDueAt != null
                && s.NextDueAt <= pmCutoff)
            .OrderBy(s => s.NextDueAt)
            .Take(30)
            .Select(s => new
            {
                s.Id,
                s.Title,
                AssetName = (s.ServiceAsset.Brand + " " + s.ServiceAsset.Model).Trim(),
                s.ServiceAsset.SerialNumber,
                CustomerName = s.ServiceAsset.Customer == null ? null : s.ServiceAsset.Customer.FullName,
                s.DefaultLocationType,
                s.NextDueAt,
                s.AssignedTechnicianUserId,
                AssignedTechnicianName = s.AssignedTechnicianUser == null
                    ? null
                    : (s.AssignedTechnicianUser.FirstName + " " + s.AssignedTechnicianUser.LastName).Trim()
            })
            .ToListAsync(cancellationToken);

        var duePmDtos = rawDuePm
            .Select(s => new DispatchDueMaintenanceDto(
                s.Id,
                s.Title,
                s.AssetName,
                s.SerialNumber,
                s.CustomerName,
                s.DefaultLocationType.ToString(),
                s.NextDueAt,
                s.AssignedTechnicianUserId,
                string.IsNullOrWhiteSpace(s.AssignedTechnicianName) ? null : s.AssignedTechnicianName,
                s.NextDueAt.HasValue && s.NextDueAt.Value <= now))
            .ToList();

        var slaThreshold = now.AddHours(24);
        var summary = new DispatchSummaryDto(
            ScheduledCount: scheduledDtos.Count,
            UnscheduledCount: unscheduledDtos.Count,
            EnRouteOrOnSiteCount: scheduledDtos.Count(t =>
                t.DispatchStatus is nameof(DispatchStatus.EnRoute) or nameof(DispatchStatus.ArrivedOnSite)),
            ConflictCount: scheduledDtos.Count(t => t.HasConflict),
            SlaAtRiskCount: scheduledDtos.Concat(unscheduledDtos).Count(t =>
                t.SlaDueAt.HasValue && t.SlaDueAt.Value <= slaThreshold && t.DispatchStatus != nameof(DispatchStatus.VisitCompleted)),
            DuePmCount: duePmDtos.Count);

        return Results.Ok(new DispatchBoardResponse(
            rangeStart,
            rangeEnd,
            operationalHours,
            technicians,
            resources,
            scheduledDtos,
            unscheduledDtos,
            duePmDtos,
            summary));
    }

    private static async Task<IResult> ScheduleTicketAsync(
        int id,
        [FromBody] ScheduleTicketRequest request,
        ClaimsPrincipal principal,
        IShopContext shopContext,
        DispatchSchedulingService schedulingService,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var result = await schedulingService.ScheduleTicketAsync(
            shopId,
            id,
            new ScheduleTicketCommand(
                request.ScheduledVisitStart,
                request.ScheduledVisitEnd,
                request.AssignedTechnicianUserId,
                request.ServiceResourceId,
                request.ServiceLocationType,
                request.SiteAddressSnapshot,
                request.DispatchNotes,
                request.AllowConflict,
                TryGetUserId(principal)),
            cancellationToken);

        if (!result.Succeeded)
        {
            if (result.StatusCode == StatusCodes.Status409Conflict)
            {
                return Results.Conflict(new
                {
                    message = result.ErrorMessage,
                    conflicts = result.Conflicts ?? []
                });
            }

            return Results.Problem(detail: result.ErrorMessage, statusCode: result.StatusCode);
        }

        var t = result.Value!;
        return Results.Ok(new
        {
            t.Id,
            t.TicketNumber,
            t.AssignedTechnicianUserId,
            t.ServiceResourceId,
            t.ScheduledVisitStart,
            t.ScheduledVisitEnd,
            DispatchStatus = t.DispatchStatus.ToString(),
            ServiceLocationType = t.ServiceLocationType.ToString(),
            t.SiteAddressSnapshot,
            t.DispatchNotes
        });
    }

    private static async Task<IResult> UnscheduleTicketAsync(
        int id,
        ClaimsPrincipal principal,
        IShopContext shopContext,
        DispatchSchedulingService schedulingService,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var result = await schedulingService.UnscheduleTicketAsync(shopId, id, TryGetUserId(principal), cancellationToken);
        return result.Succeeded
            ? Results.NoContent()
            : Results.Problem(detail: result.ErrorMessage, statusCode: result.StatusCode);
    }

    private static async Task<IResult> UpdateDispatchStatusAsync(
        int id,
        [FromBody] UpdateDispatchStatusRequest request,
        ClaimsPrincipal principal,
        IShopContext shopContext,
        DispatchSchedulingService schedulingService,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var result = await schedulingService.UpdateDispatchStatusAsync(
            shopId,
            id,
            request.DispatchStatus,
            request.Note,
            TryGetUserId(principal),
            cancellationToken);

        if (!result.Succeeded)
        {
            return Results.Problem(detail: result.ErrorMessage, statusCode: result.StatusCode);
        }

        var t = result.Value!;
        return Results.Ok(new
        {
            t.Id,
            DispatchStatus = t.DispatchStatus.ToString(),
            t.VisitStartedAt,
            t.VisitCompletedAt,
            t.DispatchNotes
        });
    }

    private static async Task<IResult> GenerateAndScheduleMaintenanceAsync(
        int scheduleId,
        [FromBody] ScheduleMaintenanceRequest request,
        ClaimsPrincipal principal,
        AppDbContext context,
        IShopContext shopContext,
        PreventiveMaintenanceJob pmJob,
        DispatchSchedulingService schedulingService,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var schedule = await context.MaintenanceSchedules
            .Include(s => s.Shop)
            .Include(s => s.ServiceAsset)
                .ThenInclude(a => a.Customer)
            .Include(s => s.ServiceAsset)
                .ThenInclude(a => a.SiteLocation)
            .Include(s => s.ServiceTemplate)
                .ThenInclude(t => t!.ChecklistItems)
            .FirstOrDefaultAsync(s => s.Id == scheduleId && s.ShopId == shopId, cancellationToken);

        if (schedule is null)
        {
            return Results.NotFound("Maintenance schedule not found.");
        }

        var end = request.ScheduledVisitEnd.HasValue
            ? request.ScheduledVisitEnd.Value
            : request.ScheduledVisitStart.Add(DispatchConflictCalculator.DefaultVisitDuration);
        if (end <= request.ScheduledVisitStart)
        {
            return Results.BadRequest("Scheduled end time must be after start time.");
        }

        // Pre-check conflicts BEFORE creating a ticket so we never orphan a generated ticket on 409!
        var techId = request.AssignedTechnicianUserId ?? schedule.AssignedTechnicianUserId;
        var conflicts = await schedulingService.FindConflictsAsync(
            shopId,
            new ScheduleCandidate(null, techId, request.ServiceResourceId, request.ScheduledVisitStart, end),
            cancellationToken);

        if (conflicts.Count > 0 && !request.AllowConflict)
        {
            return Results.Conflict(new
            {
                message = $"Scheduling conflict detected with {conflicts.Count} overlapping ticket(s). Confirm override to proceed.",
                conflicts
            });
        }

        var now = DateTimeOffset.UtcNow;
        var ticket = await pmJob.CreateTicketFromScheduleAsync(schedule, now, cancellationToken);
        if (ticket is null)
        {
            return Results.BadRequest("Unable to generate ticket from maintenance schedule (ensure workspace statuses exist).");
        }

        schedule.LastGeneratedTicketId = ticket.Id;
        PreventiveMaintenanceJob.AdvanceNextDue(schedule, now);
        await context.SaveChangesAsync(cancellationToken);

        var scheduled = await schedulingService.ScheduleTicketAsync(
            shopId,
            ticket.Id,
            new ScheduleTicketCommand(
                request.ScheduledVisitStart,
                end,
                techId,
                request.ServiceResourceId,
                request.ServiceLocationType ?? schedule.DefaultLocationType.ToString(),
                request.SiteAddressSnapshot,
                request.DispatchNotes,
                AllowConflict: true,
                ActorUserId: TryGetUserId(principal)),
            cancellationToken);

        if (!scheduled.Succeeded)
        {
            return Results.Problem(detail: scheduled.ErrorMessage, statusCode: scheduled.StatusCode);
        }

        return Results.Created($"/api/v1/service-tickets/{ticket.Id}", new
        {
            ticketId = ticket.Id,
            ticketNumber = ticket.TicketNumber,
            scheduledVisitStart = ticket.ScheduledVisitStart,
            scheduledVisitEnd = ticket.ScheduledVisitEnd,
            dispatchStatus = ticket.DispatchStatus.ToString()
        });
    }

    private static async Task<IResult> ListResourcesAsync(
        bool? includeInactive,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var query = context.ServiceResources.AsNoTracking().Where(r => r.ShopId == shopId);
        if (includeInactive != true)
        {
            query = query.Where(r => r.IsActive);
        }

        var items = await query
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Name)
            .Select(r => new ServiceResourceDto(
                r.Id,
                r.Name,
                r.Code,
                r.ResourceType.ToString(),
                r.Color,
                r.Notes,
                r.SortOrder,
                r.IsActive))
            .ToListAsync(cancellationToken);

        return Results.Ok(items);
    }

    private static async Task<IResult> CreateResourceAsync(
        [FromBody] UpsertServiceResourceRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
        {
            return Results.BadRequest("Resource name is required and must be 100 characters or fewer.");
        }

        if (!Enum.TryParse<ServiceResourceType>(request.ResourceType, true, out var resourceType) || !Enum.IsDefined(resourceType))
        {
            return Results.BadRequest("Invalid service resource type.");
        }

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var duplicate = await context.ServiceResources.AsNoTracking()
            .AnyAsync(r => r.ShopId == shopId && r.Name == name, cancellationToken);
        if (duplicate)
        {
            return Results.Conflict("A service resource with that name already exists in this workspace.");
        }

        var entity = new ServiceResource
        {
            ShopId = shopId,
            Name = name,
            Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim()[..Math.Min(30, request.Code.Trim().Length)],
            ResourceType = resourceType,
            Color = string.IsNullOrWhiteSpace(request.Color) ? "#b45039" : request.Color.Trim()[..Math.Min(20, request.Color.Trim().Length)],
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()[..Math.Min(500, request.Notes.Trim().Length)],
            SortOrder = request.SortOrder,
            IsActive = request.IsActive
        };

        context.ServiceResources.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        await activityLogger.LogAsync("ServiceResource", entity.Id, "ServiceResourceCreated", $"Service resource created: {entity.Name}", cancellationToken: cancellationToken);

        return Results.Created($"/api/v1/service-resources/{entity.Id}", ToDto(entity));
    }

    private static async Task<IResult> UpdateResourceAsync(
        int id,
        [FromBody] UpsertServiceResourceRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
        {
            return Results.BadRequest("Resource name is required and must be 100 characters or fewer.");
        }

        if (!Enum.TryParse<ServiceResourceType>(request.ResourceType, true, out var resourceType) || !Enum.IsDefined(resourceType))
        {
            return Results.BadRequest("Invalid service resource type.");
        }

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var entity = await context.ServiceResources
            .FirstOrDefaultAsync(r => r.Id == id && r.ShopId == shopId, cancellationToken);
        if (entity is null) return Results.NotFound();

        var duplicate = await context.ServiceResources.AsNoTracking()
            .AnyAsync(r => r.ShopId == shopId && r.Id != id && r.Name == name, cancellationToken);
        if (duplicate)
        {
            return Results.Conflict("A service resource with that name already exists in this workspace.");
        }

        entity.Name = name;
        entity.Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim()[..Math.Min(30, request.Code.Trim().Length)];
        entity.ResourceType = resourceType;
        entity.Color = string.IsNullOrWhiteSpace(request.Color) ? "#b45039" : request.Color.Trim()[..Math.Min(20, request.Color.Trim().Length)];
        entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()[..Math.Min(500, request.Notes.Trim().Length)];
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;

        await context.SaveChangesAsync(cancellationToken);
        await activityLogger.LogAsync("ServiceResource", entity.Id, "ServiceResourceUpdated", $"Service resource updated: {entity.Name}", cancellationToken: cancellationToken);

        return Results.Ok(ToDto(entity));
    }

    private static async Task<IResult> DeleteResourceAsync(
        int id,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var entity = await context.ServiceResources
            .FirstOrDefaultAsync(r => r.Id == id && r.ShopId == shopId, cancellationToken);
        if (entity is null) return Results.NotFound();

        var isReferenced = await context.ServiceTickets.AsNoTracking()
            .AnyAsync(t => t.ShopId == shopId && t.ServiceResourceId == id, cancellationToken);

        if (isReferenced)
        {
            entity.IsActive = false;
        }
        else
        {
            context.ServiceResources.Remove(entity);
        }

        await context.SaveChangesAsync(cancellationToken);
        await activityLogger.LogAsync("ServiceResource", id, "ServiceResourceDeleted", $"Service resource {(isReferenced ? "archived" : "deleted")}: {entity.Name}", cancellationToken: cancellationToken);
        return Results.NoContent();
    }

    private static ServiceResourceDto ToDto(ServiceResource entity)
        => new(entity.Id, entity.Name, entity.Code, entity.ResourceType.ToString(), entity.Color, entity.Notes, entity.SortOrder, entity.IsActive);

    private static int? TryGetUserId(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : null;
    }
}
