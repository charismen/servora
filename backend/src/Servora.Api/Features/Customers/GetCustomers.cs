using Servora.Api.Infrastructure.Data;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Infrastructure.ShopContext;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace Servora.Api.Features.Customers;

public static class GetCustomers
{
    public record Query(
        string? Search,
        string? PhoneNumber,
        bool? IsBanned,
        bool? IsStarred,
        string? Sort,
        int Page = 1,
        int PageSize = 50,
        DateTimeOffset? From = null,
        DateTimeOffset? To = null,
        string? RequesterType = null) : IRequest<Result>;

    public record Result(List<CustomerDto> Items, int TotalCount, int Page, int PageSize);

    public record CustomerDto(
        int Id,
        string FullName,
        string PhoneNumber,
        string? Email,
        string? Address,
        bool IsBanned,
        string? BanReason,
        bool IsStarred,
        string? Notes,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt,
        int TotalTickets,
        string RequesterType = "Customer",
        string? CompanyName = null,
        string? DepartmentName = null,
        string? DepartmentCode = null,
        string? CostCenter = null,
        string? EmployeeId = null);

    internal sealed class Handler : IRequestHandler<Query, Result>
    {
        private readonly AppDbContext _context;
        private readonly IShopContext _shopContext;

        public Handler(AppDbContext context, IShopContext shopContext)
        {
            _context = context;
            _shopContext = shopContext;
        }

        public async Task<Result> Handle(Query request, CancellationToken cancellationToken)
        {
            var shopId = await _shopContext.GetCurrentShopIdAsync(cancellationToken);
            var query = _context.Customers.AsNoTracking().Where(customer => customer.ShopId == shopId && customer.DeletedAt == null);

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var searchPattern = request.Search.Trim();
                query = query.Where(c =>
                    c.FullName.Contains(searchPattern) ||
                    c.PhoneNumber.Contains(searchPattern) ||
                    (c.Email != null && c.Email.Contains(searchPattern)) ||
                    (c.CompanyName != null && c.CompanyName.Contains(searchPattern)) ||
                    (c.DepartmentName != null && c.DepartmentName.Contains(searchPattern)) ||
                    (c.DepartmentCode != null && c.DepartmentCode.Contains(searchPattern)));
            }

            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                var phoneNumber = request.PhoneNumber.Trim();
                query = query.Where(c => c.PhoneNumber.Contains(phoneNumber));
            }

            if (!string.IsNullOrWhiteSpace(request.RequesterType) && Enum.TryParse<RequesterType>(request.RequesterType, true, out var reqType))
            {
                query = query.Where(c => c.RequesterType == reqType);
            }

            if (request.IsBanned.HasValue)
            {
                query = query.Where(c => c.IsBanned == request.IsBanned.Value);
            }

            if (request.IsStarred.HasValue)
            {
                query = query.Where(c => c.IsStarred == request.IsStarred.Value);
            }

            if (request.From.HasValue)
            {
                query = query.Where(c => c.CreatedAt >= request.From.Value);
            }

            if (request.To.HasValue)
            {
                query = query.Where(c => c.CreatedAt <= request.To.Value);
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);

            var orderedQuery = request.Sort?.Trim().ToLowerInvariant() switch
            {
                "name-asc" => query.OrderBy(c => c.FullName).ThenByDescending(c => c.CreatedAt),
                "name-desc" => query.OrderByDescending(c => c.FullName).ThenByDescending(c => c.CreatedAt),
                "oldest" => query.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id),
                "tickets-desc" => query.OrderByDescending(c => _context.ServiceTickets.Count(t => t.CustomerId == c.Id && t.DeletedAt == null)).ThenBy(c => c.FullName),
                _ => query.OrderByDescending(c => c.IsStarred).ThenByDescending(c => c.CreatedAt)
            };

            var items = await orderedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CustomerDto(
                    c.Id,
                    c.FullName,
                    c.PhoneNumber,
                    c.Email,
                    c.Address,
                    c.IsBanned,
                    c.BanReason,
                    c.IsStarred,
                    c.Notes,
                    c.CreatedAt,
                    c.UpdatedAt,
                    _context.ServiceTickets.Count(t => t.CustomerId == c.Id && t.DeletedAt == null),
                    c.RequesterType.ToString(),
                    c.CompanyName,
                    c.DepartmentName,
                    c.DepartmentCode,
                    c.CostCenter,
                    c.EmployeeId))
                .ToListAsync(cancellationToken);

            return new Result(items, totalCount, page, pageSize);
        }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/customers", async (
            [FromQuery] string? search,
            [FromQuery] string? phoneNumber,
            [FromQuery] bool? isBanned,
            [FromQuery] bool? isStarred,
            [FromQuery] string? sort,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [FromQuery] string? requesterType,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var query = new Query(search, phoneNumber, isBanned, isStarred, sort, page ?? 1, pageSize ?? 50, from, to, requesterType);
            var result = await mediator.Send(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetCustomers")
        .WithOpenApi()
        .RequireAuthorization(ApplicationPermissions.CustomersRead);
    }
}
