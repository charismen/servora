using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data;

namespace Servora.Api.Infrastructure.ShopContext;

public sealed class ShopContext : IShopContext
{
    public const string WorkspaceHeaderName = "X-Workspace-Id";
    public const string WorkspaceClaimType = "workspace_id";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _context;
    private int? _cachedShopId;
    private Shop? _cachedWorkspace;

    public ShopContext(IHttpContextAccessor httpContextAccessor, AppDbContext context)
    {
        _httpContextAccessor = httpContextAccessor;
        _context = context;
    }

    public async Task<int> GetCurrentShopIdAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedShopId.HasValue) return _cachedShopId.Value;

        var httpContext = _httpContextAccessor.HttpContext;
        var userIdValue = httpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (int.TryParse(userIdValue, out var userId))
        {
            // 1. Check explicit workspace selection header (must be validated against active DB membership!)
            var headerValue = httpContext?.Request.Headers[WorkspaceHeaderName].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerValue) && int.TryParse(headerValue, out var requestedWorkspaceId) && requestedWorkspaceId > 0)
            {
                var isAuthorizedMember = await _context.ShopUsers
                    .AsNoTracking()
                    .AnyAsync(
                        member => member.UserId == userId
                            && member.ShopId == requestedWorkspaceId
                            && member.Status == ShopUserStatus.Active
                            && member.Shop.IsActive,
                        cancellationToken);

                if (!isAuthorizedMember)
                {
                    throw new UnauthorizedAccessException("Access denied to the requested workspace.");
                }

                _cachedShopId = requestedWorkspaceId;
                return requestedWorkspaceId;
            }

            // 2. Check workspace_id claim embedded in signed JWT (still verified against active DB membership)
            var claimValue = httpContext?.User.FindFirstValue(WorkspaceClaimType);
            if (!string.IsNullOrWhiteSpace(claimValue) && int.TryParse(claimValue, out var claimWorkspaceId) && claimWorkspaceId > 0)
            {
                var isClaimValid = await _context.ShopUsers
                    .AsNoTracking()
                    .AnyAsync(
                        member => member.UserId == userId
                            && member.ShopId == claimWorkspaceId
                            && member.Status == ShopUserStatus.Active
                            && member.Shop.IsActive,
                        cancellationToken);

                if (isClaimValid)
                {
                    _cachedShopId = claimWorkspaceId;
                    return claimWorkspaceId;
                }
            }

            // 3. Default to user's first active workspace membership
            var shopId = await _context.ShopUsers
                .AsNoTracking()
                .Where(shopUser => shopUser.UserId == userId && shopUser.Status == ShopUserStatus.Active && shopUser.Shop.IsActive)
                .OrderBy(shopUser => shopUser.ShopId)
                .Select(shopUser => shopUser.ShopId)
                .FirstOrDefaultAsync(cancellationToken);

            if (shopId > 0)
            {
                _cachedShopId = shopId;
                return shopId;
            }
        }

        // Fallback for unauthenticated or non-user contexts
        var defaultShopId = await _context.Shops
            .AsNoTracking()
            .Where(shop => shop.IsActive)
            .OrderBy(shop => shop.Id)
            .Select(shop => shop.Id)
            .FirstAsync(cancellationToken);

        _cachedShopId = defaultShopId;
        return defaultShopId;
    }

    public async Task<Shop> GetCurrentWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedWorkspace is not null) return _cachedWorkspace;

        var shopId = await GetCurrentShopIdAsync(cancellationToken);
        _cachedWorkspace = await _context.Shops
            .AsNoTracking()
            .Include(shop => shop.Organization)
            .FirstAsync(shop => shop.Id == shopId, cancellationToken);

        return _cachedWorkspace;
    }
}
