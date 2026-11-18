using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;

namespace Servora.Api.Features.Customers;

public static class CustomerLocationEndpoints
{
    public sealed record CustomerLocationDto(
        int Id,
        int? CustomerId,
        string? CustomerName,
        string Label,
        string Address,
        string? City,
        string? PostalCode,
        string? ContactName,
        string? ContactPhone,
        decimal? Latitude,
        decimal? Longitude,
        string? Notes,
        bool IsDefault,
        DateTimeOffset CreatedAt);

    public sealed record UpsertCustomerLocationRequest(
        int? CustomerId,
        string Label,
        string Address,
        string? City,
        string? PostalCode,
        string? ContactName,
        string? ContactPhone,
        decimal? Latitude,
        decimal? Longitude,
        string? Notes,
        bool IsDefault = false);

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/locations", ListLocationsAsync)
            .WithName("GetLocations")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.CustomersRead);

        app.MapPost("/locations", CreateLocationAsync)
            .WithName("CreateLocation")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.CustomersWrite);

        app.MapPut("/locations/{id:int}", UpdateLocationAsync)
            .WithName("UpdateLocation")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.CustomersWrite);

        app.MapDelete("/locations/{id:int}", DeleteLocationAsync)
            .WithName("DeleteLocation")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.CustomersDelete);
    }

    private static async Task<IResult> ListLocationsAsync(
        int? customerId,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var query = context.CustomerLocations
            .AsNoTracking()
            .Where(l => l.ShopId == workspaceId);

        if (customerId.HasValue)
        {
            query = query.Where(l => l.CustomerId == customerId.Value || l.CustomerId == null);
        }

        var items = await query
            .OrderByDescending(l => l.IsDefault)
            .ThenBy(l => l.Label)
            .Select(l => new CustomerLocationDto(
                l.Id,
                l.CustomerId,
                l.Customer != null ? l.Customer.FullName : null,
                l.Label,
                l.Address,
                l.City,
                l.PostalCode,
                l.ContactName,
                l.ContactPhone,
                l.Latitude,
                l.Longitude,
                l.Notes,
                l.IsDefault,
                l.CreatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(items);
    }

    private static async Task<IResult> CreateLocationAsync(
        [FromBody] UpsertCustomerLocationRequest request,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Label) || string.IsNullOrWhiteSpace(request.Address))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["label"] = ["Location label and address are required."]
            });
        }

        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        if (request.CustomerId.HasValue)
        {
            var exists = await context.Customers.AnyAsync(c => c.Id == request.CustomerId.Value && c.ShopId == workspaceId && c.DeletedAt == null, cancellationToken);
            if (!exists) return Results.NotFound();
        }

        var entity = new CustomerLocation
        {
            ShopId = workspaceId,
            CustomerId = request.CustomerId,
            Label = request.Label.Trim(),
            Address = request.Address.Trim(),
            City = TrimOrNull(request.City),
            PostalCode = TrimOrNull(request.PostalCode),
            ContactName = TrimOrNull(request.ContactName),
            ContactPhone = TrimOrNull(request.ContactPhone),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Notes = TrimOrNull(request.Notes),
            IsDefault = request.IsDefault
        };

        context.CustomerLocations.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/v1/locations/{entity.Id}", new { entity.Id });
    }

    private static async Task<IResult> UpdateLocationAsync(
        int id,
        [FromBody] UpsertCustomerLocationRequest request,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var entity = await context.CustomerLocations
            .FirstOrDefaultAsync(l => l.Id == id && l.ShopId == workspaceId, cancellationToken);

        if (entity is null) return Results.NotFound();

        entity.CustomerId = request.CustomerId;
        entity.Label = request.Label.Trim();
        entity.Address = request.Address.Trim();
        entity.City = TrimOrNull(request.City);
        entity.PostalCode = TrimOrNull(request.PostalCode);
        entity.ContactName = TrimOrNull(request.ContactName);
        entity.ContactPhone = TrimOrNull(request.ContactPhone);
        entity.Latitude = request.Latitude;
        entity.Longitude = request.Longitude;
        entity.Notes = TrimOrNull(request.Notes);
        entity.IsDefault = request.IsDefault;

        await context.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteLocationAsync(
        int id,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var workspaceId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var entity = await context.CustomerLocations
            .FirstOrDefaultAsync(l => l.Id == id && l.ShopId == workspaceId, cancellationToken);

        if (entity is null) return Results.NotFound();

        context.CustomerLocations.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
