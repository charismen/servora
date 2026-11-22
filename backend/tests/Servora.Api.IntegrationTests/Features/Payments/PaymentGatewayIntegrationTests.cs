using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Servora.Api.Features.Customers;
using Servora.Api.Features.Devices;
using Servora.Api.Features.Payments;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Infrastructure.Data;
using Xunit;

namespace Servora.Api.IntegrationTests.Features.Payments;

public class PaymentGatewayIntegrationTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly IntegrationTestWebAppFactory _factory;

    public PaymentGatewayIntegrationTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
    }

    private sealed class FakeGatewayState
    {
        public int Sequence;
        public ConcurrentBag<string> CancelledProviderIds { get; } = [];
        public ConcurrentDictionary<string, GatewayCheckoutStatusResult> ForcedStatuses { get; } = new();
    }

    private sealed class FakeXenditGateway(FakeGatewayState state) : IPaymentGateway
    {
        public PaymentGatewayProvider Provider => PaymentGatewayProvider.Xendit;

        public Task<GatewayCreateCheckoutResult> CreateCheckoutAsync(
            GatewayCredentials credentials,
            GatewayCreateCheckoutRequest request,
            CancellationToken cancellationToken)
        {
            var seq = Interlocked.Increment(ref state.Sequence);
            var checkoutId = $"inv_fake_{seq}_{Guid.NewGuid():N}"[..24];
            var url = $"https://checkout.fake-xendit.test/invoices/{checkoutId}";
            return Task.FromResult(new GatewayCreateCheckoutResult(
                checkoutId,
                url,
                null,
                DateTimeOffset.UtcNow.AddHours(request.ExpiresInHours)));
        }

        public Task<GatewayCheckoutStatusResult> GetCheckoutStatusAsync(
            GatewayCredentials credentials,
            string providerCheckoutId,
            CancellationToken cancellationToken)
        {
            if (state.ForcedStatuses.TryGetValue(providerCheckoutId, out var forced))
            {
                return Task.FromResult(forced);
            }

            return Task.FromResult(new GatewayCheckoutStatusResult(PaymentCheckoutStatus.Pending, null, null, null));
        }

        public Task CancelCheckoutAsync(
            GatewayCredentials credentials,
            string providerCheckoutId,
            CancellationToken cancellationToken)
        {
            state.CancelledProviderIds.Add(providerCheckoutId);
            return Task.CompletedTask;
        }

        public Task<GatewayTestConnectionResult> TestConnectionAsync(
            GatewayCredentials credentials,
            CancellationToken cancellationToken)
            => Task.FromResult(new GatewayTestConnectionResult(true, "Fake Xendit OK"));

        public Task<GatewayWebhookVerificationResult> VerifyAndParseWebhookAsync(
            GatewayCredentials credentials,
            HttpRequest httpRequest,
            string rawBody,
            CancellationToken cancellationToken)
        {
            var token = httpRequest.Headers["x-callback-token"].FirstOrDefault();
            var webhookId = httpRequest.Headers["webhook-id"].FirstOrDefault();
            return Task.FromResult(XenditGatewayAdapter.VerifyAndParseWebhook(
                credentials.WebhookSecret,
                token,
                webhookId,
                rawBody));
        }
    }

    private (HttpClient Client, IServiceProvider Services, FakeGatewayState State) CreateClientWithFakeGateway()
    {
        var state = new FakeGatewayState();
        var webApp = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPaymentGateway>();
                services.AddSingleton(state);
                services.AddScoped<IPaymentGateway, FakeXenditGateway>();
            });
        });

        return (webApp.CreateClient(), webApp.Services, state);
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

    private static async Task<(int TicketId, string TrackingToken)> SeedTicketAsync(
        IServiceProvider services,
        int workspaceId,
        decimal totalAmount,
        string phone = "+6281234567890")
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
            FullName = "Payment Test Customer",
            PhoneNumber = phone,
            Email = "customer@example.com"
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var asset = new Device
        {
            ShopId = workspaceId,
            CustomerId = customer.Id,
            DeviceType = "Laptop",
            Brand = "Lenovo",
            Model = "ThinkPad T14",
            SerialNumber = $"SN-{Guid.NewGuid():N}"[..16]
        };
        db.Devices.Add(asset);
        await db.SaveChangesAsync();

        var token = $"trk_{Guid.NewGuid():N}";
        var ticket = new ServiceTicket
        {
            ShopId = workspaceId,
            TicketNumber = $"SRV-PAY-{Random.Shared.Next(10000, 99999)}",
            CustomerId = customer.Id,
            CustomerNameSnapshot = customer.FullName,
            CustomerPhoneSnapshot = customer.PhoneNumber,
            DeviceId = asset.Id,
            DeviceSnapshot = "Lenovo ThinkPad T14",
            StatusId = status.Id,
            Complaint = "Battery replacement",
            TotalAmount = totalAmount,
            IsTotalManuallyOverridden = true,
            PaymentStatus = PaymentStatus.Unpaid,
            PublicTrackingEnabled = true,
            PublicTrackingToken = token
        };
        db.ServiceTickets.Add(ticket);
        await db.SaveChangesAsync();

        return (ticket.Id, token);
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

    [Fact]
    public async Task InternalMaintenance_Workspace_RejectsEnablingGatewayAndCreatingLinks()
    {
        var (client, services, _) = CreateClientWithFakeGateway();
        var internalWorkspaceId = await CreateWorkspaceAsync(client, "Internal Plant Ops", "InternalMaintenance", "facility_internal");

        var getResp = await client.SendAsync(ScopedRequest(HttpMethod.Get, "/api/v1/settings/payment-gateway", internalWorkspaceId));
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var getJson = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        getJson.GetProperty("workspaceEligible").GetBoolean().Should().BeFalse();

        var putResp = await client.SendAsync(ScopedRequest(HttpMethod.Put, "/api/v1/settings/payment-gateway", internalWorkspaceId, new
        {
            provider = "Xendit",
            environment = "Sandbox",
            isEnabled = true,
            apiKey = "xnd_development_secret_1234",
            clearApiKey = false,
            webhookSecret = "cb_secret_5678",
            clearWebhookSecret = false,
            defaultLinkExpiryHours = 24,
            enablePublicTrackingPayment = true,
            requirePhoneVerificationForPublicPayment = true
        }));
        putResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var (ticketId, _) = await SeedTicketAsync(services, internalWorkspaceId, 500000m);
        var linkResp = await client.SendAsync(ScopedRequest(HttpMethod.Post, $"/api/v1/service-tickets/{ticketId}/payment-links", internalWorkspaceId, new
        {
            amount = 250000m
        }));
        linkResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PaymentLink_Supersede_IdempotentWebhook_Overpayment_And_WorkspaceIsolation_WorkEndToEnd()
    {
        var (client, services, fakeState) = CreateClientWithFakeGateway();
        var workspaceA = await CreateWorkspaceAsync(client, "Commercial Shop A", "Commercial", "it_computer");
        var workspaceB = await CreateWorkspaceAsync(client, "Commercial Shop B", "Commercial", "it_computer");

        // 1. Configure Xendit on Workspace A
        var updateResp = await client.SendAsync(ScopedRequest(HttpMethod.Put, "/api/v1/settings/payment-gateway", workspaceA, new
        {
            provider = "Xendit",
            environment = "Sandbox",
            isEnabled = true,
            apiKey = "xnd_development_key_9876",
            clearApiKey = false,
            webhookSecret = "xnd_cb_secret_4321",
            clearWebhookSecret = false,
            defaultLinkExpiryHours = 24,
            enablePublicTrackingPayment = true,
            requirePhoneVerificationForPublicPayment = true
        }));
        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var settingsJson = await updateResp.Content.ReadFromJsonAsync<JsonElement>();
        settingsJson.GetProperty("isEnabled").GetBoolean().Should().BeTrue();
        settingsJson.GetProperty("apiKeyLast4").GetString().Should().Be("9876");
        settingsJson.GetProperty("webhookSecretLast4").GetString().Should().Be("4321");
        var webhookUrl = settingsJson.GetProperty("webhookUrl").GetString()!;
        var webhookPath = new Uri(webhookUrl).PathAndQuery;

        // 2. Seed ticket (500,000 IDR) and create deposit link #1 (200,000) then replace with link #2 (300,000)
        var (ticketId, _) = await SeedTicketAsync(services, workspaceA, 500000m);

        var link1Resp = await client.SendAsync(ScopedRequest(HttpMethod.Post, $"/api/v1/service-tickets/{ticketId}/payment-links", workspaceA, new
        {
            amount = 200000m
        }));
        link1Resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var link1 = await link1Resp.Content.ReadFromJsonAsync<JsonElement>();
        var link1CheckoutId = link1.GetProperty("providerCheckoutId").GetString()!;
        var link1ExtRef = link1.GetProperty("externalReference").GetString()!;

        var link2Resp = await client.SendAsync(ScopedRequest(HttpMethod.Post, $"/api/v1/service-tickets/{ticketId}/payment-links", workspaceA, new
        {
            amount = 300000m
        }));
        link2Resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var link2 = await link2Resp.Content.ReadFromJsonAsync<JsonElement>();
        var link2CheckoutId = link2.GetProperty("providerCheckoutId").GetString()!;
        var link2ExtRef = link2.GetProperty("externalReference").GetString()!;
        var link2Url = link2.GetProperty("paymentUrl").GetString()!;

        fakeState.CancelledProviderIds.Should().Contain(link1CheckoutId);

        // Verify Workspace B cannot access Workspace A's ticket payment links
        var crossTenantResp = await client.SendAsync(ScopedRequest(HttpMethod.Get, $"/api/v1/service-tickets/{ticketId}/payment-links", workspaceB));
        crossTenantResp.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Enable ShowPaymentQrCode and verify Receipt returns activePaymentUrl
        var printResp = await client.SendAsync(ScopedRequest(HttpMethod.Put, "/api/v1/settings/print-template", workspaceA, new
        {
            termsAndConditions = "Standard terms",
            showPhysicalCondition = true,
            showAccessories = true,
            showPhotoNotes = true,
            showQrCode = true,
            showPhoneNumber = true,
            showPaymentSummary = true,
            showPaymentQrCode = true
        }));
        printResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var receiptResp = await client.SendAsync(ScopedRequest(HttpMethod.Get, $"/api/v1/service-tickets/{ticketId}/receipt", workspaceA));
        receiptResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var receiptJson = await receiptResp.Content.ReadFromJsonAsync<JsonElement>();
        receiptJson.GetProperty("showPaymentQrCode").GetBoolean().Should().BeTrue();
        receiptJson.GetProperty("activePaymentUrl").GetString().Should().Be(link2Url);

        // 3. Reject webhook with invalid x-callback-token
        var badWebhookReq = new HttpRequestMessage(HttpMethod.Post, webhookPath)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                id = link2CheckoutId,
                external_id = link2ExtRef,
                status = "PAID"
            }), Encoding.UTF8, "application/json")
        };
        badWebhookReq.Headers.Add("x-callback-token", "wrong-secret");
        var badWebhookResp = await client.SendAsync(badWebhookReq);
        badWebhookResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 4. Deliver valid paid webhook for link #2 (300,000) -> ticket becomes Partial
        var paidPayload = JsonSerializer.Serialize(new
        {
            id = link2CheckoutId,
            external_id = link2ExtRef,
            status = "PAID",
            payment_method = "QR_CODE",
            payment_channel = "QRIS",
            paid_at = DateTimeOffset.UtcNow.ToString("O")
        });

        HttpRequestMessage BuildValidWebhook(string body, string webhookId)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, webhookPath)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            req.Headers.Add("x-callback-token", "xnd_cb_secret_4321");
            req.Headers.Add("webhook-id", webhookId);
            return req;
        }

        var wh1 = await client.SendAsync(BuildValidWebhook(paidPayload, "whk_evt_1"));
        wh1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Replay exact same webhook event -> must be idempotent (no double-counting)
        var whDup = await client.SendAsync(BuildValidWebhook(paidPayload, "whk_evt_1"));
        whDup.StatusCode.Should().Be(HttpStatusCode.OK);

        // Check ticket payment state: 300,000 paid, 200,000 remaining, Partial, UpdatedByUserId is null
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ticket = await db.ServiceTickets.AsNoTracking().FirstAsync(x => x.Id == ticketId);
            ticket.AmountPaid.Should().Be(300000m);
            ticket.PaymentStatus.Should().Be(PaymentStatus.Partial);

            var records = await db.PaymentRecords.AsNoTracking()
                .Where(x => x.ServiceTicketId == ticketId)
                .ToListAsync();
            records.Should().HaveCount(1);
            records[0].UpdatedByUserId.Should().BeNull();
            records[0].Method.Should().Contain("Xendit");
        }

        // 5. Late payment arrives on superseded link #1 (200,000) + create extra link to trigger OverpaidAmount
        var latePayload = JsonSerializer.Serialize(new
        {
            id = link1CheckoutId,
            external_id = link1ExtRef,
            status = "PAID",
            payment_method = "BANK_TRANSFER",
            payment_channel = "BCA",
            paid_at = DateTimeOffset.UtcNow.ToString("O")
        });
        var whLate = await client.SendAsync(BuildValidWebhook(latePayload, "whk_evt_late_1"));
        whLate.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ticket = await db.ServiceTickets.AsNoTracking().FirstAsync(x => x.Id == ticketId);
            ticket.AmountPaid.Should().Be(500000m);
            ticket.PaymentStatus.Should().Be(PaymentStatus.Paid);

            var intent1 = await db.PaymentCheckoutIntents.AsNoTracking().FirstAsync(x => x.ExternalReference == link1ExtRef);
            intent1.Status.Should().Be(PaymentCheckoutStatus.Paid);
            intent1.SettledPaymentRecordId.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task ManualSync_ExpirySweep_And_PublicTrackingPhoneGate_WorkEndToEnd()
    {
        var (client, services, fakeState) = CreateClientWithFakeGateway();
        var workspaceId = await CreateWorkspaceAsync(client, "Field HVAC Co", "Hybrid", "hvac_ac");

        var putResp = await client.SendAsync(ScopedRequest(HttpMethod.Put, "/api/v1/settings/payment-gateway", workspaceId, new
        {
            provider = "Xendit",
            environment = "Sandbox",
            isEnabled = true,
            apiKey = "xnd_development_sync_1111",
            clearApiKey = false,
            webhookSecret = "xnd_cb_sync_2222",
            clearWebhookSecret = false,
            defaultLinkExpiryHours = 12,
            enablePublicTrackingPayment = true,
            requirePhoneVerificationForPublicPayment = true
        }));
        putResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var (ticketId, trackingToken) = await SeedTicketAsync(services, workspaceId, 450000m, "+6281199887766");

        // 1. Public tracking GET hides remaining balance while phone verification is required
        var trackResp = await client.GetAsync($"/api/v1/track/{trackingToken}");
        trackResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var trackJson = await trackResp.Content.ReadFromJsonAsync<JsonElement>();
        trackJson.GetProperty("onlinePaymentEnabled").GetBoolean().Should().BeTrue();
        trackJson.GetProperty("paymentRequiresPhoneVerification").GetBoolean().Should().BeTrue();
        trackJson.GetProperty("remainingBalance").ValueKind.Should().Be(JsonValueKind.Null);

        // Wrong phone last-4 rejected
        var badVerify = await client.PostAsJsonAsync($"/api/v1/track/{trackingToken}/payment-summary", new { phoneLastFour = "0000" });
        badVerify.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Correct phone last-4 ("7766") returns summary & creates checkout link
        var okVerify = await client.PostAsJsonAsync($"/api/v1/track/{trackingToken}/payment-summary", new { phoneLastFour = "7766" });
        okVerify.StatusCode.Should().Be(HttpStatusCode.OK);
        var summaryJson = await okVerify.Content.ReadFromJsonAsync<JsonElement>();
        summaryJson.GetProperty("remainingBalance").GetDecimal().Should().Be(450000m);

        var checkoutResp = await client.PostAsJsonAsync($"/api/v1/track/{trackingToken}/checkout", new { phoneLastFour = "7766" });
        checkoutResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkoutJson = await checkoutResp.Content.ReadFromJsonAsync<JsonElement>();
        var paymentUrl = checkoutJson.GetProperty("paymentUrl").GetString()!;
        paymentUrl.Should().StartWith("https://checkout.fake-xendit.test/invoices/");

        // Repeat checkout call reuses the same active pending link
        var checkoutResp2 = await client.PostAsJsonAsync($"/api/v1/track/{trackingToken}/checkout", new { phoneLastFour = "7766" });
        checkoutResp2.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkoutJson2 = await checkoutResp2.Content.ReadFromJsonAsync<JsonElement>();
        checkoutJson2.GetProperty("paymentUrl").GetString().Should().Be(paymentUrl);

        // 2. Manual Sync Status settles the link without a webhook
        int intentId;
        string providerCheckoutId;
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var intent = await db.PaymentCheckoutIntents.AsNoTracking()
                .FirstAsync(x => x.ServiceTicketId == ticketId && x.Status == PaymentCheckoutStatus.Pending);
            intentId = intent.Id;
            providerCheckoutId = intent.ProviderCheckoutId!;
        }

        fakeState.ForcedStatuses[providerCheckoutId] = new GatewayCheckoutStatusResult(
            PaymentCheckoutStatus.Paid,
            "QRIS",
            DateTimeOffset.UtcNow,
            null);

        var syncResp = await client.SendAsync(ScopedRequest(
            HttpMethod.Post,
            $"/api/v1/service-tickets/{ticketId}/payment-links/{intentId}/sync",
            workspaceId));
        syncResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var syncedJson = await syncResp.Content.ReadFromJsonAsync<JsonElement>();
        syncedJson.GetProperty("status").GetString().Should().Be("Paid");

        // 3. Hangfire expiry sweep expires past-due Pending intents
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var expiredIntent = new PaymentCheckoutIntent
            {
                ShopId = workspaceId,
                ServiceTicketId = ticketId,
                Provider = PaymentGatewayProvider.Xendit,
                Environment = PaymentGatewayEnvironment.Sandbox,
                Status = PaymentCheckoutStatus.Pending,
                Amount = 100000m,
                CurrencyCode = "IDR",
                ExternalReference = $"srv-expired-{Guid.NewGuid():N}"[..24],
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(-2)
            };
            db.PaymentCheckoutIntents.Add(expiredIntent);
            await db.SaveChangesAsync();

            var job = scope.ServiceProvider.GetRequiredService<PaymentIntentExpiryJob>();
            var swept = await job.ExpirePastDueIntentsAsync();
            swept.Should().BeGreaterThanOrEqualTo(1);

            var reloaded = await db.PaymentCheckoutIntents.AsNoTracking().FirstAsync(x => x.Id == expiredIntent.Id);
            reloaded.Status.Should().Be(PaymentCheckoutStatus.Expired);
        }
    }

    [Fact]
    public async Task LatePayment_OnCashSettledTicket_RecordsFundsAndFlagsOverpaymentInHistory()
    {
        var (client, services, _) = CreateClientWithFakeGateway();
        var workspaceId = await CreateWorkspaceAsync(client, "Overpay Test Shop", "Commercial", "mobile_phone");

        var putResp = await client.SendAsync(ScopedRequest(HttpMethod.Put, "/api/v1/settings/payment-gateway", workspaceId, new
        {
            provider = "Xendit",
            environment = "Sandbox",
            isEnabled = true,
            apiKey = "xnd_development_ovp_1234",
            clearApiKey = false,
            webhookSecret = "xnd_cb_ovp_9999",
            clearWebhookSecret = false,
            defaultLinkExpiryHours = 24,
            enablePublicTrackingPayment = false,
            requirePhoneVerificationForPublicPayment = true
        }));
        putResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var settingsJson = await putResp.Content.ReadFromJsonAsync<JsonElement>();
        var webhookPath = new Uri(settingsJson.GetProperty("webhookUrl").GetString()!).PathAndQuery;

        var (ticketId, _) = await SeedTicketAsync(services, workspaceId, 300000m);

        // Create online link for full 300,000
        var linkResp = await client.SendAsync(ScopedRequest(HttpMethod.Post, $"/api/v1/service-tickets/{ticketId}/payment-links", workspaceId, new
        {
            amount = 300000m
        }));
        linkResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var linkJson = await linkResp.Content.ReadFromJsonAsync<JsonElement>();
        var checkoutId = linkJson.GetProperty("providerCheckoutId").GetString()!;
        var extRef = linkJson.GetProperty("externalReference").GetString()!;

        // Meanwhile, customer pays 300,000 in cash at the counter
        var cashResp = await client.SendAsync(ScopedRequest(HttpMethod.Post, $"/api/v1/service-tickets/{ticketId}/payment/transactions", workspaceId, new
        {
            entryType = "Payment",
            amount = 300000m,
            method = "Cash",
            notes = "Paid in cash at counter"
        }));
        cashResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Then the earlier online link gets paid late via webhook -> must NOT drop funds and must surface OverpaidAmount!
        var webhookReq = new HttpRequestMessage(HttpMethod.Post, webhookPath)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                id = checkoutId,
                external_id = extRef,
                status = "PAID",
                payment_method = "QR_CODE",
                payment_channel = "QRIS",
                paid_at = DateTimeOffset.UtcNow.ToString("O")
            }), Encoding.UTF8, "application/json")
        };
        webhookReq.Headers.Add("x-callback-token", "xnd_cb_ovp_9999");
        webhookReq.Headers.Add("webhook-id", "whk_overpay_1");
        var webhookResp = await client.SendAsync(webhookReq);
        webhookResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var paymentResp = await client.SendAsync(ScopedRequest(HttpMethod.Get, $"/api/v1/service-tickets/{ticketId}/payment", workspaceId));
        paymentResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var paymentJson = await paymentResp.Content.ReadFromJsonAsync<JsonElement>();
        paymentJson.GetProperty("amountPaid").GetDecimal().Should().Be(600000m);
        paymentJson.GetProperty("overpaidAmount").GetDecimal().Should().Be(300000m);

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var histories = await db.ServiceTicketHistories.AsNoTracking()
            .Where(x => x.ServiceTicketId == ticketId)
            .Select(x => x.Note)
            .ToListAsync();
        histories.Should().Contain(note => note != null && note.Contains("OVERPAYMENT ALERT"));
    }
}
