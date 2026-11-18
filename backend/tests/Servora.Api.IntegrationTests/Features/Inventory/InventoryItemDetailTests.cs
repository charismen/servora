using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servora.Api.Features.Inventory;
using Servora.Api.Infrastructure.Data;

namespace Servora.Api.IntegrationTests.Features.Inventory;

public class InventoryItemDetailTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly IntegrationTestWebAppFactory _factory;
    private readonly HttpClient _client;

    public InventoryItemDetailTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetItemDetailAndExportMovements_ShouldReturnItemMovementHistory()
    {
        int itemId;
        var sku = $"DET-{Guid.NewGuid():N}"[..16];
        const string itemName = "Detail Test SSD";

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var shopId = await context.Shops.Select(shop => shop.Id).FirstAsync();
            var item = new InventoryItem
            {
                ShopId = shopId,
                Sku = sku,
                Name = itemName,
                Unit = "pcs",
                CostPrice = 100000,
                SalePrice = 150000,
                PurchasePrice = 100000,
                FixedSalePrice = 150000,
                OnHandQuantity = 5,
                ReservedQuantity = 1,
                ReorderLevel = 2,
                IsActive = true
            };
            context.InventoryItems.Add(item);
            await context.SaveChangesAsync();

            context.InventoryMovements.Add(new InventoryMovement
            {
                ShopId = shopId,
                InventoryItemId = item.Id,
                Type = InventoryMovementType.StockIn,
                Quantity = 5,
                UnitCost = 100000,
                Reason = "Initial stock",
                CreatedAt = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();
            itemId = item.Id;
        }

        var detail = await _client.GetAsync($"/api/v1/inventory/items/{itemId}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        using var detailJson = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        detailJson.RootElement.GetProperty("item").GetProperty("sku").GetString().Should().Be(sku);
        detailJson.RootElement.GetProperty("stock").GetProperty("availableQuantity").GetDecimal().Should().Be(4);
        detailJson.RootElement.GetProperty("movements").GetArrayLength().Should().BeGreaterThan(0);

        var export = await _client.GetAsync($"/api/v1/inventory/movements/export?itemId={itemId}");
        export.StatusCode.Should().Be(HttpStatusCode.OK);
        var csv = await export.Content.ReadAsStringAsync();
        csv.Should().Contain(itemName);
        csv.Should().Contain("StockIn");
    }

    [Fact]
    public async Task ListItems_WithSortAndPaging_ReturnsExpectedOrder()
    {
        var prefix = $"SORT-{Guid.NewGuid():N}"[..13];
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var shopId = await context.Shops.Select(shop => shop.Id).FirstAsync();
            context.InventoryItems.AddRange(
                NewItem(shopId, $"{prefix}-A", "Sort Test Keyboard", 10, 9),
                NewItem(shopId, $"{prefix}-B", "Sort Test Battery", 10, 2),
                NewItem(shopId, $"{prefix}-C", "Sort Test Adapter", 10, 5));
            await context.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/v1/inventory/items?search={prefix}&sort=available-asc&page=1&pageSize=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("totalCount").GetInt32().Should().Be(3);
        json.RootElement.GetProperty("page").GetInt32().Should().Be(1);
        json.RootElement.GetProperty("pageSize").GetInt32().Should().Be(2);
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToList();
        items.Should().HaveCount(2);
        items[0].GetProperty("sku").GetString().Should().Be($"{prefix}-A");
        items[1].GetProperty("sku").GetString().Should().Be($"{prefix}-C");
    }

    [Fact]
    public async Task UpdateItem_WithMissingRequiredFields_ShouldReturnValidationError()
    {
        int itemId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var shopId = await context.Shops.Select(shop => shop.Id).FirstAsync();
            var item = new InventoryItem
            {
                ShopId = shopId,
                Sku = $"UPD-{Guid.NewGuid():N}"[..16],
                Name = "Validation Test RAM",
                Unit = "pcs",
                CostPrice = 100000,
                SalePrice = 150000,
                PurchasePrice = 100000,
                FixedSalePrice = 150000,
                ReorderLevel = 1,
                IsActive = true
            };
            context.InventoryItems.Add(item);
            await context.SaveChangesAsync();
            itemId = item.Id;
        }

        var response = await _client.PutAsync($"/api/v1/inventory/items/{itemId}", Json("{}"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("SKU is required.");
        body.Should().NotContain("Failed to read parameter");
    }

    [Fact]
    public async Task UpdateItem_WithStaleUpdatedAt_ShouldReturnConflict()
    {
        int itemId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var shopId = await context.Shops.Select(shop => shop.Id).FirstAsync();
            var item = NewItem(shopId, $"CON-{Guid.NewGuid():N}"[..16], "Conflict Test SSD", 5, 0);
            context.InventoryItems.Add(item);
            await context.SaveChangesAsync();
            itemId = item.Id;
        }

        var first = await _client.PutAsJsonAsync($"/api/v1/inventory/items/{itemId}", Request("Conflict Test SSD", null));
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var current = await _client.GetFromJsonAsync<InventoryEndpoints.InventoryItemDetailDto>($"/api/v1/inventory/items/{itemId}");
        await Task.Delay(20);

        var second = await _client.PutAsJsonAsync($"/api/v1/inventory/items/{itemId}", Request("Conflict Test SSD Updated", current!.Item.UpdatedAt));
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var stale = await _client.PutAsJsonAsync($"/api/v1/inventory/items/{itemId}", Request("Stale Conflict Test SSD", current.Item.UpdatedAt));

        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private static InventoryItem NewItem(int shopId, string sku, string name, decimal onHandQuantity, decimal reservedQuantity) => new()
    {
        ShopId = shopId,
        Sku = sku,
        Name = name,
        Unit = "pcs",
        CostPrice = 100000,
        SalePrice = 150000,
        PurchasePrice = 100000,
        FixedSalePrice = 150000,
        OnHandQuantity = onHandQuantity,
        ReservedQuantity = reservedQuantity,
        ReorderLevel = 1,
        IsActive = true
    };

    private static InventoryEndpoints.UpsertItemRequest Request(string name, DateTimeOffset? updatedAt) => new(
        CategoryId: null,
        Sku: $"CON-{Guid.NewGuid():N}"[..16],
        Name: name,
        Unit: "pcs",
        CostPrice: 100000,
        SalePrice: 150000,
        ReorderLevel: 1,
        IsActive: true,
        PurchasePrice: 100000,
        TaxPercent: 11,
        FixedSalePrice: 150000,
        UpdatedAt: updatedAt);

    private static StringContent Json(string value) => new(value, Encoding.UTF8, "application/json");
}
