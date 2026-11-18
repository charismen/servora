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

public class InventoryImportTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly IntegrationTestWebAppFactory _factory;
    private readonly HttpClient _client;

    public InventoryImportTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ApplyImport_ShouldCreateItemAndStockMovement()
    {
        var sku = $"IMP-{Guid.NewGuid():N}"[..16];
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var shopId = await context.Shops.Select(shop => shop.Id).FirstAsync();
            context.InventoryCategories.Add(new InventoryCategory { ShopId = shopId, Name = "Import Test Parts", IsActive = true });
            await context.SaveChangesAsync();
        }

        using var multipart = new MultipartFormDataContent();
        var csv = $"sku,name,category,purchase price,starting stock\n{sku},Import RAM,Import Test Parts,150000,3";
        multipart.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "items.csv");

        var preview = await _client.PostAsync("/api/v1/inventory/imports/preview", multipart);
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        using var previewJson = JsonDocument.Parse(await preview.Content.ReadAsStringAsync());
        var batchId = previewJson.RootElement.GetProperty("batchId").GetInt32();
        previewJson.RootElement.GetProperty("validRows").GetInt32().Should().Be(1);

        var apply = await _client.PostAsJsonAsync($"/api/v1/inventory/imports/{batchId}/apply", new { mode = "create" });
        apply.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var item = await verifyContext.InventoryItems.SingleAsync(item => item.Sku == sku);
        item.Name.Should().Be("Import RAM");
        item.OnHandQuantity.Should().Be(3);
        var hasMovement = await verifyContext.InventoryMovements
            .AnyAsync(movement => movement.InventoryItemId == item.Id && movement.Type == InventoryMovementType.StockIn && movement.Quantity == 3)
            ;
        hasMovement.Should().BeTrue();
    }
}
