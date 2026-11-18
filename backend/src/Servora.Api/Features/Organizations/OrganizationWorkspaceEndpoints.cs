using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Blueprints;
using Servora.Api.Features.Identity.Auth;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Features.Identity.Users;
using Servora.Api.Features.Maintenance;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Features.Settings;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Activity;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;
using Microsoft.AspNetCore.Identity;

namespace Servora.Api.Features.Organizations;

public static class OrganizationWorkspaceEndpoints
{
    public sealed record CreateWorkspaceRequest(
        string Name,
        string? Slug,
        string? PhoneNumber,
        string? Address,
        OperatingMode OperatingMode,
        string IndustryBlueprintKey,
        string? DefaultLocale,
        string? TimeZone,
        string? CurrencyCode,
        TerminologyConfigDto? Terminology,
        bool SeedSampleData = false,
        WorkspaceFeatureConfig? FeatureConfig = null);

    public sealed record CompleteSetupRequest(
        string OrganizationName,
        string? OrganizationLogoUrl,
        string WorkspaceName,
        string? WorkspaceSlug,
        string? PhoneNumber,
        string? Address,
        OperatingMode OperatingMode,
        string IndustryBlueprintKey,
        string DefaultLocale,
        string TimeZone,
        string CurrencyCode,
        TerminologyConfigDto? Terminology,
        bool ReplaceStatuses = false,
        bool ReplaceFieldsAndChecklists = false,
        bool SeedSampleData = false,
        WorkspaceFeatureConfig? FeatureConfig = null);

    public sealed record WorkspaceKpiDto(
        int WorkspaceId,
        string Name,
        string Slug,
        string OperatingMode,
        string IndustryBlueprintKey,
        string CurrencyCode,
        int OpenTickets,
        int OverdueTickets,
        int CompletedToday,
        int ActiveServiceAssets,
        int DueMaintenanceSchedules,
        decimal RevenuePaid);

    public sealed record OrganizationSummaryDto(
        int OrganizationId,
        string OrganizationName,
        string OrganizationSlug,
        int TotalWorkspaces,
        int TotalOpenTickets,
        int TotalOverdueTickets,
        int TotalCompletedToday,
        int TotalServiceAssets,
        int TotalDueMaintenanceSchedules,
        IReadOnlyCollection<WorkspaceKpiDto> Workspaces);

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/workspaces", ListAccessibleWorkspacesAsync)
            .WithName("GetAccessibleWorkspaces")
            .WithOpenApi()
            .RequireAuthorization();

