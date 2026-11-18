using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servora.Api.Features.Inventory;
using Servora.Api.Infrastructure.Data;

namespace Servora.Api.IntegrationTests.Features.Inventory;

public class InventoryStockConcurrencyTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly IntegrationTestWebAppFactory _factory;

    public InventoryStockConcurrencyTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ConcurrentStockUpdates_ShouldFailSecondSave()
    {
        int itemId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var shopId = await context.Shops.Select(shop => shop.Id).FirstAsync();
            var item = new InventoryItem
            {
                ShopId = shopId,
                Sku = $"RACE-{Guid.NewGuid():N}",
                Name = "Race Test Part",
                Unit = "pcs",
                OnHandQuantity = 5,
                IsActive = true
            };
            context.InventoryItems.Add(item);
            await context.SaveChangesAsync();
            itemId = item.Id;
        }

        await using var scope1 = _factory.Services.CreateAsyncScope();
        await using var scope2 = _factory.Services.CreateAsyncScope();
        var context1 = scope1.ServiceProvider.GetRequiredService<AppDbContext>();
        var context2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();

        var item1 = await context1.InventoryItems.SingleAsync(item => item.Id == itemId);
        var item2 = await context2.InventoryItems.SingleAsync(item => item.Id == itemId);

        item1.OnHandQuantity += 1;
        item1.StockVersion++;
        await context1.SaveChangesAsync();

        item2.OnHandQuantity += 1;
        item2.StockVersion++;
        await FluentActions.Invoking(() => context2.SaveChangesAsync())
            .Should()
            .ThrowAsync<DbUpdateConcurrencyException>();
    }
}
