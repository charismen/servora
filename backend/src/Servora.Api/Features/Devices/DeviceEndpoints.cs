using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Blueprints;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Infrastructure.Activity;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;

namespace Servora.Api.Features.Devices;

public static class DeviceEndpoints
{
    public sealed record DeviceDto(
        int Id,
        int CustomerId,
        string DeviceType,
        string Brand,
        string Model,
        string? SerialNumber,
        string? IdentifierLabel,
        string? Notes,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt,
        decimal? MeterReading = null,
        string? MeterUnit = null,
        int? SiteLocationId = null,
        string? SiteLocationLabel = null,
        string? CustomerName = null);

    public sealed record ListQuery(int CustomerId) : IRequest<List<DeviceDto>>;

    public sealed record UpsertCommand(
        int CustomerId,
        int? Id,
        string DeviceType,
        string Brand,
        string Model,
        string? SerialNumber,
        string? IdentifierLabel,
        string? Notes,
        decimal? MeterReading = null,
        string? MeterUnit = null,
        int? SiteLocationId = null,
        IReadOnlyList<BlueprintEndpoints.FieldValueInput>? CustomFields = null) : IRequest<IResult>;

    public sealed record UpsertRequest(
        string DeviceType,
        string Brand,
        string Model,
        string? SerialNumber,
        string? IdentifierLabel,
        string? Notes,
        decimal? MeterReading = null,
        string? MeterUnit = null,
        int? SiteLocationId = null,
        IReadOnlyList<BlueprintEndpoints.FieldValueInput>? CustomFields = null);

    public sealed record DeleteCommand(int CustomerId, int Id) : IRequest<IResult>;

    public sealed class UpsertValidator : AbstractValidator<UpsertCommand>
    {
        public UpsertValidator()
        {
            RuleFor(command => command.DeviceType).NotEmpty().MaximumLength(80);
            RuleFor(command => command.Brand).NotEmpty().MaximumLength(80);
            RuleFor(command => command.Model).NotEmpty().MaximumLength(120);
            RuleFor(command => command.SerialNumber).MaximumLength(120);
            RuleFor(command => command.IdentifierLabel).MaximumLength(150);
            RuleFor(command => command.MeterUnit).MaximumLength(30);
            RuleFor(command => command.Notes).MaximumLength(2000);
        }
    }

    internal sealed class ListHandler : IRequestHandler<ListQuery, List<DeviceDto>>
    {
        private readonly AppDbContext _context;
        private readonly IShopContext _shopContext;

        public ListHandler(AppDbContext context, IShopContext shopContext)
        {
            _context = context;
            _shopContext = shopContext;
        }

        public async Task<List<DeviceDto>> Handle(ListQuery request, CancellationToken cancellationToken)
        {
            var shopId = await _shopContext.GetCurrentShopIdAsync(cancellationToken);

            return await _context.Devices
                .AsNoTracking()
                .Where(device => device.ShopId == shopId && device.CustomerId == request.CustomerId && device.DeletedAt == null)
                .OrderByDescending(device => device.CreatedAt)
                .Select(device => new DeviceDto(
                    device.Id,
                    device.CustomerId,
                    device.DeviceType,
                    device.Brand,
                    device.Model,
                    device.SerialNumber,
                    device.IdentifierLabel,
                    device.Notes,
                    device.CreatedAt,
                    device.UpdatedAt,
                    device.MeterReading,
                    device.MeterUnit,
                    device.SiteLocationId,
                    device.SiteLocation != null ? device.SiteLocation.Label : null,
                    device.Customer.FullName))
                .ToListAsync(cancellationToken);
        }
    }

    internal sealed class UpsertHandler : IRequestHandler<UpsertCommand, IResult>
    {
        private readonly AppDbContext _context;
        private readonly IShopContext _shopContext;
        private readonly IActivityLogger _activityLogger;

