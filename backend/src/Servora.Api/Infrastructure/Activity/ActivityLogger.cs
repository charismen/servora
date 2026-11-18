using System.Security.Claims;
using Servora.Api.Features.Activity;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;

namespace Servora.Api.Infrastructure.Activity;

public sealed class ActivityLogger : IActivityLogger
{
    private readonly AppDbContext _context;
    private readonly IShopContext _shopContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ActivityLogger(AppDbContext context, IShopContext shopContext, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _shopContext = shopContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(
        string entityType,
        int entityId,
        string actionType,
        string title,
        string? description = null,
        string? metadataJson = null,
        CancellationToken cancellationToken = default)
    {
        var userIdValue = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        int? userId = int.TryParse(userIdValue, out var parsedUserId) ? parsedUserId : null;

        _context.ActivityLogs.Add(new ActivityLog
        {
            ShopId = await _shopContext.GetCurrentShopIdAsync(cancellationToken),
            ActorUserId = userId,
            EntityType = entityType,
            EntityId = entityId,
            ActionType = actionType,
            Title = title,
            Description = description,
            MetadataJson = metadataJson
        });

        await _context.SaveChangesAsync(cancellationToken);
    }
}
