using System.Security.Claims;
using Servora.Api.Features.Blueprints;
using Servora.Api.Features.Identity.Users;
using Servora.Api.Features.Organizations;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Servora.Api.Features.Identity.Auth;

public static class CurrentUser
{
    public sealed record OrganizationContextDto(
        int Id,
        string Name,
        string Slug,
        string? LogoUrl,
        string? Role);

    public sealed record WorkspaceSummaryDto(
        int Id,
        int OrganizationId,
        string OrganizationName,
        string Name,
        string Slug,
        string OperatingMode,
        string IndustryBlueprintKey,
        string DefaultLocale,
        string TimeZone,
        string CurrencyCode,
        bool SetupCompleted);

    public sealed record ShopContextDto(
        int Id,
        int OrganizationId,
        string OrganizationName,
        string Name,
        string Slug,
        string OperatingMode,
        string IndustryBlueprintKey,
        TerminologyConfigDto Terminology,
        string DefaultLocale,
        string TimeZone,
        string CurrencyCode,
        bool SetupCompleted,
        WorkspaceFeatureConfig FeatureConfig);

    public sealed record UserProfile(
        int Id,
        string Email,
        string FullName,
        ShopContextDto Shop,
        ShopContextDto Workspace,
        OrganizationContextDto? Organization,
        IReadOnlyCollection<WorkspaceSummaryDto> AccessibleWorkspaces,
        IReadOnlyCollection<string> Roles,
        IReadOnlyCollection<string> Permissions);

    public sealed record SwitchWorkspaceRequest(int WorkspaceId);

