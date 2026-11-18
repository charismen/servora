using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servora.Api.Features.Customers;
using Servora.Api.Features.Devices;
using Servora.Api.Features.Inventory;
using Servora.Api.Features.Notifications;
using Servora.Api.Features.Reports;
using Servora.Api.Features.ServiceStatuses;
using Servora.Api.Features.Settings;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data;

namespace Servora.Api.IntegrationTests.Features.ServiceTickets;

[CollectionDefinition(nameof(ServiceTicketDetailTests), DisableParallelization = true)]
public sealed class ServiceTicketDetailTestCollection;

[Collection(nameof(ServiceTicketDetailTests))]
public class ServiceTicketDetailTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly IntegrationTestWebAppFactory _factory;
    private readonly HttpClient _client;

    public ServiceTicketDetailTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UpdatePayment_WithValidRequest_ChangesTicketPaymentStatus()
    {
        var ticketId = await CreateTicketAsync();

        var response = await _client.PutAsJsonAsync($"/api/v1/service-tickets/{ticketId}/payment", new
        {
            status = "Paid",
            amountPaid = 150000,
            totalAmount = 150000,
            method = "Cash",
            referenceNumber = "QA-PAID",
            notes = "Paid in test"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ticket = await context.ServiceTickets.AsNoTracking().FirstAsync(item => item.Id == ticketId);
        ticket.PaymentStatus.Should().Be(PaymentStatus.Paid);
    }

    [Fact]
    public async Task UpdatePayment_WithNegativeAmount_ReturnsValidationProblem()
    {
        var ticketId = await CreateTicketAsync();

        var response = await _client.PutAsJsonAsync($"/api/v1/service-tickets/{ticketId}/payment", new
        {
            status = "Paid",
            amountPaid = -1,
            totalAmount = 100
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("amountPaid");
    }

    [Fact]
    public async Task ChangeStatus_WithInvalidTransition_ReturnsBadRequest()
    {
        var ticketId = await CreateTicketAsync();
        var (fromStatus, toStatus) = await ResetRulesAndAddRuleAsync("Received", "In Repair");
        var invalidTarget = await GetStatusAsync("Diagnosing");

        var response = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/change-status", new
        {
            statusId = invalidTarget.Id,
            reason = "Invalid move",
            overridePaymentBlock = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        fromStatus.Id.Should().NotBe(toStatus.Id);
    }

    [Fact]
    public async Task ChangeStatus_ToCancelledWithoutReason_ReturnsBadRequest()
    {
        var ticketId = await CreateTicketAsync();
        await ResetRulesAsync();
        var cancelled = await GetStatusAsync("Cancelled");

        var response = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/change-status", new
        {
            statusId = cancelled.Id,
            reason = "",
            overridePaymentBlock = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangeStatus_WhenApprovalRequiredWithoutApproval_ReturnsBadRequest()
    {
        var ticketId = await CreateTicketAsync(approvalRequired: true);
        await ResetRulesAsync();
        var inspection = await GetStatusAsync("Diagnosing");

        var response = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/change-status", new
        {
            statusId = inspection.Id,
            reason = "Need approval first",
            overridePaymentBlock = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangeStatus_WhenPaymentRequiredWithoutPayment_ReturnsBadRequest()
    {
        var ticketId = await CreateTicketAsync(isPreApproved: true);
        var (_, toStatus) = await ResetRulesAndAddRuleAsync("Received", "Ready for Pickup", requiresPaymentCompleted: true);

        var response = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/change-status", new
        {
            statusId = toStatus.Id,
            reason = "Ready",
            overridePaymentBlock = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangeStatus_WhenPaymentRequiredWithOverride_RequiresPickupOverrideReason()
    {
        var ticketId = await CreateTicketAsync(isPreApproved: true);
        var (_, toStatus) = await ResetRulesAndAddRuleAsync("Received", "Ready for Pickup", requiresPaymentCompleted: true);

        var blocked = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/change-status", new
        {
            statusId = toStatus.Id,
            reason = "Ready",
            overridePaymentBlock = true
        });
        blocked.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var overrideResponse = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/pickup-override", new
        {
            reason = "Owner approved unpaid pickup."
        });
        overrideResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var allowed = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/change-status", new
        {
            statusId = toStatus.Id,
            reason = "Ready",
            overridePaymentBlock = true
        });
        allowed.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task MoveEndpoint_WhenApprovalRequiredWithoutApproval_ReturnsBadRequest()
    {
        var ticketId = await CreateTicketAsync(approvalRequired: true);
        await ResetRulesAsync();
        var inspection = await GetStatusAsync("Diagnosing");

        var response = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/move", new
        {
            statusId = inspection.Id,
            reason = "Need approval first",
            overridePaymentBlock = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateTicket_WhenTicketIsFinal_ReturnsBadRequest()
    {
        var ticketId = await CreateTicketAsync(isPreApproved: true);
        await SetTicketStatusAsync(ticketId, "Completed");

        var response = await _client.PutAsJsonAsync($"/api/v1/service-tickets/{ticketId}", new
        {
            complaint = "Changed after final",
            priority = "Normal",
            createdAt = DateTimeOffset.UtcNow
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReadyForPickup_AllowsEditing_AndTransitionToCompletedRequiresPayment()
    {
        var ticketId = await CreateTicketAsync(isPreApproved: true);
        await ResetRulesAsync();
        var readyForPickup = await GetStatusAsync("Ready for Pickup");
        var completed = await GetStatusAsync("Completed");

        // Moving to Ready for Pickup is allowed while Unpaid
        var toReady = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/change-status", new
        {
            statusId = readyForPickup.Id,
            reason = "Repair finished, ready on shelf",
            overridePaymentBlock = false
        });
        toReady.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Editing ticket details (e.g. warranty terms) is still allowed in Ready for Pickup
        var updateInReady = await _client.PutAsJsonAsync($"/api/v1/service-tickets/{ticketId}", new
        {
            complaint = "Unit cannot boot",
            warrantyTerms = "30 days service warranty",
            priority = "Normal",
            createdAt = DateTimeOffset.UtcNow
        });
        updateInReady.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Moving to Completed while Unpaid is blocked by default
        var blockedToCompleted = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/change-status", new
        {
            statusId = completed.Id,
            reason = "Customer picking up",
            overridePaymentBlock = false
        });
        blockedToCompleted.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Completing payment allows transition to Completed
        var payResponse = await _client.PutAsJsonAsync($"/api/v1/service-tickets/{ticketId}/payment", new
        {
            status = "Paid",
            amountPaid = 150000,
            totalAmount = 150000,
            method = "Cash"
        });
        payResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var allowedToCompleted = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/change-status", new
        {
            statusId = completed.Id,
            reason = "Customer paid and picked up",
            overridePaymentBlock = false
        });
        allowedToCompleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdatePayment_WhenPaidAmountExceedsTotal_ReturnsBadRequest()
    {
        var ticketId = await CreateTicketAsync();

        var response = await _client.PutAsJsonAsync($"/api/v1/service-tickets/{ticketId}/payment", new
        {
            status = "Paid",
            amountPaid = 200000,
            totalAmount = 100000
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResolveApproval_Twice_ReturnsBadRequest()
    {
        var ticketId = await CreateTicketAsync();
        var create = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/approvals", new
        {
            title = "Main board repair",
            description = "Replace shorted component",
            estimatedAmount = 250000
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        int approvalId;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            approvalId = await context.ApprovalRequests
                .Where(approval => approval.ServiceTicketId == ticketId)
                .Select(approval => approval.Id)
                .FirstAsync();
        }

        var first = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/approvals/{approvalId}/approve", new { note = "Approved" });
        var second = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/approvals/{approvalId}/reject", new { note = "Changed mind" });

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadAttachment_WithImage_Succeeds_AndDeleteRemovesRowAndFile()
    {
        var ticketId = await CreateTicketAsync();
        var response = await UploadAsync(ticketId, "image/png", "evidence.png");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        string path;
        int attachmentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var attachment = await context.ServiceTicketAttachments.AsNoTracking().FirstAsync(item => item.ServiceTicketId == ticketId);
            attachmentId = attachment.Id;
            path = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "ticket-attachments", attachment.StoredFileName);
        }

        File.Exists(path).Should().BeTrue();
        var deleteResponse = await _client.DeleteAsync($"/api/v1/service-tickets/{ticketId}/attachments/{attachmentId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await verifyContext.ServiceTicketAttachments.AnyAsync(item => item.Id == attachmentId)).Should().BeFalse();
        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public async Task UploadAttachment_WithInvalidMime_ReturnsBadRequest()
    {
        var ticketId = await CreateTicketAsync();
        var response = await UploadAsync(ticketId, "text/plain", "evidence.txt");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddAndDeleteAccessory_IsScopedToTicket()
    {
        var ticketId = await CreateTicketAsync();
        var otherTicketId = await CreateTicketAsync();

        var addResponse = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/accessories", new
        {
            name = "Charger",
            quantity = 1,
            notes = "Original"
        });
        addResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        int accessoryId;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            accessoryId = await context.ServiceTicketAccessories
                .Where(item => item.ServiceTicketId == ticketId)
                .Select(item => item.Id)
                .FirstAsync();
        }

        var wrongTicketResponse = await _client.DeleteAsync($"/api/v1/service-tickets/{otherTicketId}/accessories/{accessoryId}");
        wrongTicketResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var deleteResponse = await _client.DeleteAsync($"/api/v1/service-tickets/{ticketId}/accessories/{accessoryId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task MutatingOtherShopTicket_ReturnsNotFound()
    {
        var otherTicketId = await CreateOtherShopTicketAsync();

        var response = await _client.PutAsJsonAsync($"/api/v1/service-tickets/{otherTicketId}/payment", new
        {
            status = "Paid",
            amountPaid = 10,
            totalAmount = 10
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReadingOtherShopTicket_ReturnsNotFound()
    {
        var otherTicketId = await CreateOtherShopTicketAsync();

        var response = await _client.GetAsync($"/api/v1/service-tickets/{otherTicketId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateTicket_WithSameIdempotencyKey_ReturnsExistingTicket()
    {
        var key = Guid.NewGuid().ToString("N");
        var first = await CreateTicketResponseAsync(idempotencyKey: key);
        var second = await CreateTicketResponseAsync(idempotencyKey: key);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var firstId = await ReadTicketIdAsync(first);
        var secondId = await ReadTicketIdAsync(second);
        secondId.Should().Be(firstId);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ticketsWithKey = await context.ServiceTickets.CountAsync(ticket => ticket.IdempotencyKey == key);
        ticketsWithKey.Should().Be(1);
    }

    [Fact]
    public async Task CreateTicket_ForBannedCustomer_RequiresOverrideReason()
    {
        var customerId = await CreateCustomerAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = await context.Customers.FirstAsync(customer => customer.Id == customerId);
            customer.IsBanned = true;
            customer.BanReason = "Unpaid previous ticket";
            await context.SaveChangesAsync();
        }

        var blocked = await CreateTicketResponseAsync(customerId: customerId);
        blocked.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await blocked.Content.ReadAsStringAsync()).Should().Contain("Banned customer override reason is required.");

        var allowed = await CreateTicketResponseAsync(customerId: customerId, overrideReason: "Manager approved intake.");
        allowed.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateTicket_WhenPhotoEvidenceRequired_RequiresIntakePhotoCount()
    {
        await SetDefaultShopSettingAsync(ShopProfileSettingKeys.RequirePhotoEvidenceOnIntake, true);
        try
        {
            var blocked = await CreateTicketResponseAsync(intakePhotoCount: 0);
            blocked.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await blocked.Content.ReadAsStringAsync()).Should().Contain("Photo evidence is required");

            var allowed = await CreateTicketResponseAsync(intakePhotoCount: 1);
            allowed.StatusCode.Should().Be(HttpStatusCode.Created);
        }
        finally
        {
            await SetDefaultShopSettingAsync(ShopProfileSettingKeys.RequirePhotoEvidenceOnIntake, false);
        }
    }

    [Fact]
    public async Task ListTickets_WithPaging_ReturnsPage()
    {
        await CreateTicketAsync();
        await CreateTicketAsync();

        var response = await _client.GetAsync("/api/v1/service-tickets?page=1&pageSize=1&sort=oldest");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        json.RootElement.GetProperty("page").GetInt32().Should().Be(1);
        json.RootElement.GetProperty("pageSize").GetInt32().Should().Be(1);
        json.RootElement.GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(2);
        json.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Kanban_WithSearchPriorityAndPaymentFilters_ReturnsMatchingTicketOnly()
    {
        var marker = $"KANBAN-{Guid.NewGuid():N}";
        var matchingTicketId = await CreateTicketAsync();
        var excludedTicketId = await CreateTicketAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var matching = await context.ServiceTickets.FirstAsync(ticket => ticket.Id == matchingTicketId);
            matching.Complaint = marker;
            matching.Priority = ServiceTicketPriority.High;
            matching.PaymentStatus = PaymentStatus.Unpaid;

            var excluded = await context.ServiceTickets.FirstAsync(ticket => ticket.Id == excludedTicketId);
            excluded.Complaint = marker;
            excluded.Priority = ServiceTicketPriority.Low;
            excluded.PaymentStatus = PaymentStatus.Paid;
            await context.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/v1/service-tickets/kanban?search={marker}&priority=High&paymentStatus=Unpaid");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var ticketIds = json.RootElement
            .EnumerateArray()
            .SelectMany(column => column.GetProperty("tickets").EnumerateArray())
            .Select(ticket => ticket.GetProperty("id").GetInt32())
            .ToList();

        ticketIds.Should().ContainSingle().Which.Should().Be(matchingTicketId);
    }

    [Fact]
    public async Task PublicTracking_WithEnabledToken_ReturnsSafeTicketSummary()
    {
        var ticketId = await CreateTicketAsync();
        var token = await GetPublicTrackingTokenAsync(ticketId);

        var response = await _client.GetAsync($"/api/v1/track/{token}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        json.RootElement.GetProperty("ticketNumber").GetString().Should().NotBeNullOrWhiteSpace();
        json.RootElement.TryGetProperty("customerPhoneSnapshot", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("publicTrackingToken", out _).Should().BeFalse();
    }

    [Fact]
    public async Task PublicTracking_PhotoEvidenceFollowsShopSetting_Visibility_AndPhoneVerification()
    {
        var ticketId = await CreateTicketAsync();
        var privateUpload = await UploadAsync(ticketId, "image/png", "private-evidence.png", isPublicVisible: false);
        var publicUpload = await UploadAsync(ticketId, "image/png", "public-evidence.png", isPublicVisible: false);
        privateUpload.StatusCode.Should().Be(HttpStatusCode.NoContent);
        publicUpload.StatusCode.Should().Be(HttpStatusCode.NoContent);

        int publicAttachmentId;
        string phoneLastFour;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            publicAttachmentId = await context.ServiceTicketAttachments
                .Where(item => item.ServiceTicketId == ticketId && item.FileName == "public-evidence.png")
                .Select(item => item.Id)
                .FirstAsync();
            var phone = await context.ServiceTickets
                .Where(item => item.Id == ticketId)
                .Select(item => item.CustomerPhoneSnapshot)
                .FirstAsync();
            var digits = new string(phone.Where(char.IsAsciiDigit).ToArray());
            phoneLastFour = digits[^4..];
        }

        // Toggle visibility on the public photo
        var toggleResponse = await _client.PutAsJsonAsync(
            $"/api/v1/service-tickets/{ticketId}/attachments/{publicAttachmentId}/visibility",
            new { isPublicVisible = true });
        toggleResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var token = await GetPublicTrackingTokenAsync(ticketId);

        var hidden = await _client.GetAsync($"/api/v1/track/{token}");
        hidden.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var json = await JsonDocument.ParseAsync(await hidden.Content.ReadAsStreamAsync()))
        {
            json.RootElement.GetProperty("publicPhotoCount").GetInt32().Should().Be(0);
            json.RootElement.GetProperty("attachments").GetArrayLength().Should().Be(0);
        }

        await SetPublicPhotoSettingAsync(ticketId, true);

        // Initial GET returns count only, no attachment URLs leaked
        var visible = await _client.GetAsync($"/api/v1/track/{token}");
        visible.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var visibleJson = await JsonDocument.ParseAsync(await visible.Content.ReadAsStreamAsync()))
        {
            visibleJson.RootElement.GetProperty("publicPhotoCount").GetInt32().Should().Be(1);
            visibleJson.RootElement.GetProperty("attachments").GetArrayLength().Should().Be(0);
        }

        // Wrong last 4 digits fails
        var wrongDigits = phoneLastFour == "0000" ? "9999" : "0000";
        var wrongVerify = await _client.PostAsJsonAsync($"/api/v1/track/{token}/verify-photos", new { phoneLastFour = wrongDigits });
        wrongVerify.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Correct last 4 digits returns only the public-visible attachment
        var verify = await _client.PostAsJsonAsync($"/api/v1/track/{token}/verify-photos", new { phoneLastFour });
        verify.StatusCode.Should().Be(HttpStatusCode.OK);
        using var verifyJson = await JsonDocument.ParseAsync(await verify.Content.ReadAsStreamAsync());
        var attachment = verifyJson.RootElement.GetProperty("attachments").EnumerateArray().Should().ContainSingle().Subject;
        attachment.GetProperty("fileName").GetString().Should().Be("public-evidence.png");
        attachment.GetProperty("url").GetString().Should().StartWith("/uploads/ticket-attachments/");
        attachment.TryGetProperty("storedFileName", out _).Should().BeFalse();
    }

    [Fact]
    public async Task PublicTracking_HistoryOnlyShowsPublicNotes()
    {
        var ticketId = await CreateTicketAsync();
        var token = await GetPublicTrackingTokenAsync(ticketId);
        var privateNote = $"private-{Guid.NewGuid():N}";
        var publicNote = $"public-{Guid.NewGuid():N}";

        var privateResponse = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/notes", new
        {
            note = privateNote,
            isPublicVisible = false
        });
        var publicResponse = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/notes", new
        {
            note = publicNote,
            isPublicVisible = true
        });

        privateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        publicResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await _client.GetAsync($"/api/v1/track/{token}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var notes = json.RootElement
            .GetProperty("history")
            .EnumerateArray()
            .Select(history => history.GetProperty("note").GetString())
            .ToList();
        notes.Should().Contain(publicNote);
        notes.Should().NotContain(privateNote);
    }

    [Fact]
    public async Task PublicTracking_StatusHistoryDoesNotExposeReason()
    {
        var ticketId = await CreateTicketAsync();
        var token = await GetPublicTrackingTokenAsync(ticketId);
        var status = await GetStatusAsync("Diagnosing");
        var reason = $"internal-{Guid.NewGuid():N}";

        var move = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/change-status", new
        {
            statusId = status.Id,
            reason,
            overridePaymentBlock = false
        });
        var response = await _client.GetAsync($"/api/v1/track/{token}");

        move.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(reason);
        using var json = JsonDocument.Parse(body);
        json.RootElement
            .GetProperty("history")
            .EnumerateArray()
            .Should()
            .OnlyContain(history => history.GetProperty("reason").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task PublicTracking_WithDisabledToken_ReturnsNotFound()
    {
        var ticketId = await CreateTicketAsync();
        string token;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ticket = await context.ServiceTickets.FirstAsync(ticket => ticket.Id == ticketId);
            token = ticket.PublicTrackingToken;
            ticket.PublicTrackingEnabled = false;
            await context.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/v1/track/{token}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PublicTracking_WithUnknownToken_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/track/{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PublicTracking_WithDeletedTicket_ReturnsNotFound()
    {
        var ticketId = await CreateTicketAsync();
        var token = await GetPublicTrackingTokenAsync(ticketId);

        var delete = await _client.DeleteAsync($"/api/v1/service-tickets/{ticketId}");
        var response = await _client.GetAsync($"/api/v1/track/{token}");

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateTicket_WithStaleUpdatedAt_ReturnsConflict()
    {
        var ticketId = await CreateTicketAsync();
        DateTimeOffset? staleUpdatedAt;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ticket = await context.ServiceTickets.FirstAsync(ticket => ticket.Id == ticketId);
            staleUpdatedAt = ticket.UpdatedAt;
            ticket.RepairNotes = "Edited elsewhere";
            await context.SaveChangesAsync();
        }

        var response = await _client.PutAsJsonAsync($"/api/v1/service-tickets/{ticketId}", new
        {
            complaint = "Unit cannot boot",
            initialDiagnosis = "Pending inspection",
            physicalConditionNotes = (string?)null,
            accessorySummary = (string?)null,
            photoNotes = (string?)null,
            diagnosis = (string?)null,
            repairNotes = "My stale edit",
            warrantyUntil = (DateTimeOffset?)null,
            warrantyTerms = (string?)null,
            priority = "Normal",
            createdAt = (DateTimeOffset?)null,
            updatedAt = staleUpdatedAt
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task MultiWorkAndParts_Case1_ThreeWorksPlusBatteryAndSplitPayments_ComputesBillWithoutDoubleCounting()
    {
        var ticketId = await CreateTicketAsync(isPreApproved: true);
        await ResetRulesAsync();

        // Add 3 service work items: Reinstall Windows OS (150k) + Fix Hinge (200k) + Replace Battery Labor (100k) = 450k
        var w1 = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/work-items", new
        {
            itemType = "Service",
            title = "Reinstall Windows OS",
            quantity = 1m,
            unitPrice = 150000m
        });
        var w2 = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/work-items", new
        {
            itemType = "Service",
            title = "Fix Hinge",
            quantity = 1m,
            unitPrice = 200000m
        });
        var w3 = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/work-items", new
        {
            itemType = "Service",
            title = "Replace Battery",
            quantity = 1m,
            unitPrice = 100000m
        });
        w1.StatusCode.Should().Be(HttpStatusCode.Created);
        w2.StatusCode.Should().Be(HttpStatusCode.Created);
        w3.StatusCode.Should().Be(HttpStatusCode.Created);

        // Reserve stock battery (500k) -> reserved part is NOT billed yet
        var batteryId = await SeedInventoryItemAsync("Battery Pack 45Wh", fixedSalePrice: 500000m, onHandQuantity: 5m);
        var reserve = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/inventory/reserve", new
        {
            inventoryItemId = batteryId,
            quantity = 1m,
            notes = "Battery replacement stock"
        });
        reserve.StatusCode.Should().Be(HttpStatusCode.Created);
        var usageId = await ReadTicketIdAsync(reserve);

        var afterReserve = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/service-tickets/{ticketId}/payment");
        afterReserve.GetProperty("workItemsSubtotal").GetDecimal().Should().Be(450000m);
        afterReserve.GetProperty("consumedPartsSubtotal").GetDecimal().Should().Be(0m);
        afterReserve.GetProperty("totalAmount").GetDecimal().Should().Be(450000m);

        // Consume battery -> billed at 500k snapshot -> total becomes 950k without double-counting labor
        var consume = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/inventory/{usageId}/consume", new
        {
            quantity = 1m,
            reason = "Installed battery"
        });
        consume.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Mutate catalog price to 650k after consumption -> existing ticket retains 500k UnitPriceSnapshot
        await UpdateInventoryFixedSalePriceAsync(batteryId, 650000m);

        var afterConsume = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/service-tickets/{ticketId}/payment");
        afterConsume.GetProperty("consumedPartsSubtotal").GetDecimal().Should().Be(500000m);
        afterConsume.GetProperty("totalAmount").GetDecimal().Should().Be(950000m);
        afterConsume.GetProperty("remainingBalance").GetDecimal().Should().Be(950000m);
        afterConsume.GetProperty("status").GetString().Should().Be("Unpaid");

        // Split payment 1: Cash DP 300k -> Partial
        var dp = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/payment/transactions", new
        {
            entryType = "Payment",
            amount = 300000m,
            method = "Cash",
            referenceNumber = "CASH-DP-01",
            notes = "Customer DP"
        });
        dp.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterDp = await dp.Content.ReadFromJsonAsync<JsonElement>();
        afterDp.GetProperty("status").GetString().Should().Be("Partial");
        afterDp.GetProperty("amountPaid").GetDecimal().Should().Be(300000m);
        afterDp.GetProperty("remainingBalance").GetDecimal().Should().Be(650000m);

        // Split payment 2: QRIS settlement 650k -> Paid
        var settle = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/payment/transactions", new
        {
            entryType = "Payment",
            amount = 650000m,
            method = "QRIS",
            referenceNumber = "QRIS-991",
            notes = "Final pickup settlement"
        });
        settle.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterSettle = await settle.Content.ReadFromJsonAsync<JsonElement>();
        afterSettle.GetProperty("status").GetString().Should().Be("Paid");
        afterSettle.GetProperty("amountPaid").GetDecimal().Should().Be(950000m);
        afterSettle.GetProperty("remainingBalance").GetDecimal().Should().Be(0m);
        afterSettle.GetProperty("overpaidAmount").GetDecimal().Should().Be(0m);
        afterSettle.GetProperty("transactions").GetArrayLength().Should().Be(2);

        // Move through Ready for Pickup -> Completed now succeeds cleanly
        var readyForPickup = await GetStatusAsync("Ready for Pickup");
        var completed = await GetStatusAsync("Completed");
        (await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/change-status", new { statusId = readyForPickup.Id, overridePaymentBlock = false }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/change-status", new { statusId = completed.Id, overridePaymentBlock = false }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Receipt exposes full itemized breakdown + financial summary
        var receipt = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/service-tickets/{ticketId}/receipt");
        receipt.GetProperty("showPaymentSummary").GetBoolean().Should().BeTrue();
        receipt.GetProperty("paymentStatus").GetString().Should().Be("Paid");
        receipt.GetProperty("workItemsSubtotal").GetDecimal().Should().Be(450000m);
        receipt.GetProperty("consumedPartsSubtotal").GetDecimal().Should().Be(500000m);
        receipt.GetProperty("totalAmount").GetDecimal().Should().Be(950000m);
        receipt.GetProperty("amountPaid").GetDecimal().Should().Be(950000m);
        receipt.GetProperty("remainingBalance").GetDecimal().Should().Be(0m);
        receipt.GetProperty("workItems").GetArrayLength().Should().Be(3);
        var receiptPart = receipt.GetProperty("consumedParts").EnumerateArray().Should().ContainSingle().Subject;
        receiptPart.GetProperty("unitPrice").GetDecimal().Should().Be(500000m);
        receiptPart.GetProperty("lineTotal").GetDecimal().Should().Be(500000m);
    }

    [Fact]
    public async Task SupplementaryApproval_Case2_BlocksForwardTransition_AndRevertsPaidToPartialWhenApproved()
    {
        var ticketId = await CreateTicketAsync(isPreApproved: true);
        await ResetRulesAsync();
        await SetTicketStatusAsync(ticketId, "In Repair");

        // Confirm and pay initial 2 jobs: OS Reinstall (150k) + Fix Hinge (200k) = 350k
        (await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/work-items", new { itemType = "Service", title = "Reinstall Windows OS", quantity = 1m, unitPrice = 150000m }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/work-items", new { itemType = "Service", title = "Fix Hinge", quantity = 1m, unitPrice = 200000m }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/payment/transactions", new { entryType = "Payment", amount = 350000m, method = "Cash" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Request an optional approval and reject it -> ticket remains Paid at 350k
        var rejectedQuote = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/approvals", new
        {
            title = "Thermal paste upgrade",
            description = "Optional cooling upgrade",
            proposedWorkItems = new[]
            {
                new { itemType = "Service", title = "Liquid metal repaste", quantity = 1m, unitPrice = 50000m }
            }
        });
        rejectedQuote.StatusCode.Should().Be(HttpStatusCode.Created);
        var rejectedApprovalId = await ReadTicketIdAsync(rejectedQuote);

        (await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/approvals/{rejectedApprovalId}/reject", new { note = "Customer declined" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterReject = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/service-tickets/{ticketId}");
        afterReject.GetProperty("paymentStatus").GetString().Should().Be("Paid");
        afterReject.GetProperty("totalAmount").GetDecimal().Should().Be(350000m);
        afterReject.GetProperty("hasPendingApproval").GetBoolean().Should().BeFalse();

        // Create supplementary approval for dead battery with proposed work item (auto-calculates estimatedAmount = 100k)
        var batteryQuote = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/approvals", new
        {
            title = "Replace dead battery",
            description = "Battery health is 0%, customer confirmation required",
            proposedWorkItems = new[]
            {
                new { itemType = "Service", title = "Battery replacement labor", quantity = 1m, unitPrice = 100000m }
            }
        });
        batteryQuote.StatusCode.Should().Be(HttpStatusCode.Created);
        var batteryApprovalId = await ReadTicketIdAsync(batteryQuote);

        var approvalsList = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/service-tickets/{ticketId}/approvals");
        var createdApproval = approvalsList.EnumerateArray().First(a => a.GetProperty("id").GetInt32() == batteryApprovalId);
        createdApproval.GetProperty("estimatedAmount").GetDecimal().Should().Be(100000m);
        createdApproval.GetProperty("status").GetString().Should().Be("Pending");

        // While pending, ticket stays Paid (350k), forward transitions blocked, Blocked status still allowed
        var duringPending = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/service-tickets/{ticketId}");
        duringPending.GetProperty("hasPendingApproval").GetBoolean().Should().BeTrue();
        duringPending.GetProperty("totalAmount").GetDecimal().Should().Be(350000m);
        duringPending.GetProperty("paymentStatus").GetString().Should().Be("Paid");

        var readyForPickup = await GetStatusAsync("Ready for Pickup");
        var waitingSparepart = await GetStatusAsync("Waiting Approval");
        var completed = await GetStatusAsync("Completed");

        var blockedForward = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/change-status", new { statusId = readyForPickup.Id, overridePaymentBlock = false });
        blockedForward.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await blockedForward.Content.ReadAsStringAsync()).Should().Contain("Pending customer approval");

        var allowBlocked = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/change-status", new { statusId = waitingSparepart.Id, reason = "Awaiting battery approval", overridePaymentBlock = false });
        allowBlocked.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Approve supplementary quote -> linked item promoted to Approved, Total becomes 450k, Paid auto-reverts to Partial!
        (await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/approvals/{batteryApprovalId}/approve", new { note = "Customer approved via WhatsApp" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterApprove = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/service-tickets/{ticketId}/payment");
        afterApprove.GetProperty("workItemsSubtotal").GetDecimal().Should().Be(450000m);
        afterApprove.GetProperty("totalAmount").GetDecimal().Should().Be(450000m);
        afterApprove.GetProperty("amountPaid").GetDecimal().Should().Be(350000m);
        afterApprove.GetProperty("remainingBalance").GetDecimal().Should().Be(100000m);
        afterApprove.GetProperty("status").GetString().Should().Be("Partial");

        // Consume battery stock (500k) -> Total becomes 950k, Remaining balance becomes 600k
        var batteryId = await SeedInventoryItemAsync("Lithium Battery 4-Cell", fixedSalePrice: 500000m, onHandQuantity: 2m);
        var reserve = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/inventory/reserve", new { inventoryItemId = batteryId, quantity = 1m });
        var usageId = await ReadTicketIdAsync(reserve);
        (await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/inventory/{usageId}/consume", new { quantity = 1m, reason = "Installed replacement" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterBattery = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/service-tickets/{ticketId}/payment");
        afterBattery.GetProperty("totalAmount").GetDecimal().Should().Be(950000m);
        afterBattery.GetProperty("remainingBalance").GetDecimal().Should().Be(600000m);
        afterBattery.GetProperty("status").GetString().Should().Be("Partial");

        // Move to Ready for Pickup is allowed while Partial, Completed is blocked until 600k is paid
        (await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/change-status", new { statusId = readyForPickup.Id, overridePaymentBlock = false }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/change-status", new { statusId = completed.Id, overridePaymentBlock = false }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Customer pays remaining 600k -> Paid -> Completed succeeds
        var settleRemaining = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/payment/transactions", new { entryType = "Payment", amount = 600000m, method = "Transfer" });
        settleRemaining.StatusCode.Should().Be(HttpStatusCode.OK);
        (await settleRemaining.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString().Should().Be("Paid");

        (await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/change-status", new { statusId = completed.Id, overridePaymentBlock = false }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task BillingRefundVoidOverrideWaived_AndCancelAutoCancelsPendingApprovals()
    {
        var ticketId = await CreateTicketAsync(isPreApproved: true);
        await ResetRulesAsync();

        // Add work (100k) + consume part (300k) = 400k, pay in full -> Paid
        (await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/work-items", new { itemType = "Service", title = "Diagnostic & install", quantity = 1m, unitPrice = 100000m }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        var partId = await SeedInventoryItemAsync("NVMe 512GB", fixedSalePrice: 300000m, onHandQuantity: 3m);
        var reserve = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/inventory/reserve", new { inventoryItemId = partId, quantity = 1m });
        var usageId = await ReadTicketIdAsync(reserve);
        (await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/inventory/{usageId}/consume", new { quantity = 1m, reason = "Installed NVMe" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var pay = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/payment/transactions", new { entryType = "Payment", amount = 400000m, method = "Cash" });
        pay.StatusCode.Should().Be(HttpStatusCode.OK);
        var paymentTxId = (await pay.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("transactions")[0]
            .GetProperty("id")
            .GetInt32();

        // Return part -> Total drops to 100k, Paid = 400k, OverpaidAmount = 300k!
        (await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/inventory/{usageId}/return", new { quantity = 1m, reason = "Customer reused existing SSD" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var overpaid = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/service-tickets/{ticketId}/payment");
        overpaid.GetProperty("totalAmount").GetDecimal().Should().Be(100000m);
        overpaid.GetProperty("amountPaid").GetDecimal().Should().Be(400000m);
        overpaid.GetProperty("overpaidAmount").GetDecimal().Should().Be(300000m);
        overpaid.GetProperty("status").GetString().Should().Be("Paid");

        // Record Refund of 300k -> OverpaidAmount clears to 0
        var refund = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/payment/transactions", new { entryType = "Refund", amount = 300000m, method = "Transfer", notes = "Refund for returned SSD" });
        refund.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterRefund = await refund.Content.ReadFromJsonAsync<JsonElement>();
        afterRefund.GetProperty("amountPaid").GetDecimal().Should().Be(100000m);
        afterRefund.GetProperty("overpaidAmount").GetDecimal().Should().Be(0m);
        afterRefund.GetProperty("status").GetString().Should().Be("Paid");
        var refundTxId = afterRefund.GetProperty("transactions")
            .EnumerateArray()
            .First(t => t.GetProperty("entryType").GetString() == "Refund")
            .GetProperty("id")
            .GetInt32();

        // Cannot void original 400k payment while 300k refund depends on it
        var blockedVoid = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/payment/transactions/{paymentTxId}/void", new { reason = "Wrong amount" });
        blockedVoid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Void refund first, then void payment -> amountPaid back to 0, status Unpaid
        (await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/payment/transactions/{refundTxId}/void", new { reason = "Re-entering full refund" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var afterVoidPayment = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/payment/transactions/{paymentTxId}/void", new { reason = "Entered on wrong ticket" });
        afterVoidPayment.StatusCode.Should().Be(HttpStatusCode.OK);
        var voidedDto = await afterVoidPayment.Content.ReadFromJsonAsync<JsonElement>();
        voidedDto.GetProperty("amountPaid").GetDecimal().Should().Be(0m);
        voidedDto.GetProperty("status").GetString().Should().Be("Unpaid");

        // Discount, Manual Override, Clear Manual Override, and Waived
        var withDiscount = await _client.PutAsJsonAsync($"/api/v1/service-tickets/{ticketId}/billing", new
        {
            discountAmount = 20000m,
            manualTotalAmount = (decimal?)null,
            clearManualOverride = false,
            isWaived = false,
            notes = "Repeat customer discount"
        });
        withDiscount.StatusCode.Should().Be(HttpStatusCode.OK);
        (await withDiscount.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totalAmount").GetDecimal().Should().Be(80000m);

        var withOverride = await _client.PutAsJsonAsync($"/api/v1/service-tickets/{ticketId}/billing", new
        {
            discountAmount = 20000m,
            manualTotalAmount = 95000m,
            clearManualOverride = false,
            isWaived = false,
            notes = "Fixed bundle fee"
        });
        var overrideDto = await withOverride.Content.ReadFromJsonAsync<JsonElement>();
        overrideDto.GetProperty("isTotalManuallyOverridden").GetBoolean().Should().BeTrue();
        overrideDto.GetProperty("totalAmount").GetDecimal().Should().Be(95000m);

        var clearedOverride = await _client.PutAsJsonAsync($"/api/v1/service-tickets/{ticketId}/billing", new
        {
            discountAmount = 20000m,
            manualTotalAmount = (decimal?)null,
            clearManualOverride = true,
            isWaived = false
        });
        var clearedDto = await clearedOverride.Content.ReadFromJsonAsync<JsonElement>();
        clearedDto.GetProperty("isTotalManuallyOverridden").GetBoolean().Should().BeFalse();
        clearedDto.GetProperty("totalAmount").GetDecimal().Should().Be(80000m);

        var waived = await _client.PutAsJsonAsync($"/api/v1/service-tickets/{ticketId}/billing", new
        {
            discountAmount = 20000m,
            manualTotalAmount = (decimal?)null,
            clearManualOverride = false,
            isWaived = true,
            notes = "Warranty goodwill waiver"
        });
        waived.StatusCode.Should().Be(HttpStatusCode.OK);
        (await waived.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString().Should().Be("Waived");

        // Adding another work item preserves Waived status until un-waived or payment added
        (await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/work-items", new { itemType = "Fee", title = "Bench fee", quantity = 1m, unitPrice = 25000m }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        var stillWaived = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/service-tickets/{ticketId}/payment");
        stillWaived.GetProperty("status").GetString().Should().Be("Waived");
        stillWaived.GetProperty("totalAmount").GetDecimal().Should().Be(105000m);

        // Cancelling another ticket auto-cancels open Pending approvals & linked PendingApproval work items
        var ticketToCancel = await CreateTicketAsync(isPreApproved: true);
        var pendingQuote = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketToCancel}/approvals", new
        {
            title = "Motherboard repair",
            description = "BGA rework",
            proposedWorkItems = new[] { new { itemType = "Service", title = "BGA rework", quantity = 1m, unitPrice = 600000m } }
        });
        var pendingApprovalId = await ReadTicketIdAsync(pendingQuote);
        var cancelledStatus = await GetStatusAsync("Cancelled");

        (await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketToCancel}/change-status", new { statusId = cancelledStatus.Id, reason = "Too expensive", overridePaymentBlock = false }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var cancelledApprovals = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/service-tickets/{ticketToCancel}/approvals");
        cancelledApprovals.EnumerateArray()
            .First(a => a.GetProperty("id").GetInt32() == pendingApprovalId)
            .GetProperty("status")
            .GetString()
            .Should()
            .Be("Cancelled");

        var cancelledWorkItems = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/service-tickets/{ticketToCancel}/work-items");
        cancelledWorkItems.EnumerateArray().Should().OnlyContain(item => item.GetProperty("status").GetString() == "Cancelled");
    }

    [Fact]
    public async Task ReportsAndWhatsApp_ReflectRecordedAtLedgerPaymentsAndPlaceholders()
    {
        var todayUtc = DateTimeOffset.UtcNow.UtcDateTime.Date;
        var todayParam = todayUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        var baselineReport = await _client.GetFromJsonAsync<ReportsEndpoints.ReportSummaryDto>($"/api/v1/reports/summary?from={todayParam}&to={todayParam}&groupBy=daily");
        var baselineRevenue = baselineReport!.Totals.TotalRevenue;

        // Create ticket backdated 45 days ago -> verify today's ledger transactions STILL attribute revenue to today
        var ticketId = await CreateTicketAsync(isPreApproved: true);
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ticket = await context.ServiceTickets.FirstAsync(t => t.Id == ticketId);
            ticket.CreatedAt = DateTimeOffset.UtcNow.AddDays(-45);
            await context.SaveChangesAsync();
        }

        (await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/work-items", new { itemType = "Service", title = "Full board repair", quantity = 1m, unitPrice = 950000m }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        // Backdated payment 45 days ago -> excluded from today's revenue
        (await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/payment/transactions", new
        {
            entryType = "Payment",
            amount = 50000m,
            method = "Cash",
            recordedAt = DateTimeOffset.UtcNow.AddDays(-45)
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        // Today's payment (300k) -> included
        (await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/payment/transactions", new
        {
            entryType = "Payment",
            amount = 300000m,
            method = "Transfer"
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        // Voided payment today (500k) -> excluded
        var toVoidResp = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/payment/transactions", new
        {
            entryType = "Payment",
            amount = 500000m,
            method = "QRIS"
        });
        toVoidResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var voidTxId = (await toVoidResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("transactions")
            .EnumerateArray()
            .First(t => t.GetProperty("transactionAmount").GetDecimal() == 500000m)
            .GetProperty("id")
            .GetInt32();
        (await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/payment/transactions/{voidTxId}/void", new { reason = "Duplicate entry" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Today's refund (25k) -> subtracts from revenue (net delta = +275k)
        (await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/payment/transactions", new
        {
            entryType = "Refund",
            amount = 25000m,
            method = "Cash"
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedReport = await _client.GetFromJsonAsync<ReportsEndpoints.ReportSummaryDto>($"/api/v1/reports/summary?from={todayParam}&to={todayParam}&groupBy=daily");
        updatedReport!.Totals.TotalRevenue.Should().Be(baselineRevenue + 275000m);

        // Create custom WhatsApp template covering financial & approval placeholders
        var templateKey = $"billing_wa_{Guid.NewGuid():N}";
        var createTemplate = await _client.PostAsJsonAsync("/api/v1/notification-templates", new
        {
            key = templateKey,
            channel = "WhatsApp",
            provider = "manual",
            locale = "en",
            name = "Billing Summary Test",
            body = "Est={{estimated_cost}}|Final={{final_cost}}|Paid={{amount_paid}}|Bal={{remaining_balance}}|Appr={{approval_status}}",
            isActive = true
        });
        createTemplate.StatusCode.Should().Be(HttpStatusCode.Created);

        var waResponse = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/whatsapp-message", new
        {
            templateKey,
            locale = "en"
        });
        waResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var waDto = await waResponse.Content.ReadFromJsonAsync<NotificationTemplateEndpoints.GeneratedWhatsAppDto>();
        waDto.Should().NotBeNull();
        // Net paid = 50k + 300k - 25k = 325k; total = 950k; remaining = 625k
        waDto!.Message.Should().Contain("950");
        waDto.Message.Should().Contain("325");
        waDto.Message.Should().Contain("625");
        waDto.Message.Should().Contain("Appr=Approved");

        // Ticket created without pre-approval or approval-required should render Appr=None
        var unapprovedTicketId = await CreateTicketAsync(isPreApproved: false);
        var noneWaResponse = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{unapprovedTicketId}/whatsapp-message", new
        {
            templateKey,
            locale = "en"
        });
        noneWaResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var noneWaDto = await noneWaResponse.Content.ReadFromJsonAsync<NotificationTemplateEndpoints.GeneratedWhatsAppDto>();
        noneWaDto!.Message.Should().Contain("Appr=None");
    }

    [Fact]
    public async Task DashboardSummary_WaitingApprovalIncludesTicketsWithPendingApprovalsEvenWhenPreApproved()
    {
        var baseline = await _client.GetFromJsonAsync<JsonElement>("/api/v1/dashboard/summary");
        var beforeWaiting = baseline.GetProperty("waitingApproval").GetInt32();

        var ticketId = await CreateTicketAsync(isPreApproved: true);
        var approvalResp = await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/approvals", new
        {
            title = "Supplementary display cable",
            description = "Confirm cable replacement",
            estimatedAmount = 120000m
        });
        approvalResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var approvalId = await ReadTicketIdAsync(approvalResp);

        var duringPending = await _client.GetFromJsonAsync<JsonElement>("/api/v1/dashboard/summary");
        duringPending.GetProperty("waitingApproval").GetInt32().Should().Be(beforeWaiting + 1);

        (await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/approvals/{approvalId}/approve", new { note = "Approved" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterResolve = await _client.GetFromJsonAsync<JsonElement>("/api/v1/dashboard/summary");
        afterResolve.GetProperty("waitingApproval").GetInt32().Should().Be(beforeWaiting);
    }

    [Fact]
    public async Task Receipt_ReflectsPrintTemplateShowPaymentSummarySetting()
    {
        var ticketId = await CreateTicketAsync(isPreApproved: true);
        (await _client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/work-items", new { itemType = "Service", title = "Cleaning", quantity = 1m, unitPrice = 50000m }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var currentSettings = await _client.GetFromJsonAsync<PrintTemplateSettings>("/api/v1/settings/print-template");
        try
        {
            var disableResponse = await _client.PutAsJsonAsync("/api/v1/settings/print-template", new
            {
                termsAndConditions = currentSettings!.TermsAndConditions,
                showPhysicalCondition = currentSettings.ShowPhysicalCondition,
                showAccessories = currentSettings.ShowAccessories,
                showPhotoNotes = currentSettings.ShowPhotoNotes,
                showQrCode = currentSettings.ShowQrCode,
                showPhoneNumber = currentSettings.ShowPhoneNumber,
                showPaymentSummary = false
            });
            disableResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var hiddenReceipt = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/service-tickets/{ticketId}/receipt");
            hiddenReceipt.GetProperty("showPaymentSummary").GetBoolean().Should().BeFalse();

            var enableResponse = await _client.PutAsJsonAsync("/api/v1/settings/print-template", new
            {
                termsAndConditions = currentSettings.TermsAndConditions,
                showPhysicalCondition = currentSettings.ShowPhysicalCondition,
                showAccessories = currentSettings.ShowAccessories,
                showPhotoNotes = currentSettings.ShowPhotoNotes,
                showQrCode = currentSettings.ShowQrCode,
                showPhoneNumber = currentSettings.ShowPhoneNumber,
                showPaymentSummary = true
            });
            enableResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var visibleReceipt = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/service-tickets/{ticketId}/receipt");
            visibleReceipt.GetProperty("showPaymentSummary").GetBoolean().Should().BeTrue();
        }
        finally
        {
            await _client.PutAsJsonAsync("/api/v1/settings/print-template", new
            {
                termsAndConditions = currentSettings!.TermsAndConditions,
                showPhysicalCondition = currentSettings.ShowPhysicalCondition,
                showAccessories = currentSettings.ShowAccessories,
                showPhotoNotes = currentSettings.ShowPhotoNotes,
                showQrCode = currentSettings.ShowQrCode,
                showPhoneNumber = currentSettings.ShowPhoneNumber,
                showPaymentSummary = currentSettings.ShowPaymentSummary
            });
        }
    }

    private async Task<int> SeedInventoryItemAsync(string name, decimal fixedSalePrice, decimal onHandQuantity)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var shopId = await GetDefaultShopIdAsync(context);
        var item = new InventoryItem
        {
            ShopId = shopId,
            Sku = $"TST-{Guid.NewGuid():N}"[..16],
            Name = name,
            Unit = "pcs",
            CostPrice = fixedSalePrice * 0.6m,
            SalePrice = fixedSalePrice,
            PurchasePrice = fixedSalePrice * 0.6m,
            FixedSalePrice = fixedSalePrice,
            OnHandQuantity = onHandQuantity,
            IsActive = true
        };
        context.InventoryItems.Add(item);
        await context.SaveChangesAsync();
        return item.Id;
    }

    private async Task UpdateInventoryFixedSalePriceAsync(int itemId, decimal newFixedSalePrice)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var item = await context.InventoryItems.FirstAsync(row => row.Id == itemId);
        item.FixedSalePrice = newFixedSalePrice;
        item.SalePrice = newFixedSalePrice;
        await context.SaveChangesAsync();
    }

    private async Task<int> CreateTicketAsync(bool approvalRequired = false, bool isPreApproved = false)
    {
        var customerId = await CreateCustomerAsync();
        var response = await CreateTicketResponseAsync(customerId, approvalRequired, isPreApproved);

        response.EnsureSuccessStatusCode();
        return await ReadTicketIdAsync(response);
    }

    private async Task<HttpResponseMessage> CreateTicketResponseAsync(int? customerId = null, bool approvalRequired = false, bool isPreApproved = false, string? idempotencyKey = null, string? overrideReason = null, int intakePhotoCount = 0)
    {
        customerId ??= await CreateCustomerAsync();
        return await _client.PostAsJsonAsync("/api/v1/service-tickets", new
        {
            customerId,
            deviceId = (int?)null,
            newDevice = new
            {
                deviceType = "Laptop",
                brand = "Lenovo",
                model = $"ThinkPad {Guid.NewGuid():N}",
                serialNumber = $"SN-{Guid.NewGuid():N}",
                identifierLabel = "QA",
                notes = "Integration test"
            },
            complaint = "Unit cannot boot",
            initialDiagnosis = "Pending inspection",
            priority = "Normal",
            assignedTechnicianUserId = (int?)null,
            approvalRequired,
            isPreApproved,
            createdAt = DateTimeOffset.UtcNow,
            idempotencyKey,
            bannedCustomerOverrideReason = overrideReason,
            intakePhotoCount
        });
    }

    private static async Task<int> ReadTicketIdAsync(HttpResponseMessage response)
    {
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return json.RootElement.GetProperty("id").GetInt32();
    }

    private async Task<string> GetPublicTrackingTokenAsync(int ticketId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.ServiceTickets
            .Where(ticket => ticket.Id == ticketId)
            .Select(ticket => ticket.PublicTrackingToken)
            .FirstAsync();
    }

    private async Task SetPublicPhotoSettingAsync(int ticketId, bool enabled)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var shopId = await context.ServiceTickets
            .Where(ticket => ticket.Id == ticketId)
            .Select(ticket => ticket.ShopId)
            .FirstAsync();
        await SetShopSettingAsync(context, shopId, ShopProfileSettingKeys.ShowPhotoEvidenceOnPublicTracking, enabled);
    }

    private async Task SetDefaultShopSettingAsync(string key, bool enabled)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var shopId = await GetDefaultShopIdAsync(context);
        await SetShopSettingAsync(context, shopId, key, enabled);
    }

    private static async Task SetShopSettingAsync(AppDbContext context, int shopId, string key, bool enabled)
    {
        var setting = await context.AppSettings.FirstOrDefaultAsync(setting =>
            setting.ShopId == shopId &&
            setting.Key == key);
        if (setting is null)
        {
            context.AppSettings.Add(new AppSetting
            {
                ShopId = shopId,
                Key = key,
                Value = enabled.ToString()
            });
        }
        else
        {
            setting.Value = enabled.ToString();
        }
        await context.SaveChangesAsync();
    }

    private async Task<int> CreateCustomerAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/customers", new CreateCustomer.Command(
            $"Ticket Test {Guid.NewGuid():N}",
            $"+628{Random.Shared.Next(100000000, 999999999)}",
            null,
            null,
            null));

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateCustomer.Result>();
        return result!.Id;
    }

    private async Task<HttpResponseMessage> UploadAsync(int ticketId, string mimeType, string fileName, bool isPublicVisible = false)
    {
        using var form = new MultipartFormDataContent();
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        form.Add(file, "file", fileName);
        form.Add(new StringContent("Other"), "attachmentType");
        form.Add(new StringContent(isPublicVisible.ToString()), "isPublicVisible");
        return await _client.PostAsync($"/api/v1/service-tickets/{ticketId}/attachments", form);
    }

    private async Task<ServiceStatus> GetStatusAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var shopId = await GetDefaultShopIdAsync(context);
        return await context.ServiceStatuses.AsNoTracking().FirstAsync(status => status.ShopId == shopId && status.Name == name);
    }

    private async Task ResetRulesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var shopId = await GetDefaultShopIdAsync(context);
        var rules = await context.StatusTransitionRules.Where(rule => rule.ShopId == shopId).ToListAsync();
        context.StatusTransitionRules.RemoveRange(rules);
        await context.SaveChangesAsync();
    }

    private async Task<(ServiceStatus FromStatus, ServiceStatus ToStatus)> ResetRulesAndAddRuleAsync(
        string from,
        string to,
        bool requiresPaymentCompleted = false)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var shopId = await GetDefaultShopIdAsync(context);
        var rules = await context.StatusTransitionRules.Where(rule => rule.ShopId == shopId).ToListAsync();
        context.StatusTransitionRules.RemoveRange(rules);
        var fromStatus = await context.ServiceStatuses.FirstAsync(status => status.ShopId == shopId && status.Name == from);
        var toStatus = await context.ServiceStatuses.FirstAsync(status => status.ShopId == shopId && status.Name == to);
        context.StatusTransitionRules.Add(new StatusTransitionRule
        {
            ShopId = shopId,
            FromStatusId = fromStatus.Id,
            ToStatusId = toStatus.Id,
            RequiresPaymentCompleted = requiresPaymentCompleted,
            IsActive = true
        });
        await context.SaveChangesAsync();
        return (fromStatus, toStatus);
    }

    private async Task SetTicketStatusAsync(int ticketId, string statusName)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var shopId = await GetDefaultShopIdAsync(context);
        var status = await context.ServiceStatuses.FirstAsync(status => status.ShopId == shopId && status.Name == statusName);
        var ticket = await context.ServiceTickets.FirstAsync(ticket => ticket.Id == ticketId);
        ticket.StatusId = status.Id;
        await context.SaveChangesAsync();
    }

    private async Task<int> CreateOtherShopTicketAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var received = await context.ServiceStatuses.AsNoTracking().FirstAsync(status => status.Name == "Received");
        var orgId = await context.Organizations.Select(o => o.Id).FirstAsync();
        var shop = new Shop { OrganizationId = orgId, Name = $"Other {Guid.NewGuid():N}", Slug = $"other-{Guid.NewGuid():N}", IsActive = true };
        context.Shops.Add(shop);
        await context.SaveChangesAsync();

        var status = new ServiceStatus { ShopId = shop.Id, Name = "Received", Color = "#64748b", SortOrder = 1, IsInitial = true };
        var customer = new Customer { ShopId = shop.Id, FullName = "Other Shop Customer", PhoneNumber = "+628111111111", CreatedAt = DateTimeOffset.UtcNow };
        var device = new Device { ShopId = shop.Id, Customer = customer, DeviceType = "Laptop", Brand = "Dell", Model = "Latitude", CreatedAt = DateTimeOffset.UtcNow };
        var ticket = new ServiceTicket
        {
            ShopId = shop.Id,
            Customer = customer,
            Device = device,
            Status = status,
            TicketNumber = $"OTHER-{Guid.NewGuid():N}",
            PublicTrackingToken = Guid.NewGuid().ToString("N"),
            CustomerNameSnapshot = customer.FullName,
            CustomerPhoneSnapshot = customer.PhoneNumber,
            DeviceSnapshot = "Dell Latitude",
            Complaint = "Other shop ticket",
            Priority = ServiceTicketPriority.Normal,
            PaymentStatus = PaymentStatus.Unpaid,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.ServiceStatuses.Add(status);
        context.Customers.Add(customer);
        context.Devices.Add(device);
        context.ServiceTickets.Add(ticket);
        await context.SaveChangesAsync();
        received.Id.Should().BeGreaterThan(0);
        return ticket.Id;
    }

    private static async Task<int> GetDefaultShopIdAsync(AppDbContext context)
        => await context.Shops.OrderBy(shop => shop.Id).Select(shop => shop.Id).FirstAsync();
}