        public UpsertHandler(AppDbContext context, IShopContext shopContext, IActivityLogger activityLogger)
        {
            _context = context;
            _shopContext = shopContext;
            _activityLogger = activityLogger;
        }

        public async Task<IResult> Handle(UpsertCommand request, CancellationToken cancellationToken)
        {
            var shopId = await _shopContext.GetCurrentShopIdAsync(cancellationToken);
            var customerExists = await _context.Customers
                .AnyAsync(customer => customer.Id == request.CustomerId && customer.ShopId == shopId && customer.DeletedAt == null, cancellationToken);

            if (!customerExists) return Results.NotFound();

            var device = request.Id is null
                ? new Device { ShopId = shopId, CustomerId = request.CustomerId }
                : await _context.Devices.FirstOrDefaultAsync(
                    item => item.Id == request.Id && item.CustomerId == request.CustomerId && item.ShopId == shopId && item.DeletedAt == null,
                    cancellationToken);

            if (device is null) return Results.NotFound();

            device.DeviceType = request.DeviceType.Trim();
            device.Brand = request.Brand.Trim();
            device.Model = request.Model.Trim();
            device.SerialNumber = string.IsNullOrWhiteSpace(request.SerialNumber) ? null : request.SerialNumber.Trim();
            device.IdentifierLabel = string.IsNullOrWhiteSpace(request.IdentifierLabel) ? null : request.IdentifierLabel.Trim();
            device.MeterReading = request.MeterReading;
            device.MeterUnit = string.IsNullOrWhiteSpace(request.MeterUnit) ? null : request.MeterUnit.Trim();
            device.SiteLocationId = request.SiteLocationId;
            device.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

            if (request.Id is null)
            {
                _context.Devices.Add(device);
            }

            await _context.SaveChangesAsync(cancellationToken);

            if (request.CustomFields is { Count: > 0 })
            {
                var validFieldIds = await _context.AssetFieldDefinitions
                    .Where(f => f.ShopId == shopId && f.Scope == CustomFieldScope.Asset)
                    .Select(f => f.Id)
                    .ToHashSetAsync(cancellationToken);

                var existingValues = await _context.AssetFieldValues
                    .Where(v => v.ShopId == shopId && v.ServiceAssetId == device.Id)
                    .ToDictionaryAsync(v => v.FieldDefinitionId, cancellationToken);

                foreach (var field in request.CustomFields)
                {
                    if (!validFieldIds.Contains(field.FieldDefinitionId)) continue;
                    var trimmed = string.IsNullOrWhiteSpace(field.Value) ? null : field.Value.Trim();

                    if (existingValues.TryGetValue(field.FieldDefinitionId, out var row))
                    {
                        row.Value = trimmed;
                    }
                    else if (trimmed is not null)
                    {
                        _context.AssetFieldValues.Add(new AssetFieldValue
                        {
                            ShopId = shopId,
                            ServiceAssetId = device.Id,
                            FieldDefinitionId = field.FieldDefinitionId,
                            Value = trimmed
                        });
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);
            }

            if (request.Id is null)
            {
                await _activityLogger.LogAsync(
                    "ServiceAsset",
                    device.Id,
                    "ServiceAssetCreated",
                    $"Service asset created: {device.Brand} {device.Model}",
                    cancellationToken: cancellationToken);
            }

            return request.Id is null
                ? Results.Created($"/api/v1/customers/{request.CustomerId}/devices/{device.Id}", new { device.Id })
                : Results.NoContent();
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
            var device = await _context.Devices.FirstOrDefaultAsync(
                item => item.Id == request.Id && item.CustomerId == request.CustomerId && item.ShopId == shopId && item.DeletedAt == null,
                cancellationToken);

            if (device is null) return Results.NotFound();

            device.DeletedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        }
    }

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        async Task<IResult> ListAllAssetsAsync(int? customerId, string? search, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
        {
            var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
            var query = context.Devices
                .AsNoTracking()
                .Where(device => device.ShopId == shopId && device.DeletedAt == null);

            if (customerId.HasValue)
            {
                query = query.Where(device => device.CustomerId == customerId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(device =>
                    device.Brand.Contains(term) ||
                    device.Model.Contains(term) ||
                    device.DeviceType.Contains(term) ||
                    (device.SerialNumber != null && device.SerialNumber.Contains(term)) ||
                    (device.IdentifierLabel != null && device.IdentifierLabel.Contains(term)) ||
                    device.Customer.FullName.Contains(term));
            }

            var devices = await query
                .OrderByDescending(device => device.CreatedAt)
                .Select(device => new DeviceDto(
                    device.Id,
                    device.CustomerId,
                    device.DeviceType,
                    device.Brand,
                    device.Model,
                    device.SerialNumber,
                    device.IdentifierLabel,
                    device.Notes,
                    device.CreatedAt,
                    device.UpdatedAt,
                    device.MeterReading,
                    device.MeterUnit,
                    device.SiteLocationId,
                    device.SiteLocation != null ? device.SiteLocation.Label : null,
                    device.Customer.FullName))
                .ToListAsync(cancellationToken);

            return Results.Ok(devices);
        }

        app.MapGet("/devices", ListAllAssetsAsync)
            .WithName("GetShopDevices")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.DevicesRead);

        app.MapGet("/service-assets", ListAllAssetsAsync)
            .WithName("GetWorkspaceServiceAssets")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.DevicesRead);

