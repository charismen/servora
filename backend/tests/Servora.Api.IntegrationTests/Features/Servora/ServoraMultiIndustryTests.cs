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
        configJson.GetProperty("featureConfig").GetProperty("showSecurityCredentials").GetBoolean().Should().BeFalse();
        configJson.GetProperty("featureConfig").GetProperty("defaultServiceLocationType").GetString().Should().Be("OnSite");

        // Customize feature config for this workspace via PUT /blueprints/terminology
        using var updateFeatReq = new HttpRequestMessage(HttpMethod.Put, "/api/v1/blueprints/terminology")
        {
            Content = JsonContent.Create(new
            {
                operatingMode = "Hybrid",
                terminology = configJson.GetProperty("terminology"),
                featureConfig = new
                {
                    showSecurityCredentials = false,
                    securityCredentialMode = "None",
                    showAccessories = true,
                    quickAccessoriesEn = new[] { "Remote Control", "Circuit Breaker" },
                    quickAccessoriesId = new[] { "Remote AC", "MCB" },
                    showPreRepairChecklist = true,
                    showPhysicalConditionNotes = true,
                    showWarrantySection = true,
                    showBillingAndPayments = true,
                    showCustomerApprovals = false,
                    enableFieldService = true,
                    defaultServiceLocationType = "OnSite",
                    enablePreventiveMaintenance = true,
                    attachmentTypes = new[] { "IndoorUnit", "OutdoorUnit", "Other" }
                }
            })
        };
        updateFeatReq.Headers.Add("X-Workspace-Id", newWorkspaceId.ToString());
        var updateFeatResp = await client.SendAsync(updateFeatReq);
        updateFeatResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedJson = await updateFeatResp.Content.ReadFromJsonAsync<JsonElement>();
        updatedJson.GetProperty("featureConfig").GetProperty("showCustomerApprovals").GetBoolean().Should().BeFalse();
        updatedJson.GetProperty("featureConfig").GetProperty("quickAccessoriesEn").EnumerateArray().Select(x => x.GetString()).Should().Equal("Remote Control", "Circuit Breaker");

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

    [Fact]
    public async Task MultiAssetLinking_And_GeneralizedAttachmentType_WorkOnTickets()
    {
        var client = _factory.CreateClient();

        var custResp = await client.PostAsJsonAsync("/api/v1/customers", new
        {
            fullName = "HVAC Site Client",
            phoneNumber = "+628111222333",
            requesterType = "Customer",
            companyName = "PT Sejuk Bersama"
        });
        custResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var customerId = (await custResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var primaryAssetResp = await client.PostAsJsonAsync($"/api/v1/customers/{customerId}/devices", new
        {
            deviceType = "Split AC",
            brand = "Daikin",
            model = "FTKC25",
            serialNumber = "INDOOR-01"
        });
        primaryAssetResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var primaryAssetId = (await primaryAssetResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var secondaryAssetResp = await client.PostAsJsonAsync($"/api/v1/customers/{customerId}/devices", new
        {
            deviceType = "Split AC",
            brand = "Daikin",
            model = "RKC25",
            serialNumber = "OUTDOOR-01"
        });
        secondaryAssetResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var secondaryAssetId = (await secondaryAssetResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var ticketResp = await client.PostAsJsonAsync("/api/v1/service-tickets", new
        {
            customerId,
            deviceId = primaryAssetId,
            complaint = "Multi-unit AC servicing",
            approvalRequired = false,
            isPreApproved = false,
            serviceLocationType = "OnSite"
        });
        ticketResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var ticketId = (await ticketResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var linkResp = await client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/assets", new
        {
            assetId = secondaryAssetId,
            notes = "Outdoor condenser unit"
        });
        linkResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detailResp = await client.GetAsync($"/api/v1/service-tickets/{ticketId}");
        detailResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var detailJson = await detailResp.Content.ReadFromJsonAsync<JsonElement>();
        detailJson.GetProperty("linkedAssets").GetArrayLength().Should().Be(1);
        detailJson.GetProperty("linkedAssets")[0].GetProperty("serviceAssetId").GetInt32().Should().Be(secondaryAssetId);

        var unlinkResp = await client.DeleteAsync($"/api/v1/service-tickets/{ticketId}/assets/{secondaryAssetId}");
        unlinkResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detailAfter = await client.GetFromJsonAsync<JsonElement>($"/api/v1/service-tickets/{ticketId}");
        detailAfter.GetProperty("linkedAssets").GetArrayLength().Should().Be(0);
    }
}