    public static async Task<UserProfile> CreateProfileAsync(
        ApplicationUser user,
        UserManager<ApplicationUser> userManager,
        AppDbContext context,
        CancellationToken cancellationToken,
        int? preferredWorkspaceId = null)
    {
        var memberships = await context.ShopUsers
            .AsNoTracking()
            .Where(member => member.UserId == user.Id && member.Status == ShopUserStatus.Active && member.Shop.IsActive)
            .OrderBy(member => member.ShopId)
            .Select(member => new
            {
                member.Id,
                member.ShopId,
                member.Shop.OrganizationId,
                OrganizationName = member.Shop.Organization.Name,
                OrganizationSlug = member.Shop.Organization.Slug,
                OrganizationLogoUrl = member.Shop.Organization.LogoUrl,
                member.Shop.Name,
                member.Shop.Slug,
                member.Shop.OperatingMode,
                member.Shop.IndustryBlueprintKey,
                member.Shop.TerminologyConfig,
                member.Shop.FeatureConfig,
                member.Shop.DefaultLocale,
                member.Shop.TimeZone,
                member.Shop.CurrencyCode,
                member.Shop.SetupCompleted
            })
            .ToListAsync(cancellationToken);

        var accessibleWorkspaces = memberships
            .Select(m => new WorkspaceSummaryDto(
                m.ShopId,
                m.OrganizationId,
                m.OrganizationName,
                m.Name,
                m.Slug,
                m.OperatingMode.ToString(),
                m.IndustryBlueprintKey,
                m.DefaultLocale,
                m.TimeZone,
                m.CurrencyCode,
                m.SetupCompleted))
            .ToArray();

        var activeMembership = preferredWorkspaceId.HasValue
            ? memberships.FirstOrDefault(m => m.ShopId == preferredWorkspaceId.Value) ?? memberships.FirstOrDefault()
            : memberships.FirstOrDefault();

        if (activeMembership is null)
        {
            var defaultShop = await context.Shops
                .AsNoTracking()
                .Include(shop => shop.Organization)
                .Where(shop => shop.IsActive)
                .OrderBy(shop => shop.Id)
                .FirstAsync(cancellationToken);

            var fallbackCtx = new ShopContextDto(
                defaultShop.Id,
                defaultShop.OrganizationId,
                defaultShop.Organization.Name,
                defaultShop.Name,
                defaultShop.Slug,
                defaultShop.OperatingMode.ToString(),
                defaultShop.IndustryBlueprintKey,
                IndustryBlueprintCatalog.DeserializeTerminology(defaultShop.TerminologyConfig, defaultShop.IndustryBlueprintKey),
                defaultShop.DefaultLocale,
                defaultShop.TimeZone,
                defaultShop.CurrencyCode,
                defaultShop.SetupCompleted,
                IndustryBlueprintCatalog.DeserializeFeatureConfig(defaultShop.FeatureConfig, defaultShop.IndustryBlueprintKey, defaultShop.OperatingMode));

            return new UserProfile(
                user.Id,
                user.Email ?? string.Empty,
                $"{user.FirstName} {user.LastName}".Trim(),
                fallbackCtx,
                fallbackCtx,
                new OrganizationContextDto(defaultShop.OrganizationId, defaultShop.Organization.Name, defaultShop.Organization.Slug, defaultShop.Organization.LogoUrl, null),
                [],
                [],
                []);
        }

        var orgMembership = await context.OrganizationUsers
            .AsNoTracking()
            .Where(ou => ou.OrganizationId == activeMembership.OrganizationId && ou.UserId == user.Id && ou.Status == OrganizationUserStatus.Active)
            .Select(ou => (OrganizationRole?)ou.Role)
            .FirstOrDefaultAsync(cancellationToken);

        var userRoles = await context.ShopUserRoles
            .Where(userRole => userRole.ShopUserId == activeMembership.Id)
            .Join(context.Roles, userRole => userRole.RoleId, role => role.Id, (_, role) => new
            {
                RoleId = role.Id,
                RoleName = role.Name!
            })
            .ToListAsync(cancellationToken);

        var permissions = await context.ShopUserRoles
            .Where(userRole => userRole.ShopUserId == activeMembership.Id)
            .Join(context.RolePermissions, userRole => userRole.RoleId, rolePermission => rolePermission.RoleId, (_, rolePermission) => rolePermission.Permission.Code)
            .Distinct()
            .OrderBy(permission => permission)
            .ToListAsync(cancellationToken);

        var workspaceDto = new ShopContextDto(
            activeMembership.ShopId,
            activeMembership.OrganizationId,
            activeMembership.OrganizationName,
            activeMembership.Name,
            activeMembership.Slug,
            activeMembership.OperatingMode.ToString(),
            activeMembership.IndustryBlueprintKey,
            IndustryBlueprintCatalog.DeserializeTerminology(activeMembership.TerminologyConfig, activeMembership.IndustryBlueprintKey),
            activeMembership.DefaultLocale,
            activeMembership.TimeZone,
            activeMembership.CurrencyCode,
            activeMembership.SetupCompleted,
            IndustryBlueprintCatalog.DeserializeFeatureConfig(activeMembership.FeatureConfig, activeMembership.IndustryBlueprintKey, activeMembership.OperatingMode));

        var orgDto = new OrganizationContextDto(
            activeMembership.OrganizationId,
            activeMembership.OrganizationName,
            activeMembership.OrganizationSlug,
            activeMembership.OrganizationLogoUrl,
            orgMembership?.ToString());

        return new UserProfile(
            user.Id,
            user.Email ?? string.Empty,
            $"{user.FirstName} {user.LastName}".Trim(),
            workspaceDto,
            workspaceDto,
            orgDto,
            accessibleWorkspaces,
            userRoles.Select(role => role.RoleName).OrderBy(role => role).ToArray(),
            permissions);
    }

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/auth/me", async (
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager,
            AppDbContext context,
            IShopContext shopContext,
            CancellationToken cancellationToken) =>
        {
            var user = await userManager.GetUserAsync(principal);
            if (user is null || !user.IsActive)
            {
                return Results.Unauthorized();
            }

            var activeWorkspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
            return Results.Ok(await CreateProfileAsync(user, userManager, context, cancellationToken, activeWorkspaceId));
        })
        .WithName("CurrentUser")
        .WithOpenApi()
        .RequireAuthorization();

        app.MapPost("/auth/switch-workspace", async (
            [FromBody] SwitchWorkspaceRequest request,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager,
            AppDbContext context,
            JwtTokenService tokenService,
            CancellationToken cancellationToken) =>
        {
            var user = await userManager.GetUserAsync(principal);
            if (user is null || !user.IsActive)
            {
                return Results.Unauthorized();
            }

            var isMember = await context.ShopUsers
                .AsNoTracking()
                .AnyAsync(
                    su => su.UserId == user.Id
                        && su.ShopId == request.WorkspaceId
                        && su.Status == ShopUserStatus.Active
                        && su.Shop.IsActive,
                    cancellationToken);

            if (!isMember)
            {
                return Results.Problem(
                    title: "Workspace access denied",
                    detail: "You do not have active membership in the requested workspace.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var token = await tokenService.CreateTokenAsync(user, request.WorkspaceId);
            var profile = await CreateProfileAsync(user, userManager, context, cancellationToken, request.WorkspaceId);

            return Results.Ok(new Login.Response(token.AccessToken, token.ExpiresAt, profile));
        })
        .WithName("SwitchWorkspace")
        .WithOpenApi()
        .RequireAuthorization();
    }
}
