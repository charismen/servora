using System.Security.Claims;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Servora.Api.Infrastructure.Auth;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IServiceProvider _serviceProvider;

    public PermissionAuthorizationHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var userIdString = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out var userId))
        {
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var shopContext = scope.ServiceProvider.GetRequiredService<IShopContext>();

        int activeWorkspaceId;
        try
        {
            activeWorkspaceId = await shopContext.GetCurrentShopIdAsync();
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        var shopUserId = await dbContext.ShopUsers
            .Where(shopUser => shopUser.UserId == userId
                && shopUser.ShopId == activeWorkspaceId
                && shopUser.Status == ShopUserStatus.Active
                && shopUser.Shop.IsActive)
            .Select(shopUser => shopUser.Id)
            .FirstOrDefaultAsync();

        if (shopUserId == 0) return;

        var roleIds = dbContext.ShopUserRoles
            .Where(shopUserRole => shopUserRole.ShopUserId == shopUserId)
            .Select(shopUserRole => shopUserRole.RoleId);

        var hasPermission = await dbContext.RolePermissions
            .Where(rolePermission => roleIds.Contains(rolePermission.RoleId))
            .AnyAsync(rolePermission => rolePermission.Permission.Code == requirement.Permission);

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}
