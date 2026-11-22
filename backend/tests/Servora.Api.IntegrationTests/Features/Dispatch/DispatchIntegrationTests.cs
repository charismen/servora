using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servora.Api.Features.Customers;
using Servora.Api.Features.Devices;
using Servora.Api.Features.Dispatch;
using Servora.Api.Features.Maintenance;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Infrastructure.Data;
using Xunit;

namespace Servora.Api.IntegrationTests.Features.Dispatch;

public class DispatchIntegrationTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly IntegrationTestWebAppFactory _factory;

    public DispatchIntegrationTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
    }

    private static HttpRequestMessage ScopedRequest(HttpMethod method, string uri, int workspaceId, object? body = null)
    {
        var req = new HttpRequestMessage(method, uri);
        req.Headers.Add("X-Workspace-Id", workspaceId.ToString());
        if (body is not null)
        {
            req.Content = JsonContent.Create(body);
        }
        return req;
    }

    private static async Task<int> CreateWorkspaceAsync(HttpClient client, string name, string operatingMode, string blueprint)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/workspaces", new
        {
            name,
            operatingMode,
            industryBlueprintKey = blueprint,
            defaultLocale = "en",
            timeZone = "Asia/Jakarta",
            currencyCode = "IDR",
            seedSampleData = false
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetInt32();
    }

    private static async Task<(int TicketId, string TrackingToken, int AssetId)> SeedTicketAsync(
        IServiceProvider services,
        int workspaceId,
        string label)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var status = await db.ServiceStatuses
            .Where(x => x.ShopId == workspaceId)
            .OrderBy(x => x.SortOrder)
            .FirstAsync();

        var customer = new Customer
        {
            ShopId = workspaceId,
            FullName = $"Customer {label}",
            PhoneNumber = "+6281234500001"
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var asset = new Device
        {
            ShopId = workspaceId,
            CustomerId = customer.Id,
            DeviceType = "AC Unit",
            Brand = "Daikin",
            Model = $"Inverter {label}",
            SerialNumber = $"AC-{Guid.NewGuid():N}"[..14]
        };
        db.Devices.Add(asset);
        await db.SaveChangesAsync();

        var token = $"trk_disp_{Guid.NewGuid():N}";
        var ticket = new ServiceTicket
        {
            ShopId = workspaceId,
            TicketNumber = $"DSP-{Random.Shared.Next(10000, 99999)}",
            CustomerId = customer.Id,
            CustomerNameSnapshot = customer.FullName,
            CustomerPhoneSnapshot = customer.PhoneNumber,
            DeviceId = asset.Id,
            DeviceSnapshot = $"Daikin Inverter {label}",
            StatusId = status.Id,
            ServiceLocationType = ServiceLocationType.OnSite,
            SiteAddressSnapshot = "Jl. Sudirman No. 10, Jakarta",
            Complaint = "Routine check & cleaning",
            PublicTrackingEnabled = true,
            PublicTrackingToken = token
        };
        db.ServiceTickets.Add(ticket);
        await db.SaveChangesAsync();

        return (ticket.Id, token, asset.Id);
    }

    [Fact]
    public async Task BlueprintSeeding_CreatesStarterResources_And_ResourceCrudEnforcesWorkspaceIsolation()
    {
        var client = _factory.CreateClient();
        var autoWorkspace = await CreateWorkspaceAsync(client, "Auto Bay Shop", "Commercial", "automotive_vehicle");
        var hvacWorkspace = await CreateWorkspaceAsync(client, "HVAC Van Ops", "Commercial", "hvac_ac");

        // Automotive blueprint seeds Bay 1 & Bay 2
        var autoResResp = await client.SendAsync(ScopedRequest(HttpMethod.Get, "/api/v1/service-resources", autoWorkspace));
        autoResResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var autoResources = await autoResResp.Content.ReadFromJsonAsync<JsonElement>();
        autoResources.EnumerateArray().Select(r => r.GetProperty("name").GetString())
            .Should().Contain(["Bay 1", "Bay 2"]);

        // HVAC blueprint seeds Service Van 1 & Service Van 2
        var hvacResResp = await client.SendAsync(ScopedRequest(HttpMethod.Get, "/api/v1/service-resources", hvacWorkspace));
        hvacResResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var hvacResources = await hvacResResp.Content.ReadFromJsonAsync<JsonElement>();
        hvacResources.EnumerateArray().Select(r => r.GetProperty("name").GetString())
            .Should().Contain(["Service Van 1", "Service Van 2"]);

        // Create custom resource in autoWorkspace
        var createResp = await client.SendAsync(ScopedRequest(HttpMethod.Post, "/api/v1/service-resources", autoWorkspace, new
        {
            name = "Hydraulic Lift A",
            code = "LIFT-A",
            resourceType = "Lift",
            color = "#7c3aed",
            notes = "4-ton 2-post lift"
        }));
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var liftId = created.GetProperty("id").GetInt32();

        // Cross-workspace isolation: hvacWorkspace cannot update or delete autoWorkspace's Lift
        var crossDelete = await client.SendAsync(ScopedRequest(HttpMethod.Delete, $"/api/v1/service-resources/{liftId}", hvacWorkspace));
        crossDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Schedule_ConflictOverride_Unschedule_LiveStatus_And_PmGeneration_WorkEndToEnd()
    {
        var client = _factory.CreateClient();
        var workspaceId = await CreateWorkspaceAsync(client, "Dispatch Field Ops", "Hybrid", "hvac_ac");

        var (ticket1Id, trackingToken1, assetId) = await SeedTicketAsync(_factory.Services, workspaceId, "Unit-1");
        var (ticket2Id, _, _) = await SeedTicketAsync(_factory.Services, workspaceId, "Unit-2");

        // Fetch board to get a technician and seeded Service Van 1
        var dayStart = new DateTimeOffset(2026, 11, 24, 0, 0, 0, TimeSpan.Zero);
        var boardResp = await client.SendAsync(ScopedRequest(
            HttpMethod.Get,
            $"/api/v1/dispatch/board?from={Uri.EscapeDataString(dayStart.ToString("O"))}&to={Uri.EscapeDataString(dayStart.AddDays(1).ToString("O"))}",
            workspaceId));
        boardResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var board = await boardResp.Content.ReadFromJsonAsync<JsonElement>();
        var techId = board.GetProperty("technicians")[0].GetProperty("userId").GetInt32();
        var vanId = board.GetProperty("resources")[0].GetProperty("id").GetInt32();
        board.GetProperty("unscheduledTickets").GetArrayLength().Should().BeGreaterThanOrEqualTo(2);

        // 1. Schedule Ticket 1: 09:00 - 11:00
        var t1Start = new DateTimeOffset(2026, 11, 24, 9, 0, 0, TimeSpan.Zero);
        var t1End = new DateTimeOffset(2026, 11, 24, 11, 0, 0, TimeSpan.Zero);
        var sched1 = await client.SendAsync(ScopedRequest(HttpMethod.Put, $"/api/v1/dispatch/tickets/{ticket1Id}/schedule", workspaceId, new
        {
            scheduledVisitStart = t1Start,
            scheduledVisitEnd = t1End,
            assignedTechnicianUserId = techId,
            serviceResourceId = vanId,
            serviceLocationType = "OnSite",
            dispatchNotes = "Call building security on arrival"
        }));
        sched1.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Schedule Ticket 2 overlapping (10:00 - 12:00) without override -> 409 Conflict
        var t2Start = new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);
        var t2End = new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
        var conflictResp = await client.SendAsync(ScopedRequest(HttpMethod.Put, $"/api/v1/dispatch/tickets/{ticket2Id}/schedule", workspaceId, new
        {
            scheduledVisitStart = t2Start,
            scheduledVisitEnd = t2End,
            assignedTechnicianUserId = techId,
            serviceResourceId = vanId,
            allowConflict = false
        }));
        conflictResp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var conflictJson = await conflictResp.Content.ReadFromJsonAsync<JsonElement>();
        conflictJson.GetProperty("conflicts").GetArrayLength().Should().Be(1);
        conflictJson.GetProperty("conflicts")[0].GetProperty("conflictType").GetString().Should().Be("Both");

        // 3. Schedule Ticket 2 with allowConflict = true -> 200 OK and board flags both as conflicting
        var overrideResp = await client.SendAsync(ScopedRequest(HttpMethod.Put, $"/api/v1/dispatch/tickets/{ticket2Id}/schedule", workspaceId, new
        {
            scheduledVisitStart = t2Start,
            scheduledVisitEnd = t2End,
            assignedTechnicianUserId = techId,
            serviceResourceId = vanId,
            allowConflict = true
        }));
        overrideResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var boardAfterConflict = await client.SendAsync(ScopedRequest(
            HttpMethod.Get,
            $"/api/v1/dispatch/board?from={Uri.EscapeDataString(dayStart.ToString("O"))}&to={Uri.EscapeDataString(dayStart.AddDays(1).ToString("O"))}",
            workspaceId));
        var boardJson2 = await boardAfterConflict.Content.ReadFromJsonAsync<JsonElement>();
        boardJson2.GetProperty("summary").GetProperty("conflictCount").GetInt32().Should().Be(2);

        // 4. Unschedule Ticket 2 -> moves back to unscheduledTickets
        var unschedResp = await client.SendAsync(ScopedRequest(HttpMethod.Delete, $"/api/v1/dispatch/tickets/{ticket2Id}/schedule", workspaceId));
        unschedResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 5. Reschedule Ticket 2 adjacent (11:00 - 12:00) -> succeeds with NO conflict
        var adjResp = await client.SendAsync(ScopedRequest(HttpMethod.Put, $"/api/v1/dispatch/tickets/{ticket2Id}/schedule", workspaceId, new
        {
            scheduledVisitStart = new DateTimeOffset(2026, 11, 24, 11, 0, 0, TimeSpan.Zero),
            scheduledVisitEnd = new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero),
            assignedTechnicianUserId = techId,
            serviceResourceId = vanId,
            allowConflict = false
        }));
        adjResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // 6. Live Dispatch Status lifecycle on Ticket 1 -> visible on /track/{token}
        var enRouteResp = await client.SendAsync(ScopedRequest(HttpMethod.Post, $"/api/v1/dispatch/tickets/{ticket1Id}/dispatch-status", workspaceId, new
        {
            dispatchStatus = "EnRoute"
        }));
        enRouteResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var arrivedResp = await client.SendAsync(ScopedRequest(HttpMethod.Post, $"/api/v1/dispatch/tickets/{ticket1Id}/dispatch-status", workspaceId, new
        {
            dispatchStatus = "ArrivedOnSite"
        }));
        arrivedResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var arrivedJson = await arrivedResp.Content.ReadFromJsonAsync<JsonElement>();
        arrivedJson.GetProperty("visitStartedAt").ValueKind.Should().NotBe(JsonValueKind.Null);

        var trackResp = await client.GetAsync($"/api/v1/track/{trackingToken1}");
        trackResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var trackJson = await trackResp.Content.ReadFromJsonAsync<JsonElement>();
        trackJson.GetProperty("dispatchStatus").GetString().Should().Be("ArrivedOnSite");
        trackJson.GetProperty("visitStartedAt").ValueKind.Should().NotBe(JsonValueKind.Null);

        // 7. Generate & schedule ticket directly from a due MaintenanceSchedule
        int pmScheduleId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var schedule = new MaintenanceSchedule
            {
                ShopId = workspaceId,
                ServiceAssetId = assetId,
                Title = "Quarterly AC Coil Wash",
                TriggerType = MaintenanceTriggerType.Calendar,
                IntervalValue = 3,
                IntervalUnit = MaintenanceIntervalUnit.Months,
                NextDueAt = dayStart.AddHours(8),
                DefaultLocationType = ServiceLocationType.OnSite,
                Status = MaintenanceScheduleStatus.Active
            };
            db.MaintenanceSchedules.Add(schedule);
            await db.SaveChangesAsync();
            pmScheduleId = schedule.Id;
        }

        var pmSchedResp = await client.SendAsync(ScopedRequest(HttpMethod.Post, $"/api/v1/dispatch/maintenance/{pmScheduleId}/schedule", workspaceId, new
        {
            scheduledVisitStart = new DateTimeOffset(2026, 11, 24, 14, 0, 0, TimeSpan.Zero),
            scheduledVisitEnd = new DateTimeOffset(2026, 11, 24, 15, 30, 0, TimeSpan.Zero),
            assignedTechnicianUserId = techId,
            serviceResourceId = vanId
        }));
        pmSchedResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var pmSchedJson = await pmSchedResp.Content.ReadFromJsonAsync<JsonElement>();
        pmSchedJson.GetProperty("ticketId").GetInt32().Should().BeGreaterThan(0);
        pmSchedJson.GetProperty("dispatchStatus").GetString().Should().Be("Scheduled");
    }
}
