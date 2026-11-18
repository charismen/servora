using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Servora.Api.Features.ServiceStatuses;

public static class ServiceStatusEndpoints
{
    public record Dto(int Id, string Name, string Color, int SortOrder, bool IsInitial, bool IsFinal, bool IsCancelled, bool IsBlocked);

    public record UpsertCommand(
        [property: System.Text.Json.Serialization.JsonIgnore] int? Id,
        string Name,
        string Color,
        int SortOrder,
        bool IsInitial,
        bool IsFinal,
        bool IsCancelled,
        bool IsBlocked) : IRequest<IResult>;

    public record DeleteCommand(int Id) : IRequest<IResult>;
    public record ReorderCommand(List<int> Ids) : IRequest<IResult>;

    public class UpsertValidator : AbstractValidator<UpsertCommand>
    {
        public UpsertValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
            RuleFor(x => x.Color).NotEmpty().MaximumLength(20);
            RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        }
    }

    internal sealed class ListHandler : IRequestHandler<ListQuery, List<Dto>>
    {
        private readonly AppDbContext _context;
        private readonly IShopContext _shopContext;
        public ListHandler(AppDbContext context, IShopContext shopContext)
        {
            _context = context;
            _shopContext = shopContext;
        }

        public async Task<List<Dto>> Handle(ListQuery request, CancellationToken cancellationToken)
        {
            var shopId = await _shopContext.GetCurrentShopIdAsync(cancellationToken);
            return await _context.ServiceStatuses
                .AsNoTracking()
                .Where(status => status.ShopId == shopId)
                .OrderBy(status => status.SortOrder)
                .ThenBy(status => status.Id)
                .Select(status => new Dto(status.Id, status.Name, status.Color, status.SortOrder, status.IsInitial, status.IsFinal, status.IsCancelled, status.IsBlocked))
                .ToListAsync(cancellationToken);
        }
    }

    public record ListQuery : IRequest<List<Dto>>;

    internal sealed class UpsertHandler : IRequestHandler<UpsertCommand, IResult>
    {
        private readonly AppDbContext _context;
        private readonly IShopContext _shopContext;
        public UpsertHandler(AppDbContext context, IShopContext shopContext)
        {
            _context = context;
            _shopContext = shopContext;
        }

        public async Task<IResult> Handle(UpsertCommand request, CancellationToken cancellationToken)
        {
            var shopId = await _shopContext.GetCurrentShopIdAsync(cancellationToken);
            var status = request.Id is null
                ? new ServiceStatus { ShopId = shopId }
                : await _context.ServiceStatuses.FirstOrDefaultAsync(x => x.Id == request.Id && x.ShopId == shopId, cancellationToken);

            if (status is null) return Results.NotFound();
            var hasStatuses = await _context.ServiceStatuses.AnyAsync(x => x.ShopId == shopId, cancellationToken);
            if (!request.IsInitial && status.IsInitial && await _context.ServiceStatuses.CountAsync(x => x.ShopId == shopId && x.IsInitial, cancellationToken) == 1)
            {
                return Results.BadRequest("One initial status is required.");
            }

            status.Name = request.Name.Trim();
            status.Color = request.Color.Trim();
            status.SortOrder = request.SortOrder;
            status.IsInitial = request.IsInitial || !hasStatuses;
            status.IsFinal = request.IsFinal;
            status.IsCancelled = request.IsCancelled;
            status.IsBlocked = request.IsBlocked;

            if (request.Id is null) _context.ServiceStatuses.Add(status);

            if (status.IsInitial)
            {
                await _context.ServiceStatuses
                    .Where(x => x.ShopId == shopId && x.Id != status.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsInitial, false), cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
            return request.Id is null ? Results.Created($"/api/v1/service-statuses/{status.Id}", new { status.Id }) : Results.NoContent();
        }
    }

    internal sealed class DeleteHandler : IRequestHandler<DeleteCommand, IResult>
    {
        private readonly AppDbContext _context;
        private readonly IShopContext _shopContext;
        public DeleteHandler(AppDbContext context, IShopContext shopContext)
        {
            _context = context;
            _shopContext = shopContext;
        }

        public async Task<IResult> Handle(DeleteCommand request, CancellationToken cancellationToken)
        {
            var shopId = await _shopContext.GetCurrentShopIdAsync(cancellationToken);
            var status = await _context.ServiceStatuses.FirstOrDefaultAsync(x => x.Id == request.Id && x.ShopId == shopId, cancellationToken);
            if (status is null) return Results.NotFound();
            if (status.IsInitial) return Results.BadRequest("Initial status cannot be deleted.");
            if (await IsUsedByTicketAsync(shopId, request.Id, cancellationToken)) return Results.BadRequest("Status is used by service tickets.");

            _context.ServiceStatuses.Remove(status);
            await _context.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        }

        private async Task<bool> IsUsedByTicketAsync(int shopId, int statusId, CancellationToken cancellationToken)
        {
            var connection = _context.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = DATABASE() AND table_name = 'service_tickets'
                """;
            if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
            var tableCount = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            if (tableCount == 0) return false;

            command.CommandText = "SELECT COUNT(*) FROM service_tickets WHERE shop_id = @shopId AND status_id = @statusId";
            var shopParameter = command.CreateParameter();
            shopParameter.ParameterName = "@shopId";
            shopParameter.Value = shopId;
            command.Parameters.Add(shopParameter);
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@statusId";
            parameter.Value = statusId;
            command.Parameters.Add(parameter);
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
        }
    }

    internal sealed class ReorderHandler : IRequestHandler<ReorderCommand, IResult>
    {
        private readonly AppDbContext _context;
        private readonly IShopContext _shopContext;
        public ReorderHandler(AppDbContext context, IShopContext shopContext)
        {
            _context = context;
            _shopContext = shopContext;
        }

        public async Task<IResult> Handle(ReorderCommand request, CancellationToken cancellationToken)
        {
            var shopId = await _shopContext.GetCurrentShopIdAsync(cancellationToken);
            var statuses = await _context.ServiceStatuses.Where(x => x.ShopId == shopId && request.Ids.Contains(x.Id)).ToListAsync(cancellationToken);
            for (var i = 0; i < request.Ids.Count; i++)
            {
                var status = statuses.FirstOrDefault(x => x.Id == request.Ids[i]);
                if (status is not null) status.SortOrder = i + 1;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/service-statuses", async (IMediator mediator, CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(new ListQuery(), cancellationToken)))
            .WithName("GetServiceStatuses")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsRead);

        app.MapPost("/service-statuses", async ([FromBody] UpsertCommand command, IMediator mediator, IValidator<UpsertCommand> validator, CancellationToken cancellationToken) =>
        {
            var validation = await validator.ValidateAsync(command, cancellationToken);
            return validation.IsValid ? await mediator.Send(command, cancellationToken) : Results.ValidationProblem(validation.ToDictionary());
        })
            .WithName("CreateServiceStatus")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPut("/service-statuses/{id:int}", async (int id, [FromBody] UpsertCommand command, IMediator mediator, IValidator<UpsertCommand> validator, CancellationToken cancellationToken) =>
        {
            var commandWithId = command with { Id = id };
            var validation = await validator.ValidateAsync(commandWithId, cancellationToken);
            return validation.IsValid ? await mediator.Send(commandWithId, cancellationToken) : Results.ValidationProblem(validation.ToDictionary());
        })
            .WithName("UpdateServiceStatus")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPost("/service-statuses/reorder", async ([FromBody] ReorderCommand command, IMediator mediator, CancellationToken cancellationToken) =>
            await mediator.Send(command, cancellationToken))
            .WithName("ReorderServiceStatuses")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapDelete("/service-statuses/{id:int}", async (int id, IMediator mediator, CancellationToken cancellationToken) =>
            await mediator.Send(new DeleteCommand(id), cancellationToken))
            .WithName("DeleteServiceStatus")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);
    }
}
