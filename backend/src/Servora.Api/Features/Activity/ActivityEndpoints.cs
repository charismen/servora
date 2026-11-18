using MediatR;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;

namespace Servora.Api.Features.Activity;

public static class ActivityEndpoints
{
    public sealed record ActivityDto(
        int Id,
        string EntityType,
        int EntityId,
        string ActionType,
        string Title,
        string? Description,
        DateTimeOffset CreatedAt);

    public sealed record ActivityPageDto(IReadOnlyCollection<ActivityDto> Items, int Page, int PageSize, int TotalItems, int TotalPages);
    public sealed record Query(int Page = 1, int PageSize = 20, string? Search = null, string? EntityType = null, string? ActionType = null, DateTimeOffset? From = null, DateTimeOffset? To = null) : IRequest<ActivityPageDto>;

    internal sealed class Handler : IRequestHandler<Query, ActivityPageDto>
    {
        private readonly AppDbContext _context;
        private readonly IShopContext _shopContext;

        public Handler(AppDbContext context, IShopContext shopContext)
        {
            _context = context;
            _shopContext = shopContext;
        }

        public async Task<ActivityPageDto> Handle(Query request, CancellationToken cancellationToken)
        {
            var shopId = await _shopContext.GetCurrentShopIdAsync(cancellationToken);
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);

            var query = _context.ActivityLogs
                .AsNoTracking()
                .Where(log => log.ShopId == shopId);

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var term = request.Search.Trim();
                query = query.Where(log => log.Title.Contains(term) || (log.Description != null && log.Description.Contains(term)));
            }
            if (!string.IsNullOrWhiteSpace(request.EntityType)) query = query.Where(log => log.EntityType == request.EntityType);
            if (!string.IsNullOrWhiteSpace(request.ActionType)) query = query.Where(log => log.ActionType == request.ActionType);
            if (request.From.HasValue) query = query.Where(log => log.CreatedAt >= request.From.Value);
            if (request.To.HasValue) query = query.Where(log => log.CreatedAt <= request.To.Value);

            var totalItems = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(log => log.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(log => new ActivityDto(log.Id, log.EntityType, log.EntityId, log.ActionType, log.Title, log.Description, log.CreatedAt))
                .ToListAsync(cancellationToken);

            return new ActivityPageDto(items, page, pageSize, totalItems, (int)Math.Ceiling(totalItems / (double)pageSize));
        }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/activity", async (int? limit, int? page, int? pageSize, string? search, string? entityType, string? actionType, DateTimeOffset? from, DateTimeOffset? to, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var size = pageSize ?? limit ?? 20;
            var result = await mediator.Send(new Query(page ?? 1, size, search, entityType, actionType, from, to), cancellationToken);
            return limit.HasValue && page is null && pageSize is null && search is null && entityType is null && actionType is null && from is null && to is null
                ? Results.Ok(result.Items)
                : Results.Ok(result);
        })
            .WithName("GetActivity")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ReportsRead);
    }
}
