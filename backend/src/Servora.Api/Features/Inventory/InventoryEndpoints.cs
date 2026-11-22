using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Features.Notifications;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Features.Settings;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Activity;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;

namespace Servora.Api.Features.Inventory;

public static class InventoryEndpoints
{
    public sealed record InventoryItemDto(
        int Id,
        int? CategoryId,
        string? CategoryName,
        string Sku,
        string Name,
        string? Description,
        string Unit,
        decimal? CostPrice,
        decimal SalePrice,
        string ItemType,
        string Condition,
        decimal? PurchasePrice,
        decimal? ShippingCost,
        decimal? ServiceFee,
        decimal? MarginPercent,
        decimal? MarginAmount,
        decimal? TaxPercent,
        decimal? TaxAmount,
        decimal? PriceBeforeTax,
        decimal? PriceAfterTax,
        decimal FixedSalePrice,
        string? Quality,
        int? WarrantyDays,
        string? SupplierName,
        string? BarcodeValue,
        string? LocationNote,
        DateTimeOffset? LastCountedAt,
        decimal OnHandQuantity,
        decimal ReservedQuantity,
        decimal AvailableQuantity,
        decimal ReorderLevel,
        bool IsActive,
        DateTimeOffset? UpdatedAt);
    public sealed record InventoryItemsPageDto(IReadOnlyCollection<InventoryItemDto> Items, int TotalCount, int Page, int PageSize);
    public sealed record InventoryItemDetailDto(InventoryItemDto Item, InventoryStockSummaryDto Stock, InventoryPricingBreakdownDto Pricing, IReadOnlyCollection<InventoryMovementDto> Movements, IReadOnlyCollection<InventoryLinkedTicketDto> LinkedTickets);
    public sealed record InventoryStockSummaryDto(decimal OnHandQuantity, decimal ReservedQuantity, decimal AvailableQuantity, decimal ReorderLevel, bool IsLowStock);
    public sealed record InventoryPricingBreakdownDto(decimal? PurchasePrice, decimal? ShippingCost, decimal? ServiceFee, decimal? MarginPercent, decimal? MarginAmount, decimal? TaxPercent, decimal? TaxAmount, decimal? PriceBeforeTax, decimal? PriceAfterTax, decimal FixedSalePrice);
    public sealed record InventoryLinkedTicketDto(int TicketId, string TicketNumber, decimal Quantity, decimal ConsumedQuantity, decimal ReleasedQuantity, decimal ReturnedQuantity, string Status, DateTimeOffset CreatedAt);

    public sealed record InventoryMovementDto(
        int Id,
        int InventoryItemId,
        string ItemName,
        string Type,
        decimal Quantity,
        decimal? UnitCost,
        int? ServiceTicketId,
        string? TicketNumber,
        string? Reason,
        DateTimeOffset CreatedAt);
    public sealed record InventoryCategoryDto(int Id, string Name, string? Description, bool IsActive);

    public sealed record TicketInventoryUsageDto(
        int Id,
        int InventoryItemId,
        string Sku,
        string ItemName,
        string Unit,
        decimal Quantity,
        decimal ReservedQuantity,
        decimal ConsumedQuantity,
        decimal ReleasedQuantity,
        decimal ReturnedQuantity,
        string Status,
        string? Notes,
        decimal UnitPrice = 0m);

