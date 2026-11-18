using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.Activity;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Infrastructure.ShopContext;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Servora.Api.Features.Customers;

public static class DeleteCustomer
{
    public enum DeleteOutcome
    {
        Deleted,
        NotFound,
        HasRelatedRecords
    }

    public record Command(int Id) : IRequest<DeleteOutcome>;

    internal sealed class Handler : IRequestHandler<Command, DeleteOutcome>
    {
        private readonly AppDbContext _context;
        private readonly IShopContext _shopContext;
        private readonly IActivityLogger _activityLogger;

        public Handler(AppDbContext context, IShopContext shopContext, IActivityLogger activityLogger)
        {
            _context = context;
            _shopContext = shopContext;
            _activityLogger = activityLogger;
        }

        public async Task<DeleteOutcome> Handle(Command request, CancellationToken cancellationToken)
        {
            var shopId = await _shopContext.GetCurrentShopIdAsync(cancellationToken);
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Id == request.Id && c.ShopId == shopId && c.DeletedAt == null, cancellationToken);

            if (customer is null) return DeleteOutcome.NotFound;

            var hasActiveTickets = await _context.ServiceTickets
                .AnyAsync(t => t.ShopId == shopId && t.CustomerId == request.Id && t.DeletedAt == null, cancellationToken);
            if (hasActiveTickets) return DeleteOutcome.HasRelatedRecords;

            var activeDevices = await _context.Devices
                .Where(d => d.ShopId == shopId && d.CustomerId == request.Id && d.DeletedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var device in activeDevices)
            {
                device.DeletedAt = DateTimeOffset.UtcNow;
            }

            customer.DeletedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            await _activityLogger.LogAsync(
                "Customer",
                customer.Id,
                "CustomerDeleted",
                $"Customer deleted: {customer.FullName}",
                cancellationToken: cancellationToken);

            return DeleteOutcome.Deleted;
        }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("/customers/{id}", async (int id, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var outcome = await mediator.Send(new Command(id), cancellationToken);
            return outcome switch
            {
                DeleteOutcome.Deleted => Results.NoContent(),
                DeleteOutcome.HasRelatedRecords => Results.Conflict("Cannot delete a customer who has existing service tickets."),
                _ => Results.NotFound()
            };
        })
        .WithName("DeleteCustomer")
        .WithOpenApi()
        .RequireAuthorization(ApplicationPermissions.CustomersDelete);
    }
}
