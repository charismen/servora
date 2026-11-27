using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Infrastructure.Activity;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;

namespace Servora.Api.Features.Payroll;

public static partial class BonusTaskDefinitionEndpoints
{
    public sealed record BonusTaskDefinitionDto(
        int Id,
        string TaskKey,
        string Label,
        string? Description,
        string? Category,
        decimal DefaultStandardRate,
        decimal DefaultGamingRate,
        decimal DefaultCustomerPrice,
        string DefaultWorkItemType,
        bool DefaultIsInternalOnly,
        string? LegacyKeywords,
        bool IsActive,
        int SortOrder,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    public sealed record UpsertBonusTaskDefinitionRequest(
        string TaskKey,
        string Label,
        string? Description = null,
        string? Category = null,
        decimal DefaultStandardRate = 0m,
        decimal DefaultGamingRate = 0m,
        decimal DefaultCustomerPrice = 0m,
        string? DefaultWorkItemType = null,
        bool DefaultIsInternalOnly = false,
        string? LegacyKeywords = null,
        bool IsActive = true,
        int SortOrder = 0);

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/bonus-tasks", ListBonusTasksAsync)
            .WithName("GetBonusTaskDefinitions")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsRead);

        app.MapPost("/bonus-tasks", CreateBonusTaskAsync)
            .WithName("CreateBonusTaskDefinition")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPut("/bonus-tasks/{id:int}", UpdateBonusTaskAsync)
            .WithName("UpdateBonusTaskDefinition")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapDelete("/bonus-tasks/{id:int}", DeleteBonusTaskAsync)
            .WithName("DeleteBonusTaskDefinition")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);
    }

    private static async Task<IResult> ListBonusTasksAsync(
        bool? includeInactive,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var query = context.BonusTaskDefinitions
            .AsNoTracking()
            .Where(item => item.ShopId == shopId);

        if (includeInactive != true)
        {
            query = query.Where(item => item.IsActive);
        }

        var items = await query
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Label)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        return Results.Ok(items.Select(ToDto).ToList());
    }

    private static async Task<IResult> CreateBonusTaskAsync(
        [FromBody] UpsertBonusTaskDefinitionRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        var validation = ValidateRequest(request, out var normalizedKey, out var workItemType);
        if (validation is not null) return validation;

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var duplicate = await context.BonusTaskDefinitions
            .AnyAsync(item => item.ShopId == shopId && item.TaskKey == normalizedKey, cancellationToken);
        if (duplicate)
        {
            return Results.BadRequest($"A bonus task with key '{normalizedKey}' already exists in this workspace.");
        }

        var entity = new BonusTaskDefinition
        {
            ShopId = shopId,
            TaskKey = normalizedKey,
            Label = request.Label.Trim(),
            Description = TrimOrNull(request.Description),
            Category = TrimOrNull(request.Category),
            DefaultStandardRate = Math.Round(request.DefaultStandardRate, 2, MidpointRounding.AwayFromZero),
            DefaultGamingRate = Math.Round(request.DefaultGamingRate, 2, MidpointRounding.AwayFromZero),
            DefaultCustomerPrice = request.DefaultIsInternalOnly ? 0m : Math.Round(request.DefaultCustomerPrice, 2, MidpointRounding.AwayFromZero),
            DefaultWorkItemType = workItemType,
            DefaultIsInternalOnly = request.DefaultIsInternalOnly,
            LegacyKeywords = TrimOrNull(request.LegacyKeywords),
            IsActive = request.IsActive,
            SortOrder = request.SortOrder
        };

        context.BonusTaskDefinitions.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        await activityLogger.LogAsync(
            "BonusTaskDefinition",
            entity.Id,
            "BonusTaskCreated",
            $"Created bonus task '{entity.Label}' ({entity.TaskKey}).",
            cancellationToken: cancellationToken);

        return Results.Created($"/api/v1/bonus-tasks/{entity.Id}", ToDto(entity));
    }

    private static async Task<IResult> UpdateBonusTaskAsync(
        int id,
        [FromBody] UpsertBonusTaskDefinitionRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        var validation = ValidateRequest(request, out var normalizedKey, out var workItemType);
        if (validation is not null) return validation;

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var entity = await context.BonusTaskDefinitions
            .FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId, cancellationToken);
        if (entity is null) return Results.NotFound();

        var duplicate = await context.BonusTaskDefinitions
            .AnyAsync(item => item.ShopId == shopId && item.Id != id && item.TaskKey == normalizedKey, cancellationToken);
        if (duplicate)
        {
            return Results.BadRequest($"Another bonus task already uses key '{normalizedKey}'.");
        }

        entity.TaskKey = normalizedKey;
        entity.Label = request.Label.Trim();
        entity.Description = TrimOrNull(request.Description);
        entity.Category = TrimOrNull(request.Category);
        entity.DefaultStandardRate = Math.Round(request.DefaultStandardRate, 2, MidpointRounding.AwayFromZero);
        entity.DefaultGamingRate = Math.Round(request.DefaultGamingRate, 2, MidpointRounding.AwayFromZero);
        entity.DefaultCustomerPrice = request.DefaultIsInternalOnly ? 0m : Math.Round(request.DefaultCustomerPrice, 2, MidpointRounding.AwayFromZero);
        entity.DefaultWorkItemType = workItemType;
        entity.DefaultIsInternalOnly = request.DefaultIsInternalOnly;
        entity.LegacyKeywords = TrimOrNull(request.LegacyKeywords);
        entity.IsActive = request.IsActive;
        entity.SortOrder = request.SortOrder;

        await context.SaveChangesAsync(cancellationToken);

        await activityLogger.LogAsync(
            "BonusTaskDefinition",
            entity.Id,
            "BonusTaskUpdated",
            $"Updated bonus task '{entity.Label}' ({entity.TaskKey}).",
            cancellationToken: cancellationToken);

        return Results.Ok(ToDto(entity));
    }

    private static async Task<IResult> DeleteBonusTaskAsync(
        int id,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var entity = await context.BonusTaskDefinitions
            .FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId, cancellationToken);
        if (entity is null) return Results.NotFound();

        var isReferenced = await context.ServiceTicketWorkItems
            .AnyAsync(item => item.ShopId == shopId && item.BonusTaskKey == entity.TaskKey, cancellationToken);

        if (isReferenced)
        {
            entity.IsActive = false;
            await context.SaveChangesAsync(cancellationToken);
            await activityLogger.LogAsync(
                "BonusTaskDefinition",
                entity.Id,
                "BonusTaskArchived",
                $"Archived bonus task '{entity.Label}' ({entity.TaskKey}) because it is referenced by existing ticket work items.",
                cancellationToken: cancellationToken);
            return Results.Ok(new { action = "archived", id = entity.Id });
        }

        context.BonusTaskDefinitions.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        await activityLogger.LogAsync(
            "BonusTaskDefinition",
            id,
            "BonusTaskDeleted",
            $"Deleted bonus task '{entity.Label}' ({entity.TaskKey}).",
            cancellationToken: cancellationToken);

        return Results.NoContent();
    }

    public static string NormalizeTaskKey(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var collapsed = KeySanitizeRegex().Replace(raw.Trim().ToUpperInvariant(), "_").Trim('_');
        return collapsed.Length > 60 ? collapsed[..60] : collapsed;
    }

    private static IResult? ValidateRequest(
        UpsertBonusTaskDefinitionRequest request,
        out string normalizedKey,
        out WorkItemType workItemType)
    {
        normalizedKey = NormalizeTaskKey(string.IsNullOrWhiteSpace(request.TaskKey) ? request.Label : request.TaskKey);
        workItemType = WorkItemType.Service;

        if (string.IsNullOrWhiteSpace(request.Label))
        {
            return Results.BadRequest("Task label is required.");
        }
        if (request.Label.Trim().Length > 150)
        {
            return Results.BadRequest("Task label must be 150 characters or fewer.");
        }
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            return Results.BadRequest("Task key is required and must contain alphanumeric characters.");
        }
        if (request.DefaultStandardRate < 0m || request.DefaultGamingRate < 0m || request.DefaultCustomerPrice < 0m)
        {
            return Results.BadRequest("Rates and customer price cannot be negative.");
        }
        if (!string.IsNullOrWhiteSpace(request.DefaultWorkItemType))
        {
            if (!Enum.TryParse<WorkItemType>(request.DefaultWorkItemType, true, out var parsed) || !Enum.IsDefined(parsed))
            {
                return Results.BadRequest("Invalid work item type.");
            }
            workItemType = parsed;
        }

        return null;
    }

    private static BonusTaskDefinitionDto ToDto(BonusTaskDefinition item) => new(
        item.Id,
        item.TaskKey,
        item.Label,
        item.Description,
        item.Category,
        item.DefaultStandardRate,
        item.DefaultGamingRate,
        item.DefaultCustomerPrice,
        item.DefaultWorkItemType.ToString(),
        item.DefaultIsInternalOnly,
        item.LegacyKeywords,
        item.IsActive,
        item.SortOrder,
        item.CreatedAt,
        item.UpdatedAt);

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("[^A-Z0-9]+")]
    private static partial Regex KeySanitizeRegex();
}
