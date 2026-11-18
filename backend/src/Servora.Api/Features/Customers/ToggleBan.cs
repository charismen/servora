using Servora.Api.Infrastructure.Data;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Infrastructure.ShopContext;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace Servora.Api.Features.Customers;

public static class ToggleBan
{
    public record BanCommand(int Id, string? Reason) : IRequest<bool>;
    public record UnbanCommand(int Id) : IRequest<bool>;

    internal sealed class BanHandler : IRequestHandler<BanCommand, bool>
    {
        private readonly AppDbContext _context;
        private readonly IShopContext _shopContext;

        public BanHandler(AppDbContext context, IShopContext shopContext)
        {
            _context = context;
            _shopContext = shopContext;
        }

        public async Task<bool> Handle(BanCommand request, CancellationToken cancellationToken)
        {
            var shopId = await _shopContext.GetCurrentShopIdAsync(cancellationToken);
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Id == request.Id && c.ShopId == shopId && c.DeletedAt == null, cancellationToken);

            if (customer is null) return false;

            customer.IsBanned = true;
            customer.BanReason = request.Reason;

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }

    internal sealed class UnbanHandler : IRequestHandler<UnbanCommand, bool>
    {
        private readonly AppDbContext _context;
        private readonly IShopContext _shopContext;

        public UnbanHandler(AppDbContext context, IShopContext shopContext)
        {
            _context = context;
            _shopContext = shopContext;
        }

        public async Task<bool> Handle(UnbanCommand request, CancellationToken cancellationToken)
        {
            var shopId = await _shopContext.GetCurrentShopIdAsync(cancellationToken);
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Id == request.Id && c.ShopId == shopId && c.DeletedAt == null, cancellationToken);

            if (customer is null) return false;

            customer.IsBanned = false;
            customer.BanReason = null;

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/customers/{id}/ban", async (int id, [FromBody] BanModel model, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var success = await mediator.Send(new BanCommand(id, model.Reason), cancellationToken);
            return success ? Results.NoContent() : Results.NotFound();
        })
        .WithName("BanCustomer")
        .WithOpenApi()
        .RequireAuthorization(ApplicationPermissions.CustomersWrite);

        app.MapPost("/customers/{id}/unban", async (int id, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var success = await mediator.Send(new UnbanCommand(id), cancellationToken);
            return success ? Results.NoContent() : Results.NotFound();
        })
        .WithName("UnbanCustomer")
        .WithOpenApi()
        .RequireAuthorization(ApplicationPermissions.CustomersWrite);
    }

    public record BanModel(string? Reason);
}