    public sealed record UpsertItemRequest(
        int? CategoryId,
        string? Sku = null,
        string? Name = null,
        string? Description = null,
        string? Unit = null,
        decimal? CostPrice = null,
        decimal? SalePrice = null,
        decimal? ReorderLevel = null,
        bool? IsActive = null,
        string? ItemType = null,
        string? Condition = null,
        decimal? PurchasePrice = null,
        decimal? ShippingCost = null,
        decimal? ServiceFee = null,
        decimal? MarginPercent = null,
        decimal? TaxPercent = null,
        decimal? FixedSalePrice = null,
        string? Quality = null,
        int? WarrantyDays = null,
        string? SupplierName = null,
        string? BarcodeValue = null,
        string? LocationNote = null,
        DateTimeOffset? UpdatedAt = null);
    public sealed record UpsertCategoryRequest(string Name, string? Description, bool IsActive);
    public sealed record StockInRequest(decimal Quantity, decimal? UnitCost, string? Reason);
    public sealed record AdjustStockRequest(decimal QuantityDelta, string Reason);
    public sealed record ReserveItemRequest(int InventoryItemId, decimal Quantity, string? Notes);
    public sealed record QuantityRequest(decimal? Quantity, string? Reason);
    public sealed record DeleteItemResultDto(string Action);
    private sealed record InventoryPricingSettings(decimal DefaultTaxPercent, decimal RoundingIncrement, string DefaultUnit);

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/inventory/items", ListItemsAsync)
            .WithName("GetInventoryItems")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.InventoryRead);

        app.MapGet("/inventory/items/{id:int}", GetItemAsync)
            .WithName("GetInventoryItem")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.InventoryRead);

        app.MapGet("/inventory/categories", ListCategoriesAsync)
            .WithName("GetInventoryCategories")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.InventoryRead);

        app.MapPost("/inventory/categories", CreateCategoryAsync)
            .WithName("CreateInventoryCategory")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.InventoryWrite);

        app.MapPut("/inventory/categories/{id:int}", UpdateCategoryAsync)
            .WithName("UpdateInventoryCategory")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.InventoryWrite);

        app.MapDelete("/inventory/categories/{id:int}", DeleteCategoryAsync)
            .WithName("DeleteInventoryCategory")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.InventoryWrite);

        app.MapPost("/inventory/items", CreateItemAsync)
            .WithName("CreateInventoryItem")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.InventoryWrite);

        app.MapPut("/inventory/items/{id:int}", UpdateItemAsync)
            .WithName("UpdateInventoryItem")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.InventoryWrite);

        app.MapDelete("/inventory/items/{id:int}", DeleteItemAsync)
            .WithName("DeleteInventoryItem")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.InventoryDelete);

        app.MapPost("/inventory/items/{id:int}/stock-in", StockInAsync)
            .WithName("StockInInventoryItem")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.InventoryWrite);

        app.MapPost("/inventory/items/{id:int}/adjust-stock", AdjustStockAsync)
            .WithName("AdjustInventoryStock")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.InventoryWrite);

        app.MapGet("/inventory/movements", ListMovementsAsync)
            .WithName("GetInventoryMovements")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.InventoryRead);

        app.MapGet("/inventory/movements/export", ExportMovementsAsync)
            .WithName("ExportInventoryMovements")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.InventoryRead);

        app.MapGet("/service-tickets/{ticketId:int}/inventory", GetTicketInventoryAsync)
            .WithName("GetServiceTicketInventory")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.InventoryRead);

        app.MapPost("/service-tickets/{ticketId:int}/inventory/reserve", ReserveForTicketAsync)
            .WithName("ReserveInventoryForTicket")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.InventoryWrite);

        app.MapPost("/service-tickets/{ticketId:int}/inventory/{usageId:int}/consume", ConsumeForTicketAsync)
            .WithName("ConsumeTicketInventory")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.InventoryWrite);

        app.MapPost("/service-tickets/{ticketId:int}/inventory/{usageId:int}/release", ReleaseForTicketAsync)
            .WithName("ReleaseTicketInventory")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.InventoryWrite);

        app.MapPost("/service-tickets/{ticketId:int}/inventory/{usageId:int}/return", ReturnForTicketAsync)
            .WithName("ReturnTicketInventory")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.InventoryWrite);
    }

    private static async Task<IResult> ListItemsAsync(
        string? search,
        int? categoryId,
        InventoryItemType? itemType,
        InventoryCondition? condition,
        bool? isActive,
        int? page,
        int? pageSize,
        string? sort,
        AppDbContext context,
        IShopContext shopContext,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var canViewCost = await HasPermissionAsync(context, principal, shopId, ApplicationPermissions.InventoryCostRead, cancellationToken);
        var query = context.InventoryItems.AsNoTracking().Where(item => item.ShopId == shopId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.Sku.Contains(term) || item.Name.Contains(term) || (item.Category != null && item.Category.Name.Contains(term)));
        }
        if (categoryId.HasValue) query = query.Where(item => item.CategoryId == categoryId.Value);
        if (itemType.HasValue) query = query.Where(item => item.ItemType == itemType.Value);
        if (condition.HasValue) query = query.Where(item => item.Condition == condition.Value);
        if (isActive.HasValue) query = query.Where(item => item.IsActive == isActive.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var currentPage = Math.Max(1, page ?? 1);
        var currentPageSize = Math.Clamp(pageSize ?? 50, 1, 100);
        IQueryable<InventoryItem> orderedQuery = sort?.Trim().ToLowerInvariant() switch
        {
            "sku" => query.OrderBy(item => item.Sku).ThenBy(item => item.Name),
            "available-asc" => query.OrderBy(item => item.OnHandQuantity - item.ReservedQuantity).ThenBy(item => item.Name),
            "available-desc" => query.OrderByDescending(item => item.OnHandQuantity - item.ReservedQuantity).ThenBy(item => item.Name),
            _ => query.OrderBy(item => item.Name).ThenBy(item => item.Sku)
        };
        if (page.HasValue || pageSize.HasValue)
        {
            orderedQuery = orderedQuery.Skip((currentPage - 1) * currentPageSize).Take(currentPageSize);
        }

        var inventoryItems = await orderedQuery
            .Include(item => item.Category)
            .ToListAsync(cancellationToken);

        var items = inventoryItems.Select(item => ToItemDto(item, canViewCost)).ToList();

        if (page.HasValue || pageSize.HasValue) return Results.Ok(new InventoryItemsPageDto(items, totalCount, currentPage, currentPageSize));
        return Results.Ok(items);
    }

    private static async Task<IResult> GetItemAsync(int id, AppDbContext context, IShopContext shopContext, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var canViewCost = await HasPermissionAsync(context, principal, shopId, ApplicationPermissions.InventoryCostRead, cancellationToken);
        var item = await context.InventoryItems
            .AsNoTracking()
            .Include(item => item.Category)
            .FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId, cancellationToken);
        if (item is null) return Results.NotFound();

        var movementEntities = await BuildMovementQuery(context, shopId, id, null, null, null)
            .Take(50)
            .ToListAsync(cancellationToken);
        var movements = movementEntities.Select(ToMovementDto).ToList();
        var linkedTickets = await context.TicketInventoryUsages
            .AsNoTracking()
            .Include(usage => usage.ServiceTicket)
            .Where(usage => usage.ShopId == shopId && usage.InventoryItemId == id && usage.ServiceTicket.DeletedAt == null)
            .OrderByDescending(usage => usage.CreatedAt)
            .Take(50)
            .Select(usage => new InventoryLinkedTicketDto(
                usage.ServiceTicketId,
                usage.ServiceTicket.TicketNumber,
                usage.Quantity,
                usage.ConsumedQuantity,
                usage.ReleasedQuantity,
                usage.ReturnedQuantity,
                Status(usage.Quantity, usage.ConsumedQuantity, usage.ReleasedQuantity, usage.ReturnedQuantity),
                usage.CreatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(new InventoryItemDetailDto(
            ToItemDto(item, canViewCost),
            new InventoryStockSummaryDto(item.OnHandQuantity, item.ReservedQuantity, Available(item), item.ReorderLevel, Available(item) <= item.ReorderLevel),
            new InventoryPricingBreakdownDto(
                canViewCost ? item.PurchasePrice : null,
                canViewCost ? item.ShippingCost : null,
                canViewCost ? item.ServiceFee : null,
                canViewCost ? item.MarginPercent : null,
                canViewCost ? item.MarginAmount : null,
                canViewCost ? item.TaxPercent : null,
                canViewCost ? item.TaxAmount : null,
                canViewCost ? item.PriceBeforeTax : null,
                canViewCost ? item.PriceAfterTax : null,
                item.FixedSalePrice),
            movements,
            linkedTickets));
    }

    private static async Task<IResult> ListCategoriesAsync(AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var categories = await context.InventoryCategories
            .AsNoTracking()
            .Where(category => category.ShopId == shopId)
            .OrderBy(category => category.Name)
            .Select(category => new InventoryCategoryDto(category.Id, category.Name, category.Description, category.IsActive))
            .ToListAsync(cancellationToken);
        return Results.Ok(categories);
    }

    private static async Task<IResult> CreateCategoryAsync([FromBody] UpsertCategoryRequest request, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest("Category name is required.");
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var name = request.Name.Trim();
        if (await context.InventoryCategories.AnyAsync(category => category.ShopId == shopId && category.Name == name, cancellationToken)) return Results.BadRequest("Category already exists.");

        var category = new InventoryCategory { ShopId = shopId, Name = name, Description = TrimOrNull(request.Description), IsActive = request.IsActive };
        context.InventoryCategories.Add(category);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/v1/inventory/categories/{category.Id}", new { category.Id });
    }

    private static async Task<IResult> UpdateCategoryAsync(int id, [FromBody] UpsertCategoryRequest request, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest("Category name is required.");
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var category = await context.InventoryCategories.FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId, cancellationToken);
        if (category is null) return Results.NotFound();

        var name = request.Name.Trim();
        if (await context.InventoryCategories.AnyAsync(item => item.Id != id && item.ShopId == shopId && item.Name == name, cancellationToken)) return Results.BadRequest("Category already exists.");

        category.Name = name;
        category.Description = TrimOrNull(request.Description);
        category.IsActive = request.IsActive;
        await context.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteCategoryAsync(int id, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var category = await context.InventoryCategories.FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId, cancellationToken);
        if (category is null) return Results.NotFound();

        if (await context.InventoryItems.AnyAsync(item => item.CategoryId == id && item.ShopId == shopId, cancellationToken))
        {
            category.IsActive = false;
        }
        else
        {
            context.InventoryCategories.Remove(category);
        }
        await context.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> CreateItemAsync([FromBody] UpsertItemRequest request, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var settings = await GetPricingSettingsAsync(context, shopId, cancellationToken);
        var validation = ValidateItem(request, settings);
        if (validation is not null) return validation;

        if (await context.InventoryItems.AnyAsync(item => item.ShopId == shopId && item.Sku == request.Sku!.Trim(), cancellationToken))
        {
            return Results.BadRequest("SKU already exists.");
        }

        if (request.CategoryId.HasValue && !await context.InventoryCategories.AnyAsync(category => category.Id == request.CategoryId && category.ShopId == shopId, cancellationToken))
        {
            return Results.BadRequest("Category was not found.");
        }

        var item = new InventoryItem { ShopId = shopId };
        ApplyItem(item, request, settings);
        context.InventoryItems.Add(item);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/v1/inventory/items/{item.Id}", new { item.Id });
    }

    private static async Task<IResult> UpdateItemAsync(int id, [FromBody] UpsertItemRequest request, AppDbContext context, IShopContext shopContext, IActivityLogger activityLogger, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var settings = await GetPricingSettingsAsync(context, shopId, cancellationToken);
        var validation = ValidateItem(request, settings);
        if (validation is not null) return validation;

        var item = await context.InventoryItems.FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId, cancellationToken);
        if (item is null) return Results.NotFound();
        if (request.UpdatedAt.HasValue && item.UpdatedAt.HasValue && request.UpdatedAt.Value != item.UpdatedAt.Value)
        {
            return Results.Problem(
                detail: "Inventory item was updated by another user. Reload before saving again.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var sku = request.Sku!.Trim();
        if (await context.InventoryItems.AnyAsync(other => other.Id != id && other.ShopId == shopId && other.Sku == sku, cancellationToken))
        {
            return Results.BadRequest("SKU already exists.");
        }

        if (request.CategoryId.HasValue && !await context.InventoryCategories.AnyAsync(category => category.Id == request.CategoryId && category.ShopId == shopId, cancellationToken))
        {
            return Results.BadRequest("Category was not found.");
        }

        ApplyItem(item, request, settings);
        await context.SaveChangesAsync(cancellationToken);
        await activityLogger.LogAsync("InventoryItem", item.Id, "InventoryUpdated", $"Inventory updated: {item.Name}", item.Sku, cancellationToken: cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteItemAsync(int id, AppDbContext context, IShopContext shopContext, IActivityLogger activityLogger, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var item = await context.InventoryItems.FirstOrDefaultAsync(existing => existing.Id == id && existing.ShopId == shopId, cancellationToken);
        if (item is null) return Results.NotFound();

        if (item.ReservedQuantity > 0)
        {
            return Results.BadRequest("Cannot delete or deactivate an item with active ticket reservations.");
        }

        var hasHistory = await context.InventoryMovements.AnyAsync(movement => movement.ShopId == shopId && movement.InventoryItemId == id, cancellationToken)
            || await context.TicketInventoryUsages.AnyAsync(usage => usage.ShopId == shopId && usage.InventoryItemId == id, cancellationToken);

        if (hasHistory)
        {
            if (item.IsActive)
            {
                item.IsActive = false;
                await context.SaveChangesAsync(cancellationToken);
                await activityLogger.LogAsync("InventoryItem", item.Id, "InventoryDeactivated", $"Inventory deactivated: {item.Name}", item.Sku, cancellationToken: cancellationToken);
            }
            return Results.Ok(new DeleteItemResultDto("deactivated"));
        }

        context.InventoryItems.Remove(item);
        await context.SaveChangesAsync(cancellationToken);
        await activityLogger.LogAsync("InventoryItem", id, "InventoryDeleted", $"Inventory deleted: {item.Name}", item.Sku, cancellationToken: cancellationToken);
        return Results.Ok(new DeleteItemResultDto("deleted"));
    }

    private static async Task<IResult> StockInAsync(int id, [FromBody] StockInRequest request, AppDbContext context, IShopContext shopContext, IActivityLogger activityLogger, CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0) return Results.BadRequest("Quantity must be greater than zero.");
        if (request.UnitCost is < 0) return Results.BadRequest("Unit cost cannot be negative.");

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
            var item = await context.InventoryItems.FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId, cancellationToken);
            if (item is null) return Results.NotFound();

            item.OnHandQuantity += request.Quantity;
            item.StockVersion++;
            context.InventoryMovements.Add(NewMovement(shopId, item.Id, InventoryMovementType.StockIn, request.Quantity, request.UnitCost, request.Reason));
            await context.SaveChangesAsync(cancellationToken);
            await activityLogger.LogAsync("InventoryItem", item.Id, "InventoryStockIn", $"Stock added: {item.Name}", request.Reason, cancellationToken: cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.NoContent();
        }
        catch (DbUpdateConcurrencyException)
        {
            return StockConflict();
        }
    }

    private static async Task<IResult> AdjustStockAsync(
        int id,
        [FromBody] AdjustStockRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        NotificationDispatcherService notificationDispatcher,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (request.QuantityDelta == 0) return Results.BadRequest("Quantity delta cannot be zero.");
        if (string.IsNullOrWhiteSpace(request.Reason)) return Results.BadRequest("Reason is required.");

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
            var item = await context.InventoryItems.FirstOrDefaultAsync(item => item.Id == id && item.ShopId == shopId, cancellationToken);
            if (item is null) return Results.NotFound();
            if (item.OnHandQuantity + request.QuantityDelta < item.ReservedQuantity) return Results.BadRequest("Adjustment would reduce stock below reserved quantity.");

            item.OnHandQuantity += request.QuantityDelta;
            item.StockVersion++;
            context.InventoryMovements.Add(NewMovement(shopId, item.Id, InventoryMovementType.Adjustment, request.QuantityDelta, null, request.Reason));
            await context.SaveChangesAsync(cancellationToken);
            await activityLogger.LogAsync("InventoryItem", item.Id, "InventoryAdjusted", $"Inventory adjusted: {item.Name}", request.Reason, cancellationToken: cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var available = Available(item);
            if (request.QuantityDelta < 0 && item.ReorderLevel > 0 && available <= item.ReorderLevel)
            {
                var frontendBaseUrl = (configuration["PublicFrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
                await notificationDispatcher.PublishEventAsync(new NotificationEventEnvelope(
                    ShopId: shopId,
                    EventType: NotificationEventType.InventoryLowStock,
                    Title: $"Low stock alert: {item.Name} ({item.Sku})",
                    Message: $"{item.Name} ({item.Sku}) has {available:0.##} {item.Unit} available (reorder level: {item.ReorderLevel:0.##} {item.Unit}).",
                    DeepLinkUrl: $"{frontendBaseUrl}/app/inventory/{item.Id}",
                    ReferenceEntityType: "InventoryItem",
                    ReferenceEntityId: item.Id,
                    Data: new { itemId = item.Id, sku = item.Sku, name = item.Name, available, reorderLevel = item.ReorderLevel }),
                    cancellationToken);
            }

            return Results.NoContent();
        }
        catch (DbUpdateConcurrencyException)
        {
            return StockConflict();
        }
    }

    private static async Task<IResult> ListMovementsAsync(int? itemId, InventoryMovementType? type, string? from, string? to, int? page, int? pageSize, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var query = BuildMovementQuery(context, shopId, itemId, type, ParseDate(from), ParseDate(to)?.AddDays(1));
        var currentPage = Math.Max(1, page ?? 1);
        var currentPageSize = Math.Clamp(pageSize ?? 100, 1, 200);
        var movementEntities = await query.Skip((currentPage - 1) * currentPageSize).Take(currentPageSize).ToListAsync(cancellationToken);
        var movements = movementEntities.Select(ToMovementDto).ToList();

        return Results.Ok(movements);
    }

    private static async Task<IResult> ExportMovementsAsync(int? itemId, InventoryMovementType? type, string? from, string? to, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var movementEntities = await BuildMovementQuery(context, shopId, itemId, type, ParseDate(from), ParseDate(to)?.AddDays(1))
            .Take(5000)
            .ToListAsync(cancellationToken);
        var movements = movementEntities.Select(ToMovementDto).ToList();
        var csv = new StringBuilder();
        csv.AppendLine("Date,Item,Type,Quantity,Unit Cost,Ticket,Reason");
        foreach (var movement in movements)
        {
            csv.AppendLine(string.Join(',', Csv(movement.CreatedAt.ToString("u")), Csv(movement.ItemName), Csv(movement.Type), movement.Quantity.ToString(CultureInfo.InvariantCulture), movement.UnitCost?.ToString(CultureInfo.InvariantCulture) ?? "", Csv(movement.TicketNumber ?? ""), Csv(movement.Reason ?? "")));
        }

        return Results.File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"inventory-movements-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    private static async Task<IResult> GetTicketInventoryAsync(int ticketId, AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        if (!await context.ServiceTickets.AnyAsync(ticket => ticket.Id == ticketId && ticket.ShopId == shopId && ticket.DeletedAt == null, cancellationToken)) return Results.NotFound();
        return Results.Ok(await GetTicketUsageDtosAsync(context, shopId, ticketId, cancellationToken));
    }

    private static async Task<IResult> ReserveForTicketAsync(int ticketId, [FromBody] ReserveItemRequest request, AppDbContext context, IShopContext shopContext, IActivityLogger activityLogger, CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0) return Results.BadRequest("Quantity must be greater than zero.");

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
            if (!await context.ServiceTickets.AnyAsync(ticket => ticket.Id == ticketId && ticket.ShopId == shopId && ticket.DeletedAt == null, cancellationToken)) return Results.NotFound();
            var item = await context.InventoryItems.FirstOrDefaultAsync(item => item.Id == request.InventoryItemId && item.ShopId == shopId && item.IsActive, cancellationToken);
            if (item is null) return Results.BadRequest("Inventory item was not found.");
            if (Available(item) < request.Quantity) return Results.BadRequest("Insufficient available stock.");

            var usage = new TicketInventoryUsage
            {
                ShopId = shopId,
                ServiceTicketId = ticketId,
                InventoryItemId = item.Id,
                Quantity = request.Quantity,
                UnitPriceSnapshot = item.FixedSalePrice > 0 ? item.FixedSalePrice : item.SalePrice,
                Notes = TrimOrNull(request.Notes)
            };
            item.ReservedQuantity += request.Quantity;
            item.StockVersion++;
            context.TicketInventoryUsages.Add(usage);
            await context.SaveChangesAsync(cancellationToken);

            context.InventoryMovements.Add(NewMovement(shopId, item.Id, InventoryMovementType.Reserve, request.Quantity, null, request.Notes, ticketId, usage.Id));
            await context.SaveChangesAsync(cancellationToken);
            await activityLogger.LogAsync("ServiceTicket", ticketId, "InventoryReserved", $"Reserved {request.Quantity} {item.Unit} {item.Name}", request.Notes, cancellationToken: cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Created($"/api/v1/service-tickets/{ticketId}/inventory/{usage.Id}", new { usage.Id });
        }
        catch (DbUpdateConcurrencyException)
        {
            return StockConflict();
        }
    }

    private static async Task<IResult> ConsumeForTicketAsync(int ticketId, int usageId, [FromBody] QuantityRequest request, AppDbContext context, IShopContext shopContext, IActivityLogger activityLogger, NotificationDispatcherService notificationDispatcher, IConfiguration configuration, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        return await ApplyUsageActionAsync(ticketId, usageId, request, InventoryMovementType.Consume, context, shopContext, activityLogger, notificationDispatcher, configuration, principal, cancellationToken);
    }

    private static async Task<IResult> ReleaseForTicketAsync(int ticketId, int usageId, [FromBody] QuantityRequest request, AppDbContext context, IShopContext shopContext, IActivityLogger activityLogger, NotificationDispatcherService notificationDispatcher, IConfiguration configuration, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        return await ApplyUsageActionAsync(ticketId, usageId, request, InventoryMovementType.Release, context, shopContext, activityLogger, notificationDispatcher, configuration, principal, cancellationToken);
    }

    private static async Task<IResult> ReturnForTicketAsync(int ticketId, int usageId, [FromBody] QuantityRequest request, AppDbContext context, IShopContext shopContext, IActivityLogger activityLogger, NotificationDispatcherService notificationDispatcher, IConfiguration configuration, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        return await ApplyUsageActionAsync(ticketId, usageId, request, InventoryMovementType.Return, context, shopContext, activityLogger, notificationDispatcher, configuration, principal, cancellationToken);
    }

    public static async Task ReleaseOpenReservationsForTicketAsync(AppDbContext context, IActivityLogger activityLogger, int shopId, int ticketId, CancellationToken cancellationToken)
    {
        var usages = await context.TicketInventoryUsages
            .Include(usage => usage.InventoryItem)
            .Where(usage => usage.ShopId == shopId && usage.ServiceTicketId == ticketId)
            .ToListAsync(cancellationToken);

        foreach (var usage in usages)
        {
            var quantity = RemainingReserved(usage);
            if (quantity <= 0) continue;
            usage.ReleasedQuantity += quantity;
            usage.InventoryItem.ReservedQuantity -= quantity;
            usage.InventoryItem.StockVersion++;
            context.InventoryMovements.Add(NewMovement(shopId, usage.InventoryItemId, InventoryMovementType.Release, quantity, null, "Ticket cancelled.", ticketId, usage.Id));
            await activityLogger.LogAsync("ServiceTicket", ticketId, "InventoryReleased", $"Released {quantity} {usage.InventoryItem.Unit} {usage.InventoryItem.Name}", "Ticket cancelled.", cancellationToken: cancellationToken);
        }
    }

    private static async Task<IResult> ApplyUsageActionAsync(
        int ticketId,
        int usageId,
        QuantityRequest request,
        InventoryMovementType type,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        NotificationDispatcherService notificationDispatcher,
        IConfiguration configuration,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
            var usage = await context.TicketInventoryUsages
                .Include(item => item.InventoryItem)
                .FirstOrDefaultAsync(item => item.Id == usageId && item.ServiceTicketId == ticketId && item.ShopId == shopId, cancellationToken);
            if (usage is null) return Results.NotFound();

            var max = type == InventoryMovementType.Return ? usage.ConsumedQuantity - usage.ReturnedQuantity : RemainingReserved(usage);
            var quantity = request.Quantity ?? max;
            if (quantity <= 0 || quantity > max) return Results.BadRequest("Invalid quantity.");
            if (type is InventoryMovementType.Consume or InventoryMovementType.Release && usage.InventoryItem.ReservedQuantity < quantity) return Results.BadRequest("Reserved stock changed. Reload item and try again.");
            if (type == InventoryMovementType.Consume && usage.InventoryItem.OnHandQuantity < quantity) return Results.BadRequest("Insufficient on-hand stock.");

            switch (type)
            {
                case InventoryMovementType.Consume:
                    usage.ConsumedQuantity += quantity;
                    usage.InventoryItem.ReservedQuantity -= quantity;
                    usage.InventoryItem.OnHandQuantity -= quantity;
                    usage.UnitPriceSnapshot ??= usage.InventoryItem.FixedSalePrice > 0
                        ? usage.InventoryItem.FixedSalePrice
                        : usage.InventoryItem.SalePrice;
                    break;
                case InventoryMovementType.Release:
                    usage.ReleasedQuantity += quantity;
                    usage.InventoryItem.ReservedQuantity -= quantity;
                    break;
                case InventoryMovementType.Return:
                    usage.ReturnedQuantity += quantity;
                    usage.InventoryItem.OnHandQuantity += quantity;
                    usage.UnitPriceSnapshot ??= usage.InventoryItem.FixedSalePrice > 0
                        ? usage.InventoryItem.FixedSalePrice
                        : usage.InventoryItem.SalePrice;
                    break;
            }

            usage.InventoryItem.StockVersion++;
            var movementUnitCost = type is InventoryMovementType.Consume or InventoryMovementType.Return
                ? usage.InventoryItem.CostPrice
                : (decimal?)null;
            context.InventoryMovements.Add(NewMovement(shopId, usage.InventoryItemId, type, quantity, movementUnitCost, request.Reason, ticketId, usage.Id));
            await context.SaveChangesAsync(cancellationToken);

            if (type is InventoryMovementType.Consume or InventoryMovementType.Return)
            {
                var ticket = await context.ServiceTickets
                    .FirstOrDefaultAsync(item => item.Id == ticketId && item.ShopId == shopId && item.DeletedAt == null, cancellationToken);
                if (ticket is not null)
                {
                    var actorUserId = int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
                        ? parsedUserId
                        : (int?)null;
                    await TicketBillingService.RecalculateTicketBillingAsync(
                        context,
                        shopId,
                        ticket,
                        actorUserId,
                        $"Inventory {type}: {quantity} {usage.InventoryItem.Unit} {usage.InventoryItem.Name}",
                        forceFormulaWhenZero: type == InventoryMovementType.Return,
                        cancellationToken: cancellationToken);
                    await context.SaveChangesAsync(cancellationToken);
                }
            }

            await activityLogger.LogAsync("ServiceTicket", ticketId, $"Inventory{type}", $"{type} {quantity} {usage.InventoryItem.Unit} {usage.InventoryItem.Name}", request.Reason, cancellationToken: cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var availableAfter = Available(usage.InventoryItem);
            if (type == InventoryMovementType.Consume && usage.InventoryItem.ReorderLevel > 0 && availableAfter <= usage.InventoryItem.ReorderLevel)
            {
                var frontendBaseUrl = (configuration["PublicFrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
                await notificationDispatcher.PublishEventAsync(new NotificationEventEnvelope(
                    ShopId: shopId,
                    EventType: NotificationEventType.InventoryLowStock,
                    Title: $"Low stock alert: {usage.InventoryItem.Name} ({usage.InventoryItem.Sku})",
                    Message: $"{usage.InventoryItem.Name} ({usage.InventoryItem.Sku}) has {availableAfter:0.##} {usage.InventoryItem.Unit} available (reorder level: {usage.InventoryItem.ReorderLevel:0.##} {usage.InventoryItem.Unit}).",
                    DeepLinkUrl: $"{frontendBaseUrl}/app/inventory/{usage.InventoryItem.Id}",
                    ReferenceEntityType: "InventoryItem",
                    ReferenceEntityId: usage.InventoryItem.Id,
                    Data: new { itemId = usage.InventoryItem.Id, sku = usage.InventoryItem.Sku, name = usage.InventoryItem.Name, available = availableAfter, reorderLevel = usage.InventoryItem.ReorderLevel }),
                    cancellationToken);
            }

            return Results.NoContent();
        }
        catch (DbUpdateConcurrencyException)
        {
            return StockConflict();
        }
    }

    private static async Task<List<TicketInventoryUsageDto>> GetTicketUsageDtosAsync(AppDbContext context, int shopId, int ticketId, CancellationToken cancellationToken)
    {
        var usages = await context.TicketInventoryUsages
            .AsNoTracking()
            .Include(usage => usage.InventoryItem)
            .Where(usage => usage.ShopId == shopId && usage.ServiceTicketId == ticketId)
            .OrderByDescending(usage => usage.CreatedAt)
            .ToListAsync(cancellationToken);

        return usages
            .Select(usage => new TicketInventoryUsageDto(
                usage.Id,
                usage.InventoryItemId,
                usage.InventoryItem.Sku,
                usage.InventoryItem.Name,
                usage.InventoryItem.Unit,
                usage.Quantity,
                usage.Quantity - usage.ConsumedQuantity - usage.ReleasedQuantity,
                usage.ConsumedQuantity,
                usage.ReleasedQuantity,
                usage.ReturnedQuantity,
                Status(usage.Quantity, usage.ConsumedQuantity, usage.ReleasedQuantity, usage.ReturnedQuantity),
                usage.Notes,
                usage.UnitPriceSnapshot ?? (usage.InventoryItem.FixedSalePrice > 0 ? usage.InventoryItem.FixedSalePrice : usage.InventoryItem.SalePrice)))
            .ToList();
    }

    private static InventoryMovement NewMovement(int shopId, int itemId, InventoryMovementType type, decimal quantity, decimal? unitCost, string? reason, int? ticketId = null, int? usageId = null)
    {
        return new InventoryMovement
        {
            ShopId = shopId,
            InventoryItemId = itemId,
            ServiceTicketId = ticketId,
            TicketInventoryUsageId = usageId,
            Type = type,
            Quantity = quantity,
            UnitCost = unitCost,
            Reason = TrimOrNull(reason),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static InventoryItemDto ToItemDto(InventoryItem item, bool canViewCost) => new(
        item.Id,
        item.CategoryId,
        item.Category?.Name,
        item.Sku,
        item.Name,
        item.Description,
        item.Unit,
        canViewCost ? item.CostPrice : null,
        item.SalePrice,
        item.ItemType.ToString(),
        item.Condition.ToString(),
        canViewCost ? item.PurchasePrice : null,
        canViewCost ? item.ShippingCost : null,
        canViewCost ? item.ServiceFee : null,
        canViewCost ? item.MarginPercent : null,
        canViewCost ? item.MarginAmount : null,
        canViewCost ? item.TaxPercent : null,
        canViewCost ? item.TaxAmount : null,
        canViewCost ? item.PriceBeforeTax : null,
        canViewCost ? item.PriceAfterTax : null,
        item.FixedSalePrice,
        item.Quality,
        item.WarrantyDays,
        item.SupplierName,
        item.BarcodeValue,
        item.LocationNote,
        item.LastCountedAt,
        item.OnHandQuantity,
        item.ReservedQuantity,
        Available(item),
        item.ReorderLevel,
        item.IsActive,
        item.UpdatedAt);

    private static IQueryable<InventoryMovement> BuildMovementQuery(AppDbContext context, int shopId, int? itemId, InventoryMovementType? type, DateTimeOffset? from, DateTimeOffset? toExclusive)
    {
        var query = context.InventoryMovements.AsNoTracking().Where(movement => movement.ShopId == shopId);
        if (itemId.HasValue) query = query.Where(movement => movement.InventoryItemId == itemId.Value);
        if (type.HasValue) query = query.Where(movement => movement.Type == type.Value);
        if (from.HasValue) query = query.Where(movement => movement.CreatedAt >= from.Value);
        if (toExclusive.HasValue) query = query.Where(movement => movement.CreatedAt < toExclusive.Value);
        return query.Include(movement => movement.InventoryItem).Include(movement => movement.ServiceTicket).OrderByDescending(movement => movement.CreatedAt);
    }

    private static InventoryMovementDto ToMovementDto(InventoryMovement movement) => new(
        movement.Id,
        movement.InventoryItemId,
        movement.InventoryItem.Name,
        movement.Type.ToString(),
        movement.Quantity,
        movement.UnitCost,
        movement.ServiceTicketId,
        movement.ServiceTicket == null ? null : movement.ServiceTicket.TicketNumber,
        movement.Reason,
        movement.CreatedAt);

    private static DateTimeOffset? ParseDate(string? value) => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed) ? parsed : null;
    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private static IResult? ValidateItem(UpsertItemRequest request, InventoryPricingSettings settings)
    {
        if (string.IsNullOrWhiteSpace(request.Sku)) return Results.BadRequest("SKU is required.");
        if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest("Name is required.");
        if (request.CostPrice is null) return Results.BadRequest("Cost price is required.");
        if (request.SalePrice is null) return Results.BadRequest("Sale price is required.");
        if (request.ReorderLevel is null) return Results.BadRequest("Reorder level is required.");
        if (request.CostPrice < 0 || request.SalePrice < 0) return Results.BadRequest("Prices cannot be negative.");
        if (request.ReorderLevel < 0) return Results.BadRequest("Reorder level cannot be negative.");
        if (!TryOptionalEnum<InventoryItemType>(request.ItemType, out _)) return Results.BadRequest("Invalid inventory item type.");
        if (!TryOptionalEnum<InventoryCondition>(request.Condition, out _)) return Results.BadRequest("Invalid inventory condition.");
        if (request.WarrantyDays is < 0) return Results.BadRequest("Warranty days cannot be negative.");

        var pricingErrors = InventoryPricingCalculator.Validate(PricingInput(request, settings));
        if (pricingErrors.Count > 0) return Results.BadRequest(string.Join(" ", pricingErrors));
        return null;
    }

    private static void ApplyItem(InventoryItem item, UpsertItemRequest request, InventoryPricingSettings settings)
    {
        var pricing = InventoryPricingCalculator.Calculate(PricingInput(request, settings));
        item.CategoryId = request.CategoryId;
        item.Sku = request.Sku!.Trim();
        item.Name = request.Name!.Trim();
        item.Description = TrimOrNull(request.Description);
        item.Unit = string.IsNullOrWhiteSpace(request.Unit) ? settings.DefaultUnit : request.Unit.Trim();
        TryOptionalEnum<InventoryItemType>(request.ItemType, out var itemType);
        TryOptionalEnum<InventoryCondition>(request.Condition, out var condition);
        item.ItemType = itemType ?? InventoryItemType.SparePart;
        item.Condition = condition ?? InventoryCondition.New;
        var costPrice = request.CostPrice.GetValueOrDefault();
        item.PurchasePrice = request.PurchasePrice ?? costPrice;
        item.ShippingCost = request.ShippingCost ?? 0;
        item.ServiceFee = request.ServiceFee ?? 0;
        item.MarginPercent = request.MarginPercent ?? 0;
        item.MarginAmount = pricing.MarginAmount;
        item.TaxPercent = request.TaxPercent ?? settings.DefaultTaxPercent;
        item.TaxAmount = pricing.TaxAmount;
        item.PriceBeforeTax = pricing.PriceBeforeTax;
        item.PriceAfterTax = pricing.PriceAfterTax;
        item.FixedSalePrice = pricing.FixedSalePrice;
        item.CostPrice = item.PurchasePrice;
        item.SalePrice = pricing.FixedSalePrice;
        item.Quality = TrimOrNull(request.Quality);
        item.WarrantyDays = request.WarrantyDays;
        item.SupplierName = TrimOrNull(request.SupplierName);
        item.BarcodeValue = TrimOrNull(request.BarcodeValue);
        item.LocationNote = TrimOrNull(request.LocationNote);
        item.ReorderLevel = request.ReorderLevel.GetValueOrDefault();
        item.IsActive = request.IsActive ?? true;
    }

    private static InventoryPricingInput PricingInput(UpsertItemRequest request, InventoryPricingSettings settings)
    {
        var hasFormulaInput = request.PurchasePrice.HasValue
            || request.ShippingCost.HasValue
            || request.ServiceFee.HasValue
            || request.MarginPercent.HasValue
            || request.TaxPercent.HasValue
            || request.FixedSalePrice.HasValue;

        return new InventoryPricingInput(
            request.PurchasePrice ?? request.CostPrice.GetValueOrDefault(),
            request.ShippingCost ?? 0,
            request.ServiceFee ?? 0,
            request.MarginPercent ?? 0,
            request.TaxPercent ?? settings.DefaultTaxPercent,
            request.FixedSalePrice ?? (hasFormulaInput ? null : request.SalePrice.GetValueOrDefault()),
            settings.RoundingIncrement);
    }

    private static async Task<InventoryPricingSettings> GetPricingSettingsAsync(AppDbContext context, int shopId, CancellationToken cancellationToken)
    {
        var values = await context.AppSettings
            .AsNoTracking()
            .Where(setting => setting.ShopId == shopId && ShopProfileSettingKeys.Defaults.Keys.Contains(setting.Key))
            .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, cancellationToken);

        decimal DecimalValue(string key) => decimal.TryParse(values.GetValueOrDefault(key) ?? ShopProfileSettingKeys.Defaults[key], NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : decimal.Parse(ShopProfileSettingKeys.Defaults[key], CultureInfo.InvariantCulture);

        string Value(string key) => values.GetValueOrDefault(key) ?? ShopProfileSettingKeys.Defaults[key];

        return new InventoryPricingSettings(
            DecimalValue(ShopProfileSettingKeys.InventoryDefaultTaxPercent),
            DecimalValue(ShopProfileSettingKeys.InventoryRoundingIncrement),
            string.IsNullOrWhiteSpace(Value(ShopProfileSettingKeys.InventoryDefaultUnit)) ? "pcs" : Value(ShopProfileSettingKeys.InventoryDefaultUnit));
    }

    private static decimal Available(InventoryItem item) => item.OnHandQuantity - item.ReservedQuantity;
    private static decimal RemainingReserved(TicketInventoryUsage usage) => usage.Quantity - usage.ConsumedQuantity - usage.ReleasedQuantity;
    private static IResult StockConflict() => Results.Conflict("Inventory stock changed. Reload item and try again.");

    private static string Status(decimal quantity, decimal consumed, decimal released, decimal returned)
    {
        if (returned > 0) return "Returned";
        if (consumed >= quantity) return "Consumed";
        if (released >= quantity) return "Released";
        if (consumed > 0) return "PartiallyConsumed";
        return "Reserved";
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool TryOptionalEnum<TEnum>(string? value, out TEnum? parsed) where TEnum : struct, Enum
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (!Enum.TryParse<TEnum>(value.Trim(), true, out var enumValue) || !Enum.IsDefined(enumValue)) return false;
        parsed = enumValue;
        return true;
    }

    private static async Task<bool> HasPermissionAsync(AppDbContext context, ClaimsPrincipal principal, int shopId, string permission, CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId)) return false;

        var roleIds = context.ShopUserRoles
            .Where(userRole => userRole.ShopUser.UserId == userId && userRole.ShopUser.ShopId == shopId && userRole.ShopUser.Status == ShopUserStatus.Active)
            .Select(userRole => userRole.RoleId);

        return await context.RolePermissions.AnyAsync(rolePermission => roleIds.Contains(rolePermission.RoleId) && rolePermission.Permission.Code == permission, cancellationToken);
    }
}
