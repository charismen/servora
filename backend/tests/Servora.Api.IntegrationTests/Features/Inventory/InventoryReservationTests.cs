using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servora.Api.Features.Customers;
using Servora.Api.Features.Devices;
using Servora.Api.Features.Inventory;
using Servora.Api.Features.ServiceStatuses;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Infrastructure.Data;

namespace Servora.Api.IntegrationTests.Features.Inventory;

public class InventoryReservationTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly IntegrationTestWebAppFactory _factory;
    private readonly HttpClient _client;

    public InventoryReservationTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task TicketInventoryReservation_ShouldUpdateStockAndUsage()
    {
        var (ticketId, itemId) = await SeedTicketAndItemAsync();

        var overReserve = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/inventory/reserve", new
        {
            inventoryItemId = itemId,
            quantity = 6,
            notes = "Too many"
        });
        overReserve.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var reserve = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/inventory/reserve", new
        {
            inventoryItemId = itemId,
            quantity = 3,
            notes = "Use for repair"
        });
        reserve.StatusCode.Should().Be(HttpStatusCode.Created);
        var usageId = await ReadIdAsync(reserve);

        var consume = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/inventory/{usageId}/consume", new
        {
            quantity = 2,
            reason = "Installed"
        });
        consume.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var release = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/inventory/{usageId}/release", new
        {
            quantity = 1,
            reason = "Unused"
        });
        release.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var item = await context.InventoryItems.AsNoTracking().FirstAsync(item => item.Id == itemId);
        var usage = await context.TicketInventoryUsages.AsNoTracking().FirstAsync(usage => usage.Id == usageId);
        var movements = await context.InventoryMovements
            .AsNoTracking()
            .Where(movement => movement.TicketInventoryUsageId == usageId)
            .Select(movement => movement.Type)
            .ToListAsync();

        item.OnHandQuantity.Should().Be(3);
        item.ReservedQuantity.Should().Be(0);
        usage.Quantity.Should().Be(3);
        usage.ConsumedQuantity.Should().Be(2);
        usage.ReleasedQuantity.Should().Be(1);
        movements.Should().BeEquivalentTo([InventoryMovementType.Reserve, InventoryMovementType.Consume, InventoryMovementType.Release]);
    }

    private async Task<(int TicketId, int ItemId)> SeedTicketAndItemAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var shopId = await context.Shops.Select(shop => shop.Id).FirstAsync();
        var status = await context.ServiceStatuses.FirstAsync(status => status.ShopId == shopId && status.IsInitial);
        var customer = new Customer
        {
            ShopId = shopId,
            FullName = $"Inventory Reservation {Guid.NewGuid():N}",
            PhoneNumber = $"+628{Random.Shared.Next(100000000, 999999999)}",
            CreatedAt = DateTimeOffset.UtcNow
        };
        var device = new Device
        {
            ShopId = shopId,
            Customer = customer,
            DeviceType = "Laptop",
            Brand = "Lenovo",
            Model = "ThinkPad",
            CreatedAt = DateTimeOffset.UtcNow
        };
        var ticket = new ServiceTicket
        {
            ShopId = shopId,
            Customer = customer,
            Device = device,
            StatusId = status.Id,
            TicketNumber = $"INV-{Guid.NewGuid():N}"[..16],
            PublicTrackingToken = Guid.NewGuid().ToString("N"),
            CustomerNameSnapshot = customer.FullName,
            CustomerPhoneSnapshot = customer.PhoneNumber,
            DeviceSnapshot = "Lenovo ThinkPad",
            Complaint = "Needs SSD replacement",
            Priority = ServiceTicketPriority.Normal,
            PaymentStatus = PaymentStatus.Unpaid,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var item = new InventoryItem
        {
            ShopId = shopId,
            Sku = $"RSV-{Guid.NewGuid():N}"[..16],
            Name = "Reservation Test SSD",
            Unit = "pcs",
            CostPrice = 100000,
            SalePrice = 150000,
            PurchasePrice = 100000,
            FixedSalePrice = 150000,
            OnHandQuantity = 5,
            IsActive = true
        };

        context.Customers.Add(customer);
        context.Devices.Add(device);
        context.ServiceTickets.Add(ticket);
        context.InventoryItems.Add(item);
        await context.SaveChangesAsync();
        return (ticket.Id, item.Id);
    }

    private static async Task<int> ReadIdAsync(HttpResponseMessage response)
    {
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return json.RootElement.GetProperty("id").GetInt32();
    }
}