        app.MapPost("/workspaces", CreateWorkspaceAsync)
            .WithName("CreateWorkspace")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPut("/workspaces/current/setup", CompleteSetupWizardAsync)
            .WithName("CompleteWorkspaceSetup")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapGet("/organizations/summary", GetOrganizationSummaryAsync)
            .WithName("GetOrganizationSummary")
            .WithOpenApi()
            .RequireAuthorization();
    }

    private static async Task<IResult> ListAccessibleWorkspacesAsync(
        ClaimsPrincipal principal,
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId))
        {
            return Results.Unauthorized();
        }

        var workspaces = await context.ShopUsers
            .AsNoTracking()
            .Where(su => su.UserId == userId && su.Status == ShopUserStatus.Active && su.Shop.IsActive)
            .OrderBy(su => su.ShopId)
            .Select(su => new CurrentUser.WorkspaceSummaryDto(
                su.ShopId,
                su.Shop.OrganizationId,
                su.Shop.Organization.Name,
                su.Shop.Name,
                su.Shop.Slug,
                su.Shop.OperatingMode.ToString(),
                su.Shop.IndustryBlueprintKey,
                su.Shop.DefaultLocale,
                su.Shop.TimeZone,
                su.Shop.CurrencyCode,
                su.Shop.SetupCompleted))
            .ToListAsync(cancellationToken);

        return Results.Ok(workspaces);
    }

    private static async Task<IResult> CreateWorkspaceAsync(
        [FromBody] CreateWorkspaceRequest request,
        ClaimsPrincipal principal,
        AppDbContext context,
        IShopContext shopContext,
        BlueprintProvisioningService blueprintProvisioning,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = ["Workspace name is required."]
            });
        }

        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId))
        {
            return Results.Unauthorized();
        }

        var currentWorkspace = await shopContext.GetCurrentWorkspaceAsync(cancellationToken);
        var slug = await EnsureUniqueSlugAsync(context, request.Slug ?? request.Name, null, cancellationToken);
        var blueprint = IndustryBlueprintCatalog.GetByKey(request.IndustryBlueprintKey);

        var workspace = new Shop
        {
            OrganizationId = currentWorkspace.OrganizationId,
            Name = request.Name.Trim(),
            Slug = slug,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            OperatingMode = request.OperatingMode,
            IndustryBlueprintKey = blueprint.Key,
            TerminologyConfig = IndustryBlueprintCatalog.SerializeTerminology(request.Terminology ?? blueprint.Terminology),
            FeatureConfig = IndustryBlueprintCatalog.SerializeFeatureConfig(
                request.FeatureConfig ?? IndustryBlueprintCatalog.GetDefaultFeatureConfig(blueprint.Key, request.OperatingMode)),
            DefaultLocale = string.IsNullOrWhiteSpace(request.DefaultLocale) ? "en" : request.DefaultLocale.Trim(),
            TimeZone = string.IsNullOrWhiteSpace(request.TimeZone) ? currentWorkspace.TimeZone : request.TimeZone.Trim(),
            CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode) ? currentWorkspace.CurrencyCode : request.CurrencyCode.Trim().ToUpperInvariant(),
            SetupCompleted = true,
            IsActive = true
        };

        context.Shops.Add(workspace);
        await context.SaveChangesAsync(cancellationToken);

        // Add creator as Owner/Admin in the new workspace
        var ownerRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Owner/Admin", cancellationToken);
        var membership = new ShopUser
        {
            ShopId = workspace.Id,
            UserId = userId,
            Status = ShopUserStatus.Active,
            EmploymentType = EmploymentType.FullTime
        };
        context.ShopUsers.Add(membership);
        await context.SaveChangesAsync(cancellationToken);

        if (ownerRole is not null)
        {
            context.ShopUserRoles.Add(new ShopUserRole
            {
                ShopUserId = membership.Id,
                RoleId = ownerRole.Id
            });
            await context.SaveChangesAsync(cancellationToken);
        }

        await blueprintProvisioning.ApplyBlueprintAsync(
            workspace,
            blueprint.Key,
            replaceExistingStatuses: true,
            replaceExistingFieldsAndChecklists: true,
            customTerminology: request.Terminology,
            customFeatureConfig: request.FeatureConfig,
            cancellationToken: cancellationToken);

        if (request.SeedSampleData)
        {
            await blueprintProvisioning.SeedSampleDataForBlueprintAsync(workspace, cancellationToken);
        }

        await activityLogger.LogAsync(
            "Workspace",
            workspace.Id,
            "WorkspaceCreated",
            $"Created workspace '{workspace.Name}' ({workspace.OperatingMode}, blueprint: {workspace.IndustryBlueprintKey})",
            cancellationToken: cancellationToken);

        return Results.Created($"/api/v1/workspaces/{workspace.Id}", new CurrentUser.WorkspaceSummaryDto(
            workspace.Id,
            workspace.OrganizationId,
            currentWorkspace.Organization.Name,
            workspace.Name,
            workspace.Slug,
            workspace.OperatingMode.ToString(),
            workspace.IndustryBlueprintKey,
            workspace.DefaultLocale,
            workspace.TimeZone,
            workspace.CurrencyCode,
            workspace.SetupCompleted));
    }

    private static async Task<IResult> CompleteSetupWizardAsync(
        [FromBody] CompleteSetupRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        AppDbContext context,
        IShopContext shopContext,
        BlueprintProvisioningService blueprintProvisioning,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.WorkspaceName) || string.IsNullOrWhiteSpace(request.OrganizationName))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["workspaceName"] = ["Organization and workspace names are required."]
            });
        }

        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var workspace = await context.Shops
            .Include(w => w.Organization)
            .FirstOrDefaultAsync(w => w.Id == workspaceId, cancellationToken);

        if (workspace is null)
        {
            return Results.NotFound();
        }

        workspace.Organization.Name = request.OrganizationName.Trim();
        if (!string.IsNullOrWhiteSpace(request.OrganizationLogoUrl))
        {
            workspace.Organization.LogoUrl = request.OrganizationLogoUrl.Trim();
        }

        workspace.Name = request.WorkspaceName.Trim();
        if (!string.IsNullOrWhiteSpace(request.WorkspaceSlug))
        {
            workspace.Slug = await EnsureUniqueSlugAsync(context, request.WorkspaceSlug, workspace.Id, cancellationToken);
        }

        workspace.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? workspace.PhoneNumber : request.PhoneNumber.Trim();
        workspace.Address = string.IsNullOrWhiteSpace(request.Address) ? workspace.Address : request.Address.Trim();
        workspace.OperatingMode = request.OperatingMode;
        workspace.DefaultLocale = string.IsNullOrWhiteSpace(request.DefaultLocale) ? "en" : request.DefaultLocale.Trim();
        workspace.TimeZone = string.IsNullOrWhiteSpace(request.TimeZone) ? "Asia/Jakarta" : request.TimeZone.Trim();
        workspace.CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode) ? "IDR" : request.CurrencyCode.Trim().ToUpperInvariant();
        workspace.SetupCompleted = true;

        await blueprintProvisioning.ApplyBlueprintAsync(
            workspace,
            request.IndustryBlueprintKey,
            replaceExistingStatuses: request.ReplaceStatuses,
            replaceExistingFieldsAndChecklists: request.ReplaceFieldsAndChecklists,
            customTerminology: request.Terminology,
            customFeatureConfig: request.FeatureConfig,
            cancellationToken: cancellationToken);

        // Sync key workspace profile settings in app_settings
        await UpsertSettingAsync(context, workspace.Id, ShopProfileSettingKeys.ShopName, workspace.Name, cancellationToken);
        await UpsertSettingAsync(context, workspace.Id, ShopProfileSettingKeys.CurrencyCode, workspace.CurrencyCode, cancellationToken);
        await UpsertSettingAsync(context, workspace.Id, ShopProfileSettingKeys.OperationalTimeZone, workspace.TimeZone, cancellationToken);
        if (!string.IsNullOrWhiteSpace(workspace.PhoneNumber))
        {
            await UpsertSettingAsync(context, workspace.Id, ShopProfileSettingKeys.PhoneNumber, workspace.PhoneNumber, cancellationToken);
        }
        if (!string.IsNullOrWhiteSpace(workspace.Address))
        {
            await UpsertSettingAsync(context, workspace.Id, ShopProfileSettingKeys.Address, workspace.Address, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);

        if (request.SeedSampleData)
        {
            await blueprintProvisioning.SeedSampleDataForBlueprintAsync(workspace, cancellationToken);
        }

        await activityLogger.LogAsync(
            "Workspace",
            workspace.Id,
            "WorkspaceSetupCompleted",
            $"Completed onboarding wizard for '{workspace.Name}' ({workspace.OperatingMode}, blueprint: {workspace.IndustryBlueprintKey})",
            cancellationToken: cancellationToken);

        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return Results.Ok(new { workspace.Id, workspace.SetupCompleted });
        }

        var profile = await CurrentUser.CreateProfileAsync(user, userManager, context, cancellationToken, workspace.Id);
        return Results.Ok(profile);
    }

    private static async Task<IResult> GetOrganizationSummaryAsync(
        ClaimsPrincipal principal,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId))
        {
            return Results.Unauthorized();
        }

        var currentWorkspace = await shopContext.GetCurrentWorkspaceAsync(cancellationToken);
        var orgId = currentWorkspace.OrganizationId;

        var orgMembership = await context.OrganizationUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                ou => ou.OrganizationId == orgId && ou.UserId == userId && ou.Status == OrganizationUserStatus.Active,
                cancellationToken);

        var hasElevatedOrgRole = orgMembership is not null && orgMembership.Role is OrganizationRole.Owner or OrganizationRole.Admin or OrganizationRole.Executive;

        // Also allow if user has Permissions.OrganizationsRead in current workspace
        var activeShopUserId = await context.ShopUsers
            .AsNoTracking()
            .Where(su => su.UserId == userId && su.ShopId == currentWorkspace.Id && su.Status == ShopUserStatus.Active)
            .Select(su => su.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var hasOrgReadPermission = activeShopUserId > 0 && await context.ShopUserRoles
            .AsNoTracking()
            .Where(sur => sur.ShopUserId == activeShopUserId)
            .Join(context.RolePermissions, sur => sur.RoleId, rp => rp.RoleId, (_, rp) => rp.Permission.Code)
            .AnyAsync(code => code == ApplicationPermissions.OrganizationsRead, cancellationToken);

        if (!hasElevatedOrgRole && !hasOrgReadPermission)
        {
            return Results.Forbid();
        }

        var workspaces = await context.Shops
            .AsNoTracking()
            .Where(w => w.OrganizationId == orgId && w.IsActive)
            .OrderBy(w => w.Id)
            .ToListAsync(cancellationToken);

        var workspaceIds = workspaces.Select(w => w.Id).ToList();
        var todayUtcStart = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        var now = DateTimeOffset.UtcNow;

        var ticketStats = await context.ServiceTickets
            .AsNoTracking()
            .Where(t => workspaceIds.Contains(t.ShopId) && t.DeletedAt == null)
            .Select(t => new
            {
                t.ShopId,
                t.Status.IsFinal,
                t.Status.IsCancelled,
                t.SlaDueAt,
                t.CreatedAt,
                t.UpdatedAt,
                t.AmountPaid
            })
            .ToListAsync(cancellationToken);

        var assetShopIds = await context.Devices
            .AsNoTracking()
            .Where(d => workspaceIds.Contains(d.ShopId) && d.DeletedAt == null)
            .Select(d => d.ShopId)
            .ToListAsync(cancellationToken);
        var assetCounts = assetShopIds.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());

        var scheduleRows = await context.MaintenanceSchedules
            .AsNoTracking()
            .Where(s => workspaceIds.Contains(s.ShopId) && s.Status == MaintenanceScheduleStatus.Active)
            .Select(s => new
            {
                s.ShopId,
                s.NextDueAt,
                s.NextDueMeter,
                MeterReading = s.ServiceAsset.MeterReading
            })
            .ToListAsync(cancellationToken);

        var dueSchedules = scheduleRows
            .Where(s => (s.NextDueAt.HasValue && s.NextDueAt.Value <= now)
                || (s.NextDueMeter.HasValue && s.MeterReading.HasValue && s.MeterReading.Value >= s.NextDueMeter.Value))
            .GroupBy(s => s.ShopId)
            .ToDictionary(g => g.Key, g => g.Count());

        var kpis = workspaces.Select(w =>
        {
            var rows = ticketStats.Where(t => t.ShopId == w.Id).ToList();
            var open = rows.Count(t => !t.IsFinal && !t.IsCancelled);
            var overdue = rows.Count(t => !t.IsFinal && !t.IsCancelled && (
                (t.SlaDueAt.HasValue && t.SlaDueAt.Value < now) ||
                (!t.SlaDueAt.HasValue && t.CreatedAt < now.AddDays(-7))));
            var completedToday = rows.Count(t => t.IsFinal && (t.UpdatedAt ?? t.CreatedAt) >= todayUtcStart);
            var revenue = rows.Sum(t => t.AmountPaid);

            return new WorkspaceKpiDto(
                w.Id,
                w.Name,
                w.Slug,
                w.OperatingMode.ToString(),
                w.IndustryBlueprintKey,
                w.CurrencyCode,
                open,
                overdue,
                completedToday,
                assetCounts.GetValueOrDefault(w.Id, 0),
                dueSchedules.GetValueOrDefault(w.Id, 0),
                revenue);
        }).ToList();

        return Results.Ok(new OrganizationSummaryDto(
            currentWorkspace.OrganizationId,
            currentWorkspace.Organization.Name,
            currentWorkspace.Organization.Slug,
            kpis.Count,
            kpis.Sum(k => k.OpenTickets),
            kpis.Sum(k => k.OverdueTickets),
            kpis.Sum(k => k.CompletedToday),
            kpis.Sum(k => k.ActiveServiceAssets),
            kpis.Sum(k => k.DueMaintenanceSchedules),
            kpis));
    }

    private static async Task<string> EnsureUniqueSlugAsync(AppDbContext context, string raw, int? excludeId, CancellationToken cancellationToken)
    {
        var baseSlug = Regex.Replace(raw.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = "workspace";
        }
        if (baseSlug.Length > 70)
        {
            baseSlug = baseSlug[..70].TrimEnd('-');
        }

        var candidate = baseSlug;
        var suffix = 2;
        while (await context.Shops.AnyAsync(s => s.Slug == candidate && (!excludeId.HasValue || s.Id != excludeId.Value), cancellationToken))
        {
            candidate = $"{baseSlug}-{suffix++}";
        }

        return candidate;
    }

    private static async Task UpsertSettingAsync(AppDbContext context, int workspaceId, string key, string value, CancellationToken cancellationToken)
    {
        var setting = await context.AppSettings.FirstOrDefaultAsync(s => s.ShopId == workspaceId && s.Key == key, cancellationToken);
        if (setting is null)
        {
            context.AppSettings.Add(new AppSetting { ShopId = workspaceId, Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
        }
    }
}
