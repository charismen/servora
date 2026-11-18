using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Activity;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;

namespace Servora.Api.Features.Blueprints;

public static class BlueprintEndpoints
{
    public sealed record CustomFieldDefinitionDto(
        int Id,
        string Scope,
        string FieldKey,
        string LabelEn,
        string LabelId,
        string FieldType,
        IReadOnlyList<string> Options,
        bool IsRequired,
        bool IsSearchable,
        bool ShowOnIntake,
        bool ShowOnDetail,
        bool ShowOnPrint,
        bool ShowOnPublicTracking,
        int SortOrder,
        bool IsActive);

    public sealed record UpsertCustomFieldRequest(
        CustomFieldScope Scope,
        string? FieldKey,
        string LabelEn,
        string LabelId,
        CustomFieldType FieldType,
        IReadOnlyList<string>? Options,
        bool IsRequired,
        bool IsSearchable,
        bool ShowOnIntake,
        bool ShowOnDetail,
        bool ShowOnPrint,
        bool ShowOnPublicTracking,
        int SortOrder,
        bool IsActive = true);

    public sealed record InspectionChecklistTemplateDto(
        int Id,
        string ItemKey,
        string LabelEn,
        string LabelId,
        string? Category,
        int SortOrder,
        bool IsRequired,
        bool IsActive);

    public sealed record UpsertInspectionChecklistRequest(
        string? ItemKey,
        string LabelEn,
        string LabelId,
        string? Category,
        int SortOrder,
        bool IsRequired,
        bool IsActive = true);

    public sealed record WorkspaceBlueprintConfigDto(
        int WorkspaceId,
        string WorkspaceName,
        string OperatingMode,
        string IndustryBlueprintKey,
        bool SetupCompleted,
        TerminologyConfigDto Terminology,
        WorkspaceFeatureConfig FeatureConfig,
        IReadOnlyList<CustomFieldDefinitionDto> AssetFields,
        IReadOnlyList<CustomFieldDefinitionDto> TicketFields,
        IReadOnlyList<InspectionChecklistTemplateDto> InspectionChecklists);

    public sealed record UpdateTerminologyRequest(
        OperatingMode OperatingMode,
        TerminologyConfigDto Terminology,
        WorkspaceFeatureConfig? FeatureConfig = null);

    public sealed record ApplyBlueprintRequest(
        string BlueprintKey,
        bool ReplaceStatuses = false,
        bool ReplaceFieldsAndChecklists = true,
        TerminologyConfigDto? Terminology = null,
        WorkspaceFeatureConfig? FeatureConfig = null);

    public sealed record FieldValueDto(
        int FieldDefinitionId,
        string FieldKey,
        string LabelEn,
        string LabelId,
        string FieldType,
        string? Value,
        bool ShowOnPrint,
        bool ShowOnPublicTracking);

    public sealed record UpsertFieldValuesRequest(IReadOnlyList<FieldValueInput> Values);
    public sealed record FieldValueInput(int FieldDefinitionId, string? Value);

    public sealed record TicketInspectionDto(
        int Id,
        int? ChecklistTemplateId,
        string Label,
        string Status,
        string? Notes);

    public sealed record UpsertTicketInspectionsRequest(IReadOnlyList<TicketInspectionInput> Items);
    public sealed record TicketInspectionInput(int? ChecklistTemplateId, string Label, InspectionItemStatus Status, string? Notes);

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/blueprints", () => Results.Ok(IndustryBlueprintCatalog.All))
            .WithName("GetIndustryBlueprints")
            .WithOpenApi()
            .RequireAuthorization();

        app.MapGet("/blueprints/workspace-config", GetWorkspaceConfigAsync)
            .WithName("GetWorkspaceBlueprintConfig")
            .WithOpenApi()
            .RequireAuthorization();

        app.MapPut("/blueprints/terminology", UpdateTerminologyAsync)
            .WithName("UpdateWorkspaceTerminology")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPost("/blueprints/apply", ApplyBlueprintAsync)
            .WithName("ApplyIndustryBlueprint")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapGet("/custom-fields", ListCustomFieldsAsync)
            .WithName("GetCustomFields")
            .WithOpenApi()
            .RequireAuthorization();

