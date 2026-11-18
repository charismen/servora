using Servora.Api.Infrastructure.Data;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Infrastructure.ShopContext;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Servora.Api.Features.Customers;

public static class ToggleStar
{
    public record StarCommand(int Id) : IRequest<bool>;
    public record UnstarCommand(int Id) : IRequest<bool>;

    internal sealed class StarHandler : IRequestHandler<StarCommand, bool>
    {
        private readonly AppDbContext _context;
        private readonly IShopContext _shopContext;

        public StarHandler(AppDbContext context, IShopContext shopContext)
        {
            _context = context;
            _shopContext = shopContext;
        }

        public async Task<bool> Handle(StarCommand request, CancellationToken cancellationToken)
        {
            var shopId = await _shopContext.GetCurrentShopIdAsync(cancellationToken);
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Id == request.Id && c.ShopId == shopId && c.DeletedAt == null, cancellationToken);

            if (customer is null) return false;

            customer.IsStarred = true;

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }

    internal sealed class UnstarHandler : IRequestHandler<UnstarCommand, bool>
    {
        private readonly AppDbContext _context;
        private readonly IShopContext _shopContext;

        public UnstarHandler(AppDbContext context, IShopContext shopContext)
        {
            _context = context;
            _shopContext = shopContext;
        }

        public async Task<bool> Handle(UnstarCommand request, CancellationToken cancellationToken)
        {
            var shopId = await _shopContext.GetCurrentShopIdAsync(cancellationToken);
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Id == request.Id && c.ShopId == shopId && c.DeletedAt == null, cancellationToken);

            if (customer is null) return false;

            customer.IsStarred = false;

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/customers/{id}/star", async (int id, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var success = await mediator.Send(new StarCommand(id), cancellationToken);
            return success ? Results.NoContent() : Results.NotFound();
        })
        .WithName("StarCustomer")
        .WithOpenApi()
        .RequireAuthorization(ApplicationPermissions.CustomersWrite);

        app.MapPost("/customers/{id}/unstar", async (int id, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var success = await mediator.Send(new UnstarCommand(id), cancellationToken);
            return success ? Results.NoContent() : Results.NotFound();
        })
        .WithName("UnstarCustomer")
        .WithOpenApi()
        .RequireAuthorization(ApplicationPermissions.CustomersWrite);
    }
}
