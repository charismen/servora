using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servora.Api.Features.Blueprints;
using Servora.Api.Features.Customers;
using Servora.Api.Features.Maintenance;
using Servora.Api.Features.Organizations;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data;
using Xunit;

namespace Servora.Api.IntegrationTests.Features.Servora;

public class ServoraMultiIndustryTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly IntegrationTestWebAppFactory _factory;

    public ServoraMultiIndustryTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Blueprints_Endpoint_ReturnsAllSixIndustryPresets()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/blueprints");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jsonOpts = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        jsonOpts.Converters.Add(new JsonStringEnumConverter());
        var blueprints = await response.Content.ReadFromJsonAsync<List<IndustryBlueprintDefinition>>(jsonOpts);
        blueprints.Should().NotBeNull();
        blueprints!.Should().HaveCount(6);
        blueprints!.Select(b => b.Key).Should().Contain("automotive_vehicle").And.Contain("hvac_ac").And.Contain("facility_internal");
    }

    [Fact]
    public async Task WorkspaceCreation_And_AuthoritativeIsolation_WorkEndToEnd()
    {
        var client = _factory.CreateClient();

        // Create a new HVAC commercial workspace under the current organization
        var createResp = await client.PostAsJsonAsync("/api/v1/workspaces", new
        {
            name = "HVAC Field Division",
            operatingMode = "Hybrid",
            industryBlueprintKey = "hvac_ac",
            defaultLocale = "en",
            timeZone = "Asia/Jakarta",
            currencyCode = "IDR",
            seedSampleData = true
        });

        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdJson = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var newWorkspaceId = createdJson.GetProperty("id").GetInt32();
        newWorkspaceId.Should().BeGreaterThan(0);

        // Fetch blueprint config scoped to the newly created workspace via X-Workspace-Id
        using var scopedReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/blueprints/workspace-config");
        scopedReq.Headers.Add("X-Workspace-Id", newWorkspaceId.ToString());
        var configResp = await client.SendAsync(scopedReq);
        configResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var configJson = await configResp.Content.ReadFromJsonAsync<JsonElement>();
        configJson.GetProperty("industryBlueprintKey").GetString().Should().Be("hvac_ac");
        configJson.GetProperty("assetFields").GetArrayLength().Should().BeGreaterThan(0);

        // Verify unauthorized workspace header is rejected with 403 Forbidden
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var orgId = await db.Organizations.Select(o => o.Id).FirstAsync();
        var foreignWorkspace = new Shop
        {
            OrganizationId = orgId,
            Name = "Unassigned Branch",
            Slug = $"unassigned-{Guid.NewGuid():N}",
            IsActive = true
        };
        db.Shops.Add(foreignWorkspace);
        await db.SaveChangesAsync();

        using var forbiddenReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/customers");
        forbiddenReq.Headers.Add("X-Workspace-Id", foreignWorkspace.Id.ToString());
        var forbiddenResp = await client.SendAsync(forbiddenReq);
        forbiddenResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Organization summary returns KPIs across workspaces
        var summaryResp = await client.GetAsync("/api/v1/organizations/summary");
        summaryResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await summaryResp.Content.ReadFromJsonAsync<OrganizationWorkspaceEndpoints.OrganizationSummaryDto>();
        summary.Should().NotBeNull();
        summary!.TotalWorkspaces.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task PreventiveMaintenance_AutoCreatesTicketWhenDue()
    {
        var client = _factory.CreateClient();

        // Create internal department requester & facility asset
        var custResp = await client.PostAsJsonAsync("/api/v1/customers", new
        {
            fullName = "Building Maintenance Dept",
            phoneNumber = "",
            requesterType = "Department",
            departmentName = "Facilities",
            departmentCode = "FAC-01",
            costCenter = "CC-400"
        });
        custResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var custJson = await custResp.Content.ReadFromJsonAsync<JsonElement>();
        var customerId = custJson.GetProperty("id").GetInt32();

        var assetResp = await client.PostAsJsonAsync($"/api/v1/customers/{customerId}/devices", new
        {
            deviceType = "Chiller",
            brand = "Carrier",
            model = "30XA-250",
            serialNumber = "CH-99881",
            meterReading = 1200,
            meterUnit = "hours"
        });
        assetResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var assetJson = await assetResp.Content.ReadFromJsonAsync<JsonElement>();
        var assetId = assetJson.GetProperty("id").GetInt32();

        // Create already-due maintenance schedule with AutoCreateTicket = true
        var schedResp = await client.PostAsJsonAsync("/api/v1/maintenance-schedules", new
        {
            serviceAssetId = assetId,
            title = "Quarterly Chiller Inspection",
            triggerType = "Calendar",
            intervalValue = 3,
            intervalUnit = "Months",
            nextDueAt = DateTimeOffset.UtcNow.AddDays(-1),
            leadTimeDays = 7,
            autoCreateTicket = true,
            defaultLocationType = "OnSite",
            status = "Active"
        });
        schedResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // Trigger preventive maintenance due check
        var runResp = await client.PostAsync("/api/v1/maintenance-schedules/run-due-check", null);
        runResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var runResult = await runResp.Content.ReadFromJsonAsync<PreventiveMaintenanceRunResult>();
        runResult.Should().NotBeNull();
        runResult!.TicketsAutoCreated.Should().BeGreaterThanOrEqualTo(1);
    }
}
