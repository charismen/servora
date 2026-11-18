using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servora.Api.Features.Customers;
using Servora.Api.Infrastructure.Data;
using Xunit;

namespace Servora.Api.IntegrationTests.Features.Customers;

public class CustomerTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly IntegrationTestWebAppFactory _factory;
    private readonly HttpClient _client;

    public CustomerTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateCustomer_WithValidRequest_ShouldReturnCreated()
    {
        // Arrange
        var command = new CreateCustomer.Command(
            "Test Customer",
            "+628123456789",
            "test@example.com",
            "Test Address",
            "Test Notes"
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/customers", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<CreateCustomer.Result>();
        result.Should().NotBeNull();
        result!.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateCustomer_WithInvalidPhoneNumber_ShouldReturnBadRequest()
    {
        // Arrange
        var command = new CreateCustomer.Command(
            "Test Customer",
            "invalid-phone-abc",
            "test@example.com",
            "Test Address",
            "Test Notes"
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/customers", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCustomers_ShouldReturnOkWithItems()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/customers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetCustomers.Result>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCustomers_WithNameSort_ReturnsSortedItems()
    {
        var prefix = $"Sort {Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/api/v1/customers", new CreateCustomer.Command($"{prefix} Beta", $"+628{Random.Shared.Next(100000000, 999999999)}", null, null, null));
        await _client.PostAsJsonAsync("/api/v1/customers", new CreateCustomer.Command($"{prefix} Alpha", $"+628{Random.Shared.Next(100000000, 999999999)}", null, null, null));

        var response = await _client.GetAsync($"/api/v1/customers?search={Uri.EscapeDataString(prefix)}&sort=name-asc&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<GetCustomers.Result>();
        result!.Items.Select(customer => customer.FullName).Should().ContainInOrder($"{prefix} Alpha", $"{prefix} Beta");
    }

    [Fact]
    public async Task UpdateCustomer_WithStaleUpdatedAt_ReturnsConflict()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/customers", new CreateCustomer.Command(
            $"Conflict {Guid.NewGuid():N}",
            $"+628{Random.Shared.Next(100000000, 999999999)}",
            null,
            null,
            null));
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateCustomer.Result>();
        var customerId = createResult!.Id;

        var firstUpdate = await _client.PutAsJsonAsync($"/api/v1/customers/{customerId}", new UpdateCustomer.Command(
            customerId,
            "Conflict Customer",
            "+628123456701",
            null,
            null,
            "First edit"));
        firstUpdate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var current = await (await _client.GetAsync($"/api/v1/customers/{customerId}")).Content.ReadFromJsonAsync<GetCustomer.Result>();
        await Task.Delay(20);

        var secondUpdate = await _client.PutAsJsonAsync($"/api/v1/customers/{customerId}", new UpdateCustomer.Command(
            customerId,
            "Conflict Customer Updated",
            "+628123456701",
            null,
            null,
            "Second edit",
            current!.UpdatedAt));
        secondUpdate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var staleUpdate = await _client.PutAsJsonAsync($"/api/v1/customers/{customerId}", new UpdateCustomer.Command(
            customerId,
            "Stale Customer",
            "+628123456701",
            null,
            null,
            "Stale edit",
            current.UpdatedAt));

        staleUpdate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ToggleStar_ShouldReturnNoContent()
    {
        // Arrange: Create a customer first
        var createCommand = new CreateCustomer.Command(
            "Star Test Customer",
            "+628123456799",
            null,
            null,
            null
        );
        var createResponse = await _client.PostAsJsonAsync("/api/v1/customers", createCommand);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateCustomer.Result>();
        var customerId = createResult!.Id;

        // Act & Assert (Star)
        var starResponse = await _client.PostAsync($"/api/v1/customers/{customerId}/star", null);
        starResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act & Assert (Unstar)
        var unstarResponse = await _client.PostAsync($"/api/v1/customers/{customerId}/unstar", null);
        unstarResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ToggleBan_ShouldReturnNoContent()
    {
        // Arrange: Create a customer first
        var createCommand = new CreateCustomer.Command(
            "Ban Test Customer",
            "+628123456711",
            null,
            null,
            null
        );
        var createResponse = await _client.PostAsJsonAsync("/api/v1/customers", createCommand);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateCustomer.Result>();
        var customerId = createResult!.Id;

        // Act & Assert (Ban)
        var banModel = new ToggleBan.BanModel("Bounced check on previous repair");
        var banResponse = await _client.PostAsJsonAsync($"/api/v1/customers/{customerId}/ban", banModel);
        banResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act & Assert (Unban)
        var unbanResponse = await _client.PostAsync($"/api/v1/customers/{customerId}/unban", null);
        unbanResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteCustomer_WhenNoTickets_ShouldSoftDeleteAndReturnNoContent()
    {
        // Arrange
        var customerName = $"Delete Test {Guid.NewGuid():N}";
        var createResponse = await _client.PostAsJsonAsync("/api/v1/customers", new CreateCustomer.Command(
            customerName,
            $"+628{Random.Shared.Next(100000000, 999999999)}",
            null,
            null,
            null
        ));
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateCustomer.Result>();
        var customerId = createResult!.Id;

        // Add a device for this customer
        var deviceResponse = await _client.PostAsJsonAsync($"/api/v1/customers/{customerId}/devices", new
        {
            deviceType = "Phone",
            brand = "Samsung",
            model = "Galaxy S21",
            serialNumber = $"SN-{Guid.NewGuid():N}"
        });
        deviceResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act
        var deleteResponse = await _client.DeleteAsync($"/api/v1/customers/{customerId}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify customer cannot be found via API
        var getResponse = await _client.GetAsync($"/api/v1/customers/{customerId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Verify customer not in customer list
        var listResponse = await _client.GetAsync($"/api/v1/customers?search={Uri.EscapeDataString(customerName)}");
        var listResult = await listResponse.Content.ReadFromJsonAsync<GetCustomers.Result>();
        listResult!.Items.Should().NotContain(c => c.Id == customerId);

        // Verify customer and device in DB are soft-deleted
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbCustomer = await context.Customers.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == customerId);
        dbCustomer.Should().NotBeNull();
        dbCustomer!.DeletedAt.Should().NotBeNull();

        var dbDevices = await context.Devices.IgnoreQueryFilters().Where(d => d.CustomerId == customerId).ToListAsync();
        dbDevices.Should().OnlyContain(d => d.DeletedAt != null);
    }

    [Fact]
    public async Task DeleteCustomer_WhenCustomerHasActiveTickets_ShouldReturnConflict()
    {
        // Arrange
        var createCustomerResponse = await _client.PostAsJsonAsync("/api/v1/customers", new CreateCustomer.Command(
            $"Active Ticket Customer {Guid.NewGuid():N}",
            $"+628{Random.Shared.Next(100000000, 999999999)}",
            null,
            null,
            null
        ));
        var createResult = await createCustomerResponse.Content.ReadFromJsonAsync<CreateCustomer.Result>();
        var customerId = createResult!.Id;

        var ticketResponse = await _client.PostAsJsonAsync("/api/v1/service-tickets", new
        {
            customerId,
            newDevice = new
            {
                deviceType = "Laptop",
                brand = "Apple",
                model = "MacBook Pro",
                serialNumber = $"MBP-{Guid.NewGuid():N}"
            },
            complaint = "Screen flickering",
            priority = "Normal",
            createdAt = DateTimeOffset.UtcNow
        });
        ticketResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act
        var deleteResponse = await _client.DeleteAsync($"/api/v1/customers/{customerId}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await deleteResponse.Content.ReadAsStringAsync();
        body.Should().Contain("Cannot delete a customer who has existing service tickets.");
    }

    [Fact]
    public async Task DeleteCustomer_WhenCustomerTicketsAreSoftDeleted_ShouldSoftDeleteAndReturnNoContent()
    {
        // Arrange
        var createCustomerResponse = await _client.PostAsJsonAsync("/api/v1/customers", new CreateCustomer.Command(
            $"Deleted Ticket Customer {Guid.NewGuid():N}",
            $"+628{Random.Shared.Next(100000000, 999999999)}",
            null,
            null,
            null
        ));
        var createResult = await createCustomerResponse.Content.ReadFromJsonAsync<CreateCustomer.Result>();
        var customerId = createResult!.Id;

        var ticketResponse = await _client.PostAsJsonAsync("/api/v1/service-tickets", new
        {
            customerId,
            newDevice = new
            {
                deviceType = "Laptop",
                brand = "Dell",
                model = "XPS 15",
                serialNumber = $"DELL-{Guid.NewGuid():N}"
            },
            complaint = "Battery swollen",
            priority = "Normal",
            createdAt = DateTimeOffset.UtcNow
        });
        ticketResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using var jsonDoc = JsonDocument.Parse(await ticketResponse.Content.ReadAsStringAsync());
        var ticketId = jsonDoc.RootElement.GetProperty("id").GetInt32();

        // Soft-delete ticket
        var deleteTicketResponse = await _client.DeleteAsync($"/api/v1/service-tickets/{ticketId}");
        deleteTicketResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act: Delete customer
        var deleteResponse = await _client.DeleteAsync($"/api/v1/customers/{customerId}");

        // Assert: Succeeded because all tickets are soft-deleted
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify customer cannot be found via API
        var getResponse = await _client.GetAsync($"/api/v1/customers/{customerId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
