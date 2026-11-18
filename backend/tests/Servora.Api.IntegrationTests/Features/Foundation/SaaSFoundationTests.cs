using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servora.Api.Features.Customers;
using Servora.Api.Features.Identity.Users;
using Servora.Api.Features.Notifications;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data;
using Xunit;

namespace Servora.Api.IntegrationTests.Features.Foundation;

public class SaaSFoundationTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly IntegrationTestWebAppFactory _factory;

    public SaaSFoundationTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Customers_DoNotLeakAcrossShops()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var orgId = await context.Organizations.Select(o => o.Id).FirstAsync();
        var otherShop = new Shop { OrganizationId = orgId, Name = "Isolation Shop", Slug = $"isolation-{Guid.NewGuid():N}", IsActive = true };
        context.Shops.Add(otherShop);
        await context.SaveChangesAsync();

        context.Customers.Add(new Customer
        {
            ShopId = otherShop.Id,
            FullName = "Other Shop Customer",
            PhoneNumber = "+628888888888",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var response = await _factory.CreateClient().GetAsync("/api/v1/customers?pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<GetCustomers.Result>();
        result!.Items.Should().NotContain(customer => customer.FullName == "Other Shop Customer");
    }

    [Fact]
    public async Task Customers_RequirePermission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser
        {
            UserName = $"noperms-{Guid.NewGuid():N}@example.com",
            Email = $"noperms-{Guid.NewGuid():N}@example.com",
            FirstName = "No",
            LastName = "Perms",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id.ToString());

        var response = await client.GetAsync("/api/v1/customers");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ServiceTickets_CreateRequiresPermission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser
        {
            UserName = $"ticket-noperms-{Guid.NewGuid():N}@example.com",
            Email = $"ticket-noperms-{Guid.NewGuid():N}@example.com",
            FirstName = "No",
            LastName = "TicketPerms",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id.ToString());

        var response = await client.PostAsJsonAsync("/api/v1/service-tickets", new
        {
            customerId = 1,
            complaint = "Should not pass authorization"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ServiceTickets_ReadRequiresPermission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser
        {
            UserName = $"ticket-read-noperms-{Guid.NewGuid():N}@example.com",
            Email = $"ticket-read-noperms-{Guid.NewGuid():N}@example.com",
            FirstName = "No",
            LastName = "TicketReadPerms",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id.ToString());

        var response = await client.GetAsync("/api/v1/service-tickets?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ServiceTickets_MoveRequiresPermission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser
        {
            UserName = $"ticket-move-noperms-{Guid.NewGuid():N}@example.com",
            Email = $"ticket-move-noperms-{Guid.NewGuid():N}@example.com",
            FirstName = "No",
            LastName = "TicketMovePerms",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id.ToString());

        var response = await client.PostAsJsonAsync("/api/v1/service-tickets/1/move", new
        {
            statusId = 1,
            reason = "Should not pass authorization",
            overridePaymentBlock = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ServiceTickets_AssignRequiresPermission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser
        {
            UserName = $"ticket-assign-noperms-{Guid.NewGuid():N}@example.com",
            Email = $"ticket-assign-noperms-{Guid.NewGuid():N}@example.com",
            FirstName = "No",
            LastName = "TicketAssignPerms",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id.ToString());

        var response = await client.PostAsJsonAsync("/api/v1/service-tickets/1/assign", new
        {
            technicianUserId = 1
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ServiceTickets_AddNoteRequiresPermission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser
        {
            UserName = $"ticket-note-noperms-{Guid.NewGuid():N}@example.com",
            Email = $"ticket-note-noperms-{Guid.NewGuid():N}@example.com",
            FirstName = "No",
            LastName = "TicketNotePerms",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id.ToString());

        var response = await client.PostAsJsonAsync("/api/v1/service-tickets/1/notes", new
        {
            note = "Should not pass authorization",
            isPublicVisible = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ServiceTicketEndpoints_RequirePermission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser
        {
            UserName = $"ticket-endpoints-noperms-{Guid.NewGuid():N}@example.com",
            Email = $"ticket-endpoints-noperms-{Guid.NewGuid():N}@example.com",
            FirstName = "No",
            LastName = "TicketEndpointPerms",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id.ToString());

        foreach (var (name, request) in ServiceTicketPermissionRequests())
        {
            using var response = await client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden, name);
        }
    }

    [Fact]
    public async Task DashboardSummary_RequiresPermission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser
        {
            UserName = $"dashboard-noperms-{Guid.NewGuid():N}@example.com",
            Email = $"dashboard-noperms-{Guid.NewGuid():N}@example.com",
            FirstName = "No",
            LastName = "DashboardPerms",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id.ToString());

        var response = await client.GetAsync("/api/v1/dashboard/summary");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Kanban_RequiresPermission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser
        {
            UserName = $"kanban-noperms-{Guid.NewGuid():N}@example.com",
            Email = $"kanban-noperms-{Guid.NewGuid():N}@example.com",
            FirstName = "No",
            LastName = "KanbanPerms",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id.ToString());

        var response = await client.GetAsync("/api/v1/service-tickets/kanban");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TicketReceipt_RequiresPermission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser
        {
            UserName = $"receipt-noperms-{Guid.NewGuid():N}@example.com",
            Email = $"receipt-noperms-{Guid.NewGuid():N}@example.com",
            FirstName = "No",
            LastName = "ReceiptPerms",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id.ToString());

        var response = await client.GetAsync("/api/v1/service-tickets/1/receipt");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TicketWarranty_RequiresPermission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser
        {
            UserName = $"warranty-noperms-{Guid.NewGuid():N}@example.com",
            Email = $"warranty-noperms-{Guid.NewGuid():N}@example.com",
            FirstName = "No",
            LastName = "WarrantyPerms",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id.ToString());

        var response = await client.GetAsync("/api/v1/service-tickets/1/warranty");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CustomerServiceHistory_RequiresPermission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser
        {
            UserName = $"service-history-noperms-{Guid.NewGuid():N}@example.com",
            Email = $"service-history-noperms-{Guid.NewGuid():N}@example.com",
            FirstName = "No",
            LastName = "ServiceHistoryPerms",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id.ToString());

        var response = await client.GetAsync("/api/v1/customers/1/service-history");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RepeatCustomerSummary_RequiresPermission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser
        {
            UserName = $"repeat-summary-noperms-{Guid.NewGuid():N}@example.com",
            Email = $"repeat-summary-noperms-{Guid.NewGuid():N}@example.com",
            FirstName = "No",
            LastName = "RepeatSummaryPerms",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id.ToString());

        var response = await client.GetAsync("/api/v1/customers/1/repeat-summary");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeviceServiceHistory_RequiresPermission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser
        {
            UserName = $"device-history-noperms-{Guid.NewGuid():N}@example.com",
            Email = $"device-history-noperms-{Guid.NewGuid():N}@example.com",
            FirstName = "No",
            LastName = "DeviceHistoryPerms",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id.ToString());

        var response = await client.GetAsync("/api/v1/customers/1/devices/1/service-history");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TicketHistory_RequiresPermission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser
        {
            UserName = $"ticket-history-noperms-{Guid.NewGuid():N}@example.com",
            Email = $"ticket-history-noperms-{Guid.NewGuid():N}@example.com",
            FirstName = "No",
            LastName = "TicketHistoryPerms",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id.ToString());

        var response = await client.GetAsync("/api/v1/service-tickets/1/history");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Settings_RequirePermission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser
        {
            UserName = $"settings-noperms-{Guid.NewGuid():N}@example.com",
            Email = $"settings-noperms-{Guid.NewGuid():N}@example.com",
            FirstName = "No",
            LastName = "SettingsPerms",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id.ToString());

        var response = await client.GetAsync("/api/v1/settings/shop-profile");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reports_RequirePermission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser
        {
            UserName = $"reports-noperms-{Guid.NewGuid():N}@example.com",
            Email = $"reports-noperms-{Guid.NewGuid():N}@example.com",
            FirstName = "No",
            LastName = "ReportPerms",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id.ToString());

        var response = await client.GetAsync("/api/v1/reports/summary");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Inventory_RequirePermission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser
        {
            UserName = $"inventory-noperms-{Guid.NewGuid():N}@example.com",
            Email = $"inventory-noperms-{Guid.NewGuid():N}@example.com",
            FirstName = "No",
            LastName = "InventoryPerms",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id.ToString());

        var response = await client.GetAsync("/api/v1/inventory/items");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Activity_RequirePermission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser
        {
            UserName = $"activity-noperms-{Guid.NewGuid():N}@example.com",
            Email = $"activity-noperms-{Guid.NewGuid():N}@example.com",
            FirstName = "No",
            LastName = "ActivityPerms",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id.ToString());

        var response = await client.GetAsync("/api/v1/activity");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Devices_RequirePermission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser
        {
            UserName = $"devices-noperms-{Guid.NewGuid():N}@example.com",
            Email = $"devices-noperms-{Guid.NewGuid():N}@example.com",
            FirstName = "No",
            LastName = "DevicePerms",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id.ToString());

        var response = await client.GetAsync("/api/v1/customers/1/devices");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ServiceTemplates_RequirePermission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser
        {
            UserName = $"service-templates-noperms-{Guid.NewGuid():N}@example.com",
            Email = $"service-templates-noperms-{Guid.NewGuid():N}@example.com",
            FirstName = "No",
            LastName = "TemplatePerms",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id.ToString());

        var response = await client.GetAsync("/api/v1/service-templates?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ServiceStatuses_RequirePermission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser
        {
            UserName = $"service-statuses-noperms-{Guid.NewGuid():N}@example.com",
            Email = $"service-statuses-noperms-{Guid.NewGuid():N}@example.com",
            FirstName = "No",
            LastName = "StatusPerms",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id.ToString());

        var response = await client.GetAsync("/api/v1/service-statuses");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NotificationTemplates_RequirePermission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser
        {
            UserName = $"notification-templates-noperms-{Guid.NewGuid():N}@example.com",
            Email = $"notification-templates-noperms-{Guid.NewGuid():N}@example.com",
            FirstName = "No",
            LastName = "NotificationPerms",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id.ToString());

        var response = await client.GetAsync("/api/v1/notification-templates");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NotificationTemplates_PageAndSort()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var shopId = await context.Shops.OrderBy(shop => shop.Id).Select(shop => shop.Id).FirstAsync();
        context.NotificationTemplates.AddRange(
            new NotificationTemplate
            {
                ShopId = shopId,
                Key = $"aaaa_{suffix}",
                Channel = NotificationChannel.WhatsApp,
                Provider = "manual",
                Locale = "en",
                Name = "A Template",
                Body = "A"
            },
            new NotificationTemplate
            {
                ShopId = shopId,
                Key = $"zzzz_{suffix}",
                Channel = NotificationChannel.WhatsApp,
                Provider = "manual",
                Locale = "en",
                Name = "Z Template",
                Body = "Z"
            });
        await context.SaveChangesAsync();

        var response = await _factory.CreateClient().GetAsync("/api/v1/notification-templates?channel=WhatsApp&page=1&pageSize=1&sort=key-desc");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<NotificationTemplateEndpoints.NotificationTemplatePageDto>();
        result!.Page.Should().Be(1);
        result.PageSize.Should().Be(1);
        result.Items.Should().ContainSingle();
        result.Items.Single().Key.Should().Be($"zzzz_{suffix}");
    }

    [Fact]
    public async Task ShopUsers_RequirePermission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser
        {
            UserName = $"shop-users-noperms-{Guid.NewGuid():N}@example.com",
            Email = $"shop-users-noperms-{Guid.NewGuid():N}@example.com",
            FirstName = "No",
            LastName = "UserPerms",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id.ToString());

        var response = await client.GetAsync("/api/v1/shop-users?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ShopUsers_PageAndSort()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var shopId = await context.Shops.OrderBy(shop => shop.Id).Select(shop => shop.Id).FirstAsync();
        var alpha = new ApplicationUser
        {
            UserName = $"alpha-{suffix}@example.com",
            Email = $"alpha-{suffix}@example.com",
            FirstName = $"Alpha{suffix}",
            LastName = "User",
            IsActive = true
        };
        var zulu = new ApplicationUser
        {
            UserName = $"zulu-{suffix}@example.com",
            Email = $"zulu-{suffix}@example.com",
            FirstName = $"Zzzzz{suffix}",
            LastName = "User",
            IsActive = true
        };
        context.Users.AddRange(alpha, zulu);
        await context.SaveChangesAsync();
        context.ShopUsers.AddRange(
            new ShopUser { ShopId = shopId, UserId = alpha.Id, Status = ShopUserStatus.Active },
            new ShopUser { ShopId = shopId, UserId = zulu.Id, Status = ShopUserStatus.Active });
        await context.SaveChangesAsync();

        var response = await _factory.CreateClient().GetAsync($"/api/v1/shop-users?search={suffix}&page=1&pageSize=1&sort=name-desc");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ShopUserEndpoints.ShopUserPageDto>();
        result!.Page.Should().Be(1);
        result.PageSize.Should().Be(1);
        result.Items.Should().ContainSingle();
        result.Items.Single().FullName.Should().StartWith("Zzzzz");
    }

    [Fact]
    public async Task ShopStaff_RequirePermission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new ApplicationUser
        {
            UserName = $"shop-staff-noperms-{Guid.NewGuid():N}@example.com",
            Email = $"shop-staff-noperms-{Guid.NewGuid():N}@example.com",
            FirstName = "No",
            LastName = "StaffPerms",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", user.Id.ToString());

        var response = await client.GetAsync("/api/v1/shop-users/staff");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static IEnumerable<(string Name, HttpRequestMessage Request)> ServiceTicketPermissionRequests()
    {
        yield return ("ticket by number", new HttpRequestMessage(HttpMethod.Get, "/api/v1/service-tickets/by-number/T-1"));
        yield return ("ticket detail", new HttpRequestMessage(HttpMethod.Get, "/api/v1/service-tickets/1"));
        yield return ("receipt print log", new HttpRequestMessage(HttpMethod.Post, "/api/v1/service-tickets/1/receipt/print-log"));
        yield return ("ticket update", JsonRequest(HttpMethod.Put, "/api/v1/service-tickets/1", new
        {
            complaint = "Should not pass authorization",
            initialDiagnosis = (string?)null,
            physicalConditionNotes = (string?)null,
            accessorySummary = (string?)null,
            photoNotes = (string?)null,
            diagnosis = (string?)null,
            repairNotes = (string?)null,
            warrantyUntil = (DateTimeOffset?)null,
            warrantyTerms = (string?)null,
            priority = "Normal",
            createdAt = (DateTimeOffset?)null,
            updatedAt = (DateTimeOffset?)null
        }));
        yield return ("ticket change status", JsonRequest(HttpMethod.Post, "/api/v1/service-tickets/1/change-status", new
        {
            statusId = 1,
            reason = "Should not pass authorization",
            overridePaymentBlock = false
        }));
        yield return ("accessory add", JsonRequest(HttpMethod.Post, "/api/v1/service-tickets/1/accessories", new
        {
            name = "Charger",
            quantity = 1,
            notes = "Should not pass authorization"
        }));
        yield return ("accessory delete", new HttpRequestMessage(HttpMethod.Delete, "/api/v1/service-tickets/1/accessories/1"));
        yield return ("attachment upload", new HttpRequestMessage(HttpMethod.Post, "/api/v1/service-tickets/1/attachments")
        {
            Content = new MultipartFormDataContent
            {
                { new ByteArrayContent("blocked"u8.ToArray()), "file", "blocked.txt" }
            }
        });
        yield return ("attachment download", new HttpRequestMessage(HttpMethod.Get, "/api/v1/service-tickets/1/attachments/1"));
        yield return ("attachment delete", new HttpRequestMessage(HttpMethod.Delete, "/api/v1/service-tickets/1/attachments/1"));
        yield return ("attachment visibility update", JsonRequest(HttpMethod.Put, "/api/v1/service-tickets/1/attachments/1/visibility", new { isPublicVisible = true }));
        yield return ("approvals get", new HttpRequestMessage(HttpMethod.Get, "/api/v1/service-tickets/1/approvals"));
        yield return ("approval create", JsonRequest(HttpMethod.Post, "/api/v1/service-tickets/1/approvals", new
        {
            title = "Estimate",
            description = "Should not pass authorization",
            estimatedAmount = 100
        }));
        yield return ("approval approve", JsonRequest(HttpMethod.Post, "/api/v1/service-tickets/1/approvals/1/approve", new { note = "no" }));
        yield return ("approval reject", JsonRequest(HttpMethod.Post, "/api/v1/service-tickets/1/approvals/1/reject", new { note = "no" }));
        yield return ("work items get", new HttpRequestMessage(HttpMethod.Get, "/api/v1/service-tickets/1/work-items"));
        yield return ("work item create", JsonRequest(HttpMethod.Post, "/api/v1/service-tickets/1/work-items", new
        {
            itemType = "Service",
            title = "Blocked work item",
            description = (string?)null,
            quantity = 1,
            unitPrice = 100
        }));
        yield return ("work item update", JsonRequest(HttpMethod.Put, "/api/v1/service-tickets/1/work-items/1", new
        {
            itemType = "Service",
            title = "Blocked work item edit",
            description = (string?)null,
            quantity = 1,
            unitPrice = 100,
            isCompleted = false
        }));
        yield return ("work item delete", new HttpRequestMessage(HttpMethod.Delete, "/api/v1/service-tickets/1/work-items/1"));
        yield return ("payment get", new HttpRequestMessage(HttpMethod.Get, "/api/v1/service-tickets/1/payment"));
        yield return ("payment update", JsonRequest(HttpMethod.Put, "/api/v1/service-tickets/1/payment", new
        {
            status = "Paid",
            amountPaid = 100,
            totalAmount = 100,
            method = "Cash",
            referenceNumber = (string?)null,
            notes = "Should not pass authorization"
        }));
        yield return ("payment transaction record", JsonRequest(HttpMethod.Post, "/api/v1/service-tickets/1/payment/transactions", new
        {
            entryType = "Payment",
            amount = 100,
            method = "Cash"
        }));
        yield return ("payment transaction void", JsonRequest(HttpMethod.Post, "/api/v1/service-tickets/1/payment/transactions/1/void", new
        {
            reason = "Should not pass authorization"
        }));
        yield return ("billing update", JsonRequest(HttpMethod.Put, "/api/v1/service-tickets/1/billing", new
        {
            discountAmount = 10,
            isWaived = false
        }));
        yield return ("pickup override", JsonRequest(HttpMethod.Post, "/api/v1/service-tickets/1/pickup-override", new
        {
            reason = "Should not pass authorization"
        }));
        yield return ("checklist get", new HttpRequestMessage(HttpMethod.Get, "/api/v1/service-tickets/1/checklist"));
        yield return ("apply template", JsonRequest(HttpMethod.Post, "/api/v1/service-tickets/1/apply-template", new
        {
            templateId = 1
        }));
        yield return ("checklist update", JsonRequest(HttpMethod.Put, "/api/v1/service-tickets/1/checklist/1", new
        {
            isCompleted = true
        }));
        yield return ("ticket delete", new HttpRequestMessage(HttpMethod.Delete, "/api/v1/service-tickets/1"));
    }

    private static HttpRequestMessage JsonRequest(HttpMethod method, string uri, object body) => new(method, uri)
    {
        Content = JsonContent.Create(body)
    };
}
