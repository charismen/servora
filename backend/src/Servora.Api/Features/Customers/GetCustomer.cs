using Servora.Api.Infrastructure.Data;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Infrastructure.ShopContext;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Servora.Api.Features.Customers;

public static class GetCustomer
{
    public record Query(int Id) : IRequest<Result?>;

    public record Result(
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
        string RequesterType = "Customer",
        string? CompanyName = null,
        string? DepartmentName = null,
        string? DepartmentCode = null,
        string? CostCenter = null,
        string? EmployeeId = null);

    internal sealed class Handler : IRequestHandler<Query, Result?>
    {
        private readonly AppDbContext _context;
        private readonly IShopContext _shopContext;

        public Handler(AppDbContext context, IShopContext shopContext)
        {
            _context = context;
            _shopContext = shopContext;
        }

        public async Task<Result?> Handle(Query request, CancellationToken cancellationToken)
        {
            var shopId = await _shopContext.GetCurrentShopIdAsync(cancellationToken);
            var customer = await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.Id && c.ShopId == shopId && c.DeletedAt == null, cancellationToken);

            if (customer is null) return null;

            return new Result(
                customer.Id,
                customer.FullName,
                customer.PhoneNumber,
                customer.Email,
                customer.Address,
                customer.IsBanned,
                customer.BanReason,
                customer.IsStarred,
                customer.Notes,
                customer.CreatedAt,
                customer.UpdatedAt,
                customer.RequesterType.ToString(),
                customer.CompanyName,
                customer.DepartmentName,
                customer.DepartmentCode,
                customer.CostCenter,
                customer.EmployeeId);
        }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/customers/{id}", async (int id, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(new Query(id), cancellationToken);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetCustomer")
        .WithOpenApi()
        .RequireAuthorization(ApplicationPermissions.CustomersRead);
    }
}
