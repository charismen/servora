using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Servora.Api.Features.Inventory;
using Servora.Api.Features.Payroll;
using Servora.Api.Features.ServiceTickets;
using Xunit;

namespace Servora.Api.IntegrationTests.Features.Payroll;

public class CompensationCatalogAndChecklistTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;

    public CompensationCatalogAndChecklistTests(IntegrationTestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetNextInventorySku_ReturnsSequentialPrefixForCategory()
    {
        var catRes = await _client.PostAsJsonAsync("/api/v1/inventory/categories", new
        {
            name = "Cooling Fan",
            description = "Replacement cooling fans",
            isActive = true
        });
        catRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdCat = await catRes.Content.ReadFromJsonAsync<IdPayload>();

        var nextSku = await _client.GetFromJsonAsync<InventoryEndpoints.NextSkuDto>($"/api/v1/inventory/items/next-sku?categoryId={createdCat!.Id}");
        nextSku.Should().NotBeNull();
        nextSku!.Prefix.Should().Be("CF");
        nextSku.Sku.Should().Be("CF-001");
        nextSku.Sequence.Should().Be(1);
    }

    [Fact]
    public async Task BonusTaskCatalogAndCompensationTemplates_FullCrudWorkflowWorks()
    {
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var taskKey = $"CUSTOM_TASK_{uniqueSuffix}";

        var createTask = await _client.PostAsJsonAsync("/api/v1/bonus-tasks", new
        {
            taskKey,
            label = $"Custom Calibration {uniqueSuffix}",
            category = "Maintenance",
            defaultStandardRate = 30000m,
            defaultGamingRate = 60000m,
            defaultCustomerPrice = 150000m,
            defaultWorkItemType = "Service",
            defaultIsInternalOnly = false,
            isActive = true,
            sortOrder = 99
        });
        createTask.StatusCode.Should().Be(HttpStatusCode.Created);
        var taskDto = await createTask.Content.ReadFromJsonAsync<BonusTaskDefinitionEndpoints.BonusTaskDefinitionDto>();
        taskDto.Should().NotBeNull();
        taskDto!.TaskKey.Should().Be(taskKey);

        var listTasks = await _client.GetFromJsonAsync<List<BonusTaskDefinitionEndpoints.BonusTaskDefinitionDto>>("/api/v1/bonus-tasks?includeInactive=true");
        listTasks.Should().Contain(t => t.Id == taskDto.Id);

        var tplName = $"Template {uniqueSuffix}";
        var createTpl = await _client.PostAsJsonAsync("/api/v1/compensation-templates", new
        {
            name = tplName,
            description = "Fixed daily with weekend bonus",
            basePayMode = "FixedDaily",
            baseHourlyRate = 0m,
            payPolicy = new
            {
                fixedDailyRate = 120000m,
                weekendMultiplier = 1.5m,
                taskBonusTrigger = "TaskCompleted"
            },
            isActive = true,
            sortOrder = 50
        });
        createTpl.StatusCode.Should().Be(HttpStatusCode.Created);
        var tplDto = await createTpl.Content.ReadFromJsonAsync<CompensationTemplateEndpoints.CompensationTemplateDto>(JsonOptions);
        tplDto.Should().NotBeNull();
        tplDto!.BasePayMode.Should().Be("FixedDaily");
        tplDto.PayPolicy.FixedDailyRate.Should().Be(120000m);

        var delTpl = await _client.DeleteAsync($"/api/v1/compensation-templates/{tplDto.Id}");
        delTpl.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var delTask = await _client.DeleteAsync($"/api/v1/bonus-tasks/{taskDto.Id}");
        delTask.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task TicketWorkItemsInternalOnlyAndAdHocChecklist_PersistAndExcludeInternalFromBilling()
    {
        var customerRes = await _client.PostAsJsonAsync("/api/v1/customers", new
        {
            fullName = "Checklist & Bonus Customer",
            phoneNumber = "+628111222333"
        });
        customerRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var customer = await customerRes.Content.ReadFromJsonAsync<IdPayload>();

        var ticketRes = await _client.PostAsJsonAsync("/api/v1/service-tickets", new
        {
            customerId = customer!.Id,
            newDevice = new
            {
                deviceType = "Laptop",
                brand = "Lenovo",
                model = "ThinkPad X1",
                serialNumber = $"SN-{Guid.NewGuid():N}"[..14]
            },
            complaint = "Routine service",
            priority = "Normal",
            approvalRequired = false,
            isPreApproved = false
        });
        ticketRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var ticket = await ticketRes.Content.ReadFromJsonAsync<IdPayload>();

        // Add billable work item (100,000) and internal-only bonus work item (should force unitPrice 0 and stay off receipt)
        var billableRes = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticket!.Id}/work-items", new
        {
            itemType = "Service",
            title = "Standard Diagnostic",
            quantity = 1m,
            unitPrice = 100000m,
            isCompleted = true
        });
        billableRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var internalRes = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticket.Id}/work-items", new
        {
            itemType = "Service",
            title = "Internal Thermal Bonus",
            quantity = 1m,
            unitPrice = 75000m,
            isCompleted = true,
            bonusTaskKey = "CLEANING_REPASTE",
            deviceCategoryOverride = "Gaming",
            isInternalOnly = true
        });
        internalRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var internalItem = await internalRes.Content.ReadFromJsonAsync<ServiceTicketEndpoints.WorkItemDto>();
        internalItem!.IsInternalOnly.Should().BeTrue();
        internalItem.UnitPrice.Should().Be(0m);
        internalItem.BonusTaskKey.Should().Be("CLEANING_REPASTE");
        internalItem.DeviceCategoryOverride.Should().Be("Gaming");

        var receipt = await _client.GetFromJsonAsync<ServiceTicketEndpoints.TicketReceiptDto>($"/api/v1/service-tickets/{ticket.Id}/receipt", JsonOptions);
        receipt.Should().NotBeNull();
        receipt!.WorkItemsSubtotal.Should().Be(100000m);
        receipt.WorkItems.Should().ContainSingle(w => w.Title == "Standard Diagnostic");

        // Add ad-hoc checklist item, verify it exists, then delete it
        var addCheck = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticket.Id}/checklist", new
        {
            title = "Verify fan RPM under load",
            description = "Ensure < 80C"
        });
        addCheck.StatusCode.Should().Be(HttpStatusCode.Created);
        var checkDto = await addCheck.Content.ReadFromJsonAsync<ServiceTicketEndpoints.ChecklistItemDto>();
        checkDto.Should().NotBeNull();
        checkDto!.Title.Should().Be("Verify fan RPM under load");

        var delCheck = await _client.DeleteAsync($"/api/v1/service-tickets/{ticket.Id}/checklist/{checkDto.Id}");
        delCheck.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private sealed record IdPayload(int Id);
}