        app.MapGet("/customers/{customerId:int}/devices", async (int customerId, IMediator mediator, CancellationToken cancellationToken) =>
            Results.Ok(await mediator.Send(new ListQuery(customerId), cancellationToken)))
            .WithName("GetCustomerDevices")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.DevicesRead);

        app.MapPost("/customers/{customerId:int}/devices", async (
            int customerId,
            [FromBody] UpsertRequest request,
            IMediator mediator,
            IValidator<UpsertCommand> validator,
            CancellationToken cancellationToken) =>
        {
            var commandWithRoute = new UpsertCommand(
                customerId,
                null,
                request.DeviceType,
                request.Brand,
                request.Model,
                request.SerialNumber,
                request.IdentifierLabel,
                request.Notes,
                request.MeterReading,
                request.MeterUnit,
                request.SiteLocationId,
                request.CustomFields);
            var validation = await validator.ValidateAsync(commandWithRoute, cancellationToken);
            return validation.IsValid
                ? await mediator.Send(commandWithRoute, cancellationToken)
                : Results.ValidationProblem(validation.ToDictionary());
        })
            .WithName("CreateCustomerDevice")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.DevicesWrite);

        app.MapPut("/customers/{customerId:int}/devices/{id:int}", async (
            int customerId,
            int id,
            [FromBody] UpsertRequest request,
            IMediator mediator,
            IValidator<UpsertCommand> validator,
            CancellationToken cancellationToken) =>
        {
            var commandWithRoute = new UpsertCommand(
                customerId,
                id,
                request.DeviceType,
                request.Brand,
                request.Model,
                request.SerialNumber,
                request.IdentifierLabel,
                request.Notes,
                request.MeterReading,
                request.MeterUnit,
                request.SiteLocationId,
                request.CustomFields);
            var validation = await validator.ValidateAsync(commandWithRoute, cancellationToken);
            return validation.IsValid
                ? await mediator.Send(commandWithRoute, cancellationToken)
                : Results.ValidationProblem(validation.ToDictionary());
        })
            .WithName("UpdateCustomerDevice")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.DevicesWrite);

        app.MapDelete("/customers/{customerId:int}/devices/{id:int}", async (
            int customerId,
            int id,
            IMediator mediator,
            CancellationToken cancellationToken) =>
            await mediator.Send(new DeleteCommand(customerId, id), cancellationToken))
            .WithName("DeleteCustomerDevice")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.DevicesDelete);
    }
}