        app.MapPost("/custom-fields", CreateCustomFieldAsync)
            .WithName("CreateCustomField")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPut("/custom-fields/{id:int}", UpdateCustomFieldAsync)
            .WithName("UpdateCustomField")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapDelete("/custom-fields/{id:int}", DeleteCustomFieldAsync)
            .WithName("DeleteCustomField")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapGet("/inspection-checklists", ListInspectionChecklistsAsync)
            .WithName("GetInspectionChecklists")
            .WithOpenApi()
            .RequireAuthorization();

        app.MapPost("/inspection-checklists", CreateInspectionChecklistAsync)
            .WithName("CreateInspectionChecklist")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPut("/inspection-checklists/{id:int}", UpdateInspectionChecklistAsync)
            .WithName("UpdateInspectionChecklist")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapDelete("/inspection-checklists/{id:int}", DeleteInspectionChecklistAsync)
            .WithName("DeleteInspectionChecklist")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapGet("/service-assets/{assetId:int}/custom-fields", GetAssetFieldValuesAsync)
            .WithName("GetAssetFieldValues")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.DevicesRead);

        app.MapPut("/service-assets/{assetId:int}/custom-fields", UpsertAssetFieldValuesAsync)
            .WithName("UpsertAssetFieldValues")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.DevicesWrite);

        app.MapGet("/service-tickets/{ticketId:int}/custom-fields", GetTicketFieldValuesAsync)
            .WithName("GetTicketFieldValues")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsRead);

        app.MapPut("/service-tickets/{ticketId:int}/custom-fields", UpsertTicketFieldValuesAsync)
            .WithName("UpsertTicketFieldValues")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);

        app.MapGet("/service-tickets/{ticketId:int}/inspections", GetTicketInspectionsAsync)
            .WithName("GetTicketInspections")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsRead);

        app.MapPut("/service-tickets/{ticketId:int}/inspections", UpsertTicketInspectionsAsync)
            .WithName("UpsertTicketInspections")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsWrite);
    }

    private static async Task<IResult> GetWorkspaceConfigAsync(
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var workspace = await shopContext.GetCurrentWorkspaceAsync(cancellationToken);

        var fields = await context.AssetFieldDefinitions
            .AsNoTracking()
            .Where(f => f.ShopId == workspace.Id && f.IsActive)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Id)
            .ToListAsync(cancellationToken);

        var checklists = await context.InspectionChecklistTemplates
            .AsNoTracking()
            .Where(c => c.ShopId == workspace.Id && c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Id)
            .Select(c => new InspectionChecklistTemplateDto(
                c.Id,
                c.ItemKey,
                c.LabelEn,
                c.LabelId,
                c.Category,
                c.SortOrder,
                c.IsRequired,
                c.IsActive))
            .ToListAsync(cancellationToken);

        var mappedFields = fields.Select(MapFieldDto).ToList();

        return Results.Ok(new WorkspaceBlueprintConfigDto(
            workspace.Id,
            workspace.Name,
            workspace.OperatingMode.ToString(),
            workspace.IndustryBlueprintKey,
            workspace.SetupCompleted,
            IndustryBlueprintCatalog.DeserializeTerminology(workspace.TerminologyConfig, workspace.IndustryBlueprintKey),
            IndustryBlueprintCatalog.DeserializeFeatureConfig(workspace.FeatureConfig, workspace.IndustryBlueprintKey, workspace.OperatingMode),
            mappedFields.Where(f => f.Scope == CustomFieldScope.Asset.ToString()).ToList(),
            mappedFields.Where(f => f.Scope == CustomFieldScope.Ticket.ToString()).ToList(),
            checklists));
    }

    private static async Task<IResult> UpdateTerminologyAsync(
        [FromBody] UpdateTerminologyRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var workspace = await context.Shops.FirstOrDefaultAsync(w => w.Id == workspaceId, cancellationToken);
        if (workspace is null) return Results.NotFound();

        workspace.OperatingMode = request.OperatingMode;
        workspace.TerminologyConfig = IndustryBlueprintCatalog.SerializeTerminology(request.Terminology);
        if (request.FeatureConfig is not null)
        {
            workspace.FeatureConfig = IndustryBlueprintCatalog.SerializeFeatureConfig(request.FeatureConfig);
        }
        await context.SaveChangesAsync(cancellationToken);

        var resolvedFeatureConfig = IndustryBlueprintCatalog.DeserializeFeatureConfig(
            workspace.FeatureConfig, workspace.IndustryBlueprintKey, workspace.OperatingMode);

        await activityLogger.LogAsync(
            "Workspace",
            workspace.Id,
            "TerminologyUpdated",
            $"Updated operating mode ({workspace.OperatingMode}), terminology labels, and workspace feature config.",
            cancellationToken: cancellationToken);

        return Results.Ok(new
        {
            workspace.Id,
            OperatingMode = workspace.OperatingMode.ToString(),
            Terminology = request.Terminology,
            FeatureConfig = resolvedFeatureConfig
        });
    }

    private static async Task<IResult> ApplyBlueprintAsync(
        [FromBody] ApplyBlueprintRequest request,
        AppDbContext context,
        IShopContext shopContext,
        BlueprintProvisioningService provisioningService,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var workspace = await context.Shops.FirstOrDefaultAsync(w => w.Id == workspaceId, cancellationToken);
        if (workspace is null) return Results.NotFound();

        await provisioningService.ApplyBlueprintAsync(
            workspace,
            request.BlueprintKey,
            request.ReplaceStatuses,
            request.ReplaceFieldsAndChecklists,
            request.Terminology,
            request.FeatureConfig,
            cancellationToken);

        await activityLogger.LogAsync(
            "Workspace",
            workspace.Id,
            "BlueprintApplied",
            $"Applied industry blueprint '{workspace.IndustryBlueprintKey}'.",
            cancellationToken: cancellationToken);

        return await GetWorkspaceConfigAsync(context, shopContext, cancellationToken);
    }

    private static async Task<IResult> ListCustomFieldsAsync(
        string? scope,
        bool includeInactive,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var query = context.AssetFieldDefinitions
            .AsNoTracking()
            .Where(f => f.ShopId == workspaceId);

        if (!includeInactive)
        {
            query = query.Where(f => f.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(scope) && Enum.TryParse<CustomFieldScope>(scope, true, out var parsedScope))
        {
            query = query.Where(f => f.Scope == parsedScope);
        }

        var items = await query
            .OrderBy(f => f.Scope)
            .ThenBy(f => f.SortOrder)
            .ThenBy(f => f.Id)
            .ToListAsync(cancellationToken);

        return Results.Ok(items.Select(MapFieldDto).ToList());
    }

    private static async Task<IResult> CreateCustomFieldAsync(
        [FromBody] UpsertCustomFieldRequest request,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.LabelEn) || string.IsNullOrWhiteSpace(request.LabelId))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["labelEn"] = ["Both English and Indonesian labels are required."]
            });
        }

        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var fieldKey = NormalizeKey(request.FieldKey ?? request.LabelEn, "field");

        var exists = await context.AssetFieldDefinitions
            .AnyAsync(f => f.ShopId == workspaceId && f.Scope == request.Scope && f.FieldKey == fieldKey, cancellationToken);

        if (exists)
        {
            return Results.Conflict(new { detail = $"Custom field key '{fieldKey}' already exists for scope {request.Scope}." });
        }

        var entity = new AssetFieldDefinition
        {
            ShopId = workspaceId,
            Scope = request.Scope,
            FieldKey = fieldKey,
            LabelEn = request.LabelEn.Trim(),
            LabelId = request.LabelId.Trim(),
            FieldType = request.FieldType,
            OptionsJson = request.Options is { Count: > 0 }
                ? JsonSerializer.Serialize(request.Options.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim()).ToArray(), IndustryBlueprintCatalog.JsonOptions)
                : null,
            IsRequired = request.IsRequired,
            IsSearchable = request.IsSearchable,
            ShowOnIntake = request.ShowOnIntake,
            ShowOnDetail = request.ShowOnDetail,
            ShowOnPrint = request.ShowOnPrint,
            ShowOnPublicTracking = request.ShowOnPublicTracking,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive
        };

        context.AssetFieldDefinitions.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/v1/custom-fields/{entity.Id}", MapFieldDto(entity));
    }

    private static async Task<IResult> UpdateCustomFieldAsync(
        int id,
        [FromBody] UpsertCustomFieldRequest request,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var entity = await context.AssetFieldDefinitions
            .FirstOrDefaultAsync(f => f.Id == id && f.ShopId == workspaceId, cancellationToken);

        if (entity is null) return Results.NotFound();

        entity.LabelEn = request.LabelEn.Trim();
        entity.LabelId = request.LabelId.Trim();
        entity.FieldType = request.FieldType;
        entity.OptionsJson = request.Options is { Count: > 0 }
            ? JsonSerializer.Serialize(request.Options.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim()).ToArray(), IndustryBlueprintCatalog.JsonOptions)
            : null;
        entity.IsRequired = request.IsRequired;
        entity.IsSearchable = request.IsSearchable;
        entity.ShowOnIntake = request.ShowOnIntake;
        entity.ShowOnDetail = request.ShowOnDetail;
        entity.ShowOnPrint = request.ShowOnPrint;
        entity.ShowOnPublicTracking = request.ShowOnPublicTracking;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;

        await context.SaveChangesAsync(cancellationToken);
        return Results.Ok(MapFieldDto(entity));
    }

    private static async Task<IResult> DeleteCustomFieldAsync(
        int id,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var entity = await context.AssetFieldDefinitions
            .FirstOrDefaultAsync(f => f.Id == id && f.ShopId == workspaceId, cancellationToken);

        if (entity is null) return Results.NotFound();

        context.AssetFieldDefinitions.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ListInspectionChecklistsAsync(
        bool includeInactive,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var query = context.InspectionChecklistTemplates
            .AsNoTracking()
            .Where(c => c.ShopId == workspaceId);

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        var items = await query
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Id)
            .Select(c => new InspectionChecklistTemplateDto(
                c.Id,
                c.ItemKey,
                c.LabelEn,
                c.LabelId,
                c.Category,
                c.SortOrder,
                c.IsRequired,
                c.IsActive))
            .ToListAsync(cancellationToken);

        return Results.Ok(items);
    }

    private static async Task<IResult> CreateInspectionChecklistAsync(
        [FromBody] UpsertInspectionChecklistRequest request,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.LabelEn) || string.IsNullOrWhiteSpace(request.LabelId))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["labelEn"] = ["Both English and Indonesian labels are required."]
            });
        }

        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var key = NormalizeKey(request.ItemKey ?? request.LabelEn, "check");

        var exists = await context.InspectionChecklistTemplates
            .AnyAsync(c => c.ShopId == workspaceId && c.ItemKey == key, cancellationToken);

        if (exists)
        {
            key = $"{key}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 10000}";
        }

        var entity = new InspectionChecklistTemplate
        {
            ShopId = workspaceId,
            ItemKey = key,
            LabelEn = request.LabelEn.Trim(),
            LabelId = request.LabelId.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim(),
            SortOrder = request.SortOrder,
            IsRequired = request.IsRequired,
            IsActive = request.IsActive
        };

        context.InspectionChecklistTemplates.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/v1/inspection-checklists/{entity.Id}", new InspectionChecklistTemplateDto(
            entity.Id, entity.ItemKey, entity.LabelEn, entity.LabelId, entity.Category, entity.SortOrder, entity.IsRequired, entity.IsActive));
    }

    private static async Task<IResult> UpdateInspectionChecklistAsync(
        int id,
        [FromBody] UpsertInspectionChecklistRequest request,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var entity = await context.InspectionChecklistTemplates
            .FirstOrDefaultAsync(c => c.Id == id && c.ShopId == workspaceId, cancellationToken);

        if (entity is null) return Results.NotFound();

        entity.LabelEn = request.LabelEn.Trim();
        entity.LabelId = request.LabelId.Trim();
        entity.Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim();
        entity.SortOrder = request.SortOrder;
        entity.IsRequired = request.IsRequired;
        entity.IsActive = request.IsActive;

        await context.SaveChangesAsync(cancellationToken);

        return Results.Ok(new InspectionChecklistTemplateDto(
            entity.Id, entity.ItemKey, entity.LabelEn, entity.LabelId, entity.Category, entity.SortOrder, entity.IsRequired, entity.IsActive));
    }

    private static async Task<IResult> DeleteInspectionChecklistAsync(
        int id,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var entity = await context.InspectionChecklistTemplates
            .FirstOrDefaultAsync(c => c.Id == id && c.ShopId == workspaceId, cancellationToken);

        if (entity is null) return Results.NotFound();

        context.InspectionChecklistTemplates.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetAssetFieldValuesAsync(
        int assetId,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var assetExists = await context.Devices
            .AnyAsync(d => d.Id == assetId && d.ShopId == workspaceId && d.DeletedAt == null, cancellationToken);
        if (!assetExists) return Results.NotFound();

        var values = await context.AssetFieldValues
            .AsNoTracking()
            .Where(v => v.ShopId == workspaceId && v.ServiceAssetId == assetId)
            .OrderBy(v => v.FieldDefinition.SortOrder)
            .Select(v => new FieldValueDto(
                v.FieldDefinitionId,
                v.FieldDefinition.FieldKey,
                v.FieldDefinition.LabelEn,
                v.FieldDefinition.LabelId,
                v.FieldDefinition.FieldType.ToString(),
                v.Value,
                v.FieldDefinition.ShowOnPrint,
                v.FieldDefinition.ShowOnPublicTracking))
            .ToListAsync(cancellationToken);

        return Results.Ok(values);
    }

    private static async Task<IResult> UpsertAssetFieldValuesAsync(
        int assetId,
        [FromBody] UpsertFieldValuesRequest request,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var assetExists = await context.Devices
            .AnyAsync(d => d.Id == assetId && d.ShopId == workspaceId && d.DeletedAt == null, cancellationToken);
        if (!assetExists) return Results.NotFound();

        var validFieldIds = await context.AssetFieldDefinitions
            .Where(f => f.ShopId == workspaceId && f.Scope == CustomFieldScope.Asset)
            .Select(f => f.Id)
            .ToHashSetAsync(cancellationToken);

        var existing = await context.AssetFieldValues
            .Where(v => v.ShopId == workspaceId && v.ServiceAssetId == assetId)
            .ToDictionaryAsync(v => v.FieldDefinitionId, cancellationToken);

        foreach (var item in request.Values)
        {
            if (!validFieldIds.Contains(item.FieldDefinitionId)) continue;

            var trimmed = string.IsNullOrWhiteSpace(item.Value) ? null : item.Value.Trim();
            if (existing.TryGetValue(item.FieldDefinitionId, out var row))
            {
                row.Value = trimmed;
            }
            else if (trimmed is not null)
            {
                context.AssetFieldValues.Add(new AssetFieldValue
                {
                    ShopId = workspaceId,
                    ServiceAssetId = assetId,
                    FieldDefinitionId = item.FieldDefinitionId,
                    Value = trimmed
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return await GetAssetFieldValuesAsync(assetId, context, shopContext, cancellationToken);
    }

    private static async Task<IResult> GetTicketFieldValuesAsync(
        int ticketId,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var ticketExists = await context.ServiceTickets
            .AnyAsync(t => t.Id == ticketId && t.ShopId == workspaceId && t.DeletedAt == null, cancellationToken);
        if (!ticketExists) return Results.NotFound();

        var values = await context.TicketFieldValues
            .AsNoTracking()
            .Where(v => v.ShopId == workspaceId && v.ServiceTicketId == ticketId)
            .OrderBy(v => v.FieldDefinition.SortOrder)
            .Select(v => new FieldValueDto(
                v.FieldDefinitionId,
                v.FieldDefinition.FieldKey,
                v.FieldDefinition.LabelEn,
                v.FieldDefinition.LabelId,
                v.FieldDefinition.FieldType.ToString(),
                v.Value,
                v.FieldDefinition.ShowOnPrint,
                v.FieldDefinition.ShowOnPublicTracking))
            .ToListAsync(cancellationToken);

        return Results.Ok(values);
    }

    private static async Task<IResult> UpsertTicketFieldValuesAsync(
        int ticketId,
        [FromBody] UpsertFieldValuesRequest request,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var ticketExists = await context.ServiceTickets
            .AnyAsync(t => t.Id == ticketId && t.ShopId == workspaceId && t.DeletedAt == null, cancellationToken);
        if (!ticketExists) return Results.NotFound();

        var validFieldIds = await context.AssetFieldDefinitions
            .Where(f => f.ShopId == workspaceId && f.Scope == CustomFieldScope.Ticket)
            .Select(f => f.Id)
            .ToHashSetAsync(cancellationToken);

        var existing = await context.TicketFieldValues
            .Where(v => v.ShopId == workspaceId && v.ServiceTicketId == ticketId)
            .ToDictionaryAsync(v => v.FieldDefinitionId, cancellationToken);

        foreach (var item in request.Values)
        {
            if (!validFieldIds.Contains(item.FieldDefinitionId)) continue;

            var trimmed = string.IsNullOrWhiteSpace(item.Value) ? null : item.Value.Trim();
            if (existing.TryGetValue(item.FieldDefinitionId, out var row))
            {
                row.Value = trimmed;
            }
            else if (trimmed is not null)
            {
                context.TicketFieldValues.Add(new TicketFieldValue
                {
                    ShopId = workspaceId,
                    ServiceTicketId = ticketId,
                    FieldDefinitionId = item.FieldDefinitionId,
                    Value = trimmed
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return await GetTicketFieldValuesAsync(ticketId, context, shopContext, cancellationToken);
    }

    private static async Task<IResult> GetTicketInspectionsAsync(
        int ticketId,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var ticketExists = await context.ServiceTickets
            .AnyAsync(t => t.Id == ticketId && t.ShopId == workspaceId && t.DeletedAt == null, cancellationToken);
        if (!ticketExists) return Results.NotFound();

        var items = await context.TicketInspectionResults
            .AsNoTracking()
            .Where(r => r.ShopId == workspaceId && r.ServiceTicketId == ticketId)
            .OrderBy(r => r.Id)
            .Select(r => new TicketInspectionDto(r.Id, r.ChecklistTemplateId, r.Label, r.Status.ToString(), r.Notes))
            .ToListAsync(cancellationToken);

        return Results.Ok(items);
    }

    private static async Task<IResult> UpsertTicketInspectionsAsync(
        int ticketId,
        [FromBody] UpsertTicketInspectionsRequest request,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var ticketExists = await context.ServiceTickets
            .AnyAsync(t => t.Id == ticketId && t.ShopId == workspaceId && t.DeletedAt == null, cancellationToken);
        if (!ticketExists) return Results.NotFound();

        var existing = await context.TicketInspectionResults
            .Where(r => r.ShopId == workspaceId && r.ServiceTicketId == ticketId)
            .ToListAsync(cancellationToken);

        context.TicketInspectionResults.RemoveRange(existing);

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Label)) continue;

            context.TicketInspectionResults.Add(new TicketInspectionResult
            {
                ShopId = workspaceId,
                ServiceTicketId = ticketId,
                ChecklistTemplateId = item.ChecklistTemplateId,
                Label = item.Label.Trim(),
                Status = item.Status,
                Notes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim()
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        return await GetTicketInspectionsAsync(ticketId, context, shopContext, cancellationToken);
    }

    private static CustomFieldDefinitionDto MapFieldDto(AssetFieldDefinition f)
    {
        IReadOnlyList<string> options = [];
        if (!string.IsNullOrWhiteSpace(f.OptionsJson))
        {
            try
            {
                options = JsonSerializer.Deserialize<string[]>(f.OptionsJson, IndustryBlueprintCatalog.JsonOptions) ?? [];
            }
            catch
            {
                options = [];
            }
        }

        return new CustomFieldDefinitionDto(
            f.Id,
            f.Scope.ToString(),
            f.FieldKey,
            f.LabelEn,
            f.LabelId,
            f.FieldType.ToString(),
            options,
            f.IsRequired,
            f.IsSearchable,
            f.ShowOnIntake,
            f.ShowOnDetail,
            f.ShowOnPrint,
            f.ShowOnPublicTracking,
            f.SortOrder,
            f.IsActive);
    }

    private static string NormalizeKey(string raw, string fallback)
    {
        var key = Regex.Replace(raw.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
        if (string.IsNullOrWhiteSpace(key)) key = fallback;
        return key.Length > 55 ? key[..55].TrimEnd('_') : key;
    }
}
