using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Servora.Api.Features.Customers;
using Servora.Api.Features.Devices;
using Servora.Api.Features.Notifications;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Infrastructure.Data;
using Xunit;

namespace Servora.Api.IntegrationTests.Features.Notifications;

public class NotificationsAndWebhooksIntegrationTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly IntegrationTestWebAppFactory _factory;

    public NotificationsAndWebhooksIntegrationTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
    }

    private sealed record CapturedEmail(string ToEmail, string Subject, string PlainTextBody, string? HtmlBody);
    private sealed record CapturedWhatsApp(WhatsAppProviderType Provider, string ToPhone, string Message, string EventType);
    private sealed record CapturedTelegram(string ChatId, int? ThreadId, string HtmlMessage);
    private sealed record CapturedWebhook(string TargetUrl, string SigningSecret, string EventType, string RawJsonBody);

    private sealed class FakeNotificationSink
    {
        public ConcurrentQueue<CapturedEmail> Emails { get; } = new();
        public ConcurrentQueue<CapturedWhatsApp> WhatsApps { get; } = new();
        public ConcurrentQueue<CapturedTelegram> Telegrams { get; } = new();
        public ConcurrentQueue<CapturedWebhook> Webhooks { get; } = new();
        public volatile bool FailNextWebhook;
    }

    private sealed class FakeEmailSender(FakeNotificationSink sink) : IEmailSender
    {
        public Task<ChannelSendResult> SendAsync(
            SmtpConfig config,
            string toEmail,
            string subject,
            string plainTextBody,
            string? htmlBody,
            CancellationToken cancellationToken)
        {
            sink.Emails.Enqueue(new CapturedEmail(toEmail, subject, plainTextBody, htmlBody));
            return Task.FromResult(new ChannelSendResult(true, 250, null));
        }
    }

    private sealed class FakeWhatsAppSender(FakeNotificationSink sink) : IWhatsAppSender
    {
        public Task<ChannelSendResult> SendAsync(
            WhatsAppConfig config,
            string toPhone,
            string message,
            string eventType,
            CancellationToken cancellationToken)
        {
            sink.WhatsApps.Enqueue(new CapturedWhatsApp(config.Provider, toPhone, message, eventType));
            return Task.FromResult(new ChannelSendResult(true, 200, null));
        }
    }

    private sealed class FakeTelegramSender(FakeNotificationSink sink) : ITelegramSender
    {
        public Task<ChannelSendResult> SendAsync(
            TelegramConfig config,
            string htmlMessage,
            CancellationToken cancellationToken)
        {
            sink.Telegrams.Enqueue(new CapturedTelegram(config.ChatId, config.ThreadId, htmlMessage));
            return Task.FromResult(new ChannelSendResult(true, 200, null));
        }
    }

    private sealed class FakeWebhookSender(FakeNotificationSink sink) : IWebhookSender
    {
        public Task<ChannelSendResult> SendAsync(
            WebhookDispatchRequest request,
            CancellationToken cancellationToken)
        {
            if (sink.FailNextWebhook)
            {
                sink.FailNextWebhook = false;
                return Task.FromResult(new ChannelSendResult(false, 500, "Simulated downstream webhook error"));
            }

            sink.Webhooks.Enqueue(new CapturedWebhook(request.TargetUrl, request.SigningSecret, request.EventType, request.RawJsonBody));
            return Task.FromResult(new ChannelSendResult(true, 200, null));
        }
    }

    private (HttpClient Client, IServiceProvider Services, FakeNotificationSink Sink) CreateClientWithFakes(
        bool allowPublicRegistration = false)
    {
        var sink = new FakeNotificationSink();
        var webApp = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Email:Smtp:Host"] = "smtp.fake.local",
                    ["Email:Smtp:Port"] = "587",
                    ["Email:Smtp:UseSsl"] = "true",
                    ["Email:Smtp:FromEmail"] = "noreply@servora.test",
                    ["Email:Smtp:FromName"] = "Servora Test",
                    ["Auth:AllowPublicRegistration"] = allowPublicRegistration ? "true" : "false",
                    ["Webhooks:AllowPrivateTargets"] = "false"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.RemoveAll<IWhatsAppSender>();
                services.RemoveAll<ITelegramSender>();
                services.RemoveAll<IWebhookSender>();
                services.AddSingleton(sink);
                services.AddScoped<IEmailSender, FakeEmailSender>();
                services.AddScoped<IWhatsAppSender, FakeWhatsAppSender>();
                services.AddScoped<ITelegramSender, FakeTelegramSender>();
                services.AddScoped<IWebhookSender, FakeWebhookSender>();
            });
        });

        return (webApp.CreateClient(), webApp.Services, sink);
    }

    private static async Task AuthenticateAsAdminAsync(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@example.com",
            password = "Admin123!"
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var token = json.GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<int> CreateWorkspaceAsync(HttpClient client, string name)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/workspaces", new
        {
            name,
            operatingMode = "Commercial",
            industryBlueprintKey = "it_computer",
            defaultLocale = "en",
            timeZone = "Asia/Jakarta",
            currencyCode = "IDR",
            seedSampleData = false
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetInt32();
    }

    private static Task SwitchWorkspaceAsync(HttpClient client, int workspaceId)
    {
        client.DefaultRequestHeaders.Remove("X-Workspace-Id");
        client.DefaultRequestHeaders.Add("X-Workspace-Id", workspaceId.ToString());
        return Task.CompletedTask;
    }

    private static async Task<(int TicketId, string TrackingToken, int ApprovalId)> SeedTicketWithApprovalAsync(
        IServiceProvider services,
        int workspaceId,
        string customerEmail = "cust-quote@example.com",
        string customerPhone = "+6281234567890")
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var status = await db.ServiceStatuses.FirstAsync(s => s.ShopId == workspaceId);
        var customer = new Customer
        {
            ShopId = workspaceId,
            FullName = "Alex Quote Customer",
            PhoneNumber = customerPhone,
            Email = customerEmail
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var device = new Device
        {
            ShopId = workspaceId,
            CustomerId = customer.Id,
            DeviceType = "Laptop",
            Brand = "Lenovo",
            Model = "ThinkPad X1"
        };
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        var token = $"trk_{Guid.NewGuid():N}";
        var ticket = new ServiceTicket
        {
            ShopId = workspaceId,
            TicketNumber = $"NT-{Random.Shared.Next(10000, 99999)}",
            CustomerId = customer.Id,
            DeviceId = device.Id,
            StatusId = status.Id,
            CustomerNameSnapshot = customer.FullName,
            CustomerPhoneSnapshot = customer.PhoneNumber,
            DeviceSnapshot = "Lenovo ThinkPad X1",
            Complaint = "Battery drains rapidly",
            PublicTrackingToken = token,
            PublicTrackingEnabled = true
        };
        db.ServiceTickets.Add(ticket);
        await db.SaveChangesAsync();

        var adminUserId = await db.Users.Select(u => u.Id).FirstAsync();
        var approval = new ApprovalRequest
        {
            ShopId = workspaceId,
            ServiceTicketId = ticket.Id,
            RequestedByUserId = adminUserId,
            Title = "OEM Battery Replacement",
            Description = "Replace degraded 57Wh internal battery.",
            EstimatedAmount = 850000m,
            Status = ApprovalStatus.Pending
        };
        db.ApprovalRequests.Add(approval);
        await db.SaveChangesAsync();

        db.ServiceTicketWorkItems.Add(new ServiceTicketWorkItem
        {
            ShopId = workspaceId,
            ServiceTicketId = ticket.Id,
            ApprovalRequestId = approval.Id,
            ItemType = WorkItemType.NonStockPart,
            Title = "57Wh OEM Battery Pack",
            Quantity = 1,
            UnitPrice = 850000m,
            Status = WorkItemStatus.PendingApproval
        });
        await db.SaveChangesAsync();

        return (ticket.Id, token, approval.Id);
    }

    [Fact]
    public async Task NotificationChannels_MaskSecrets_DispatchMultiChannelEvents_AndRetryFailedDelivery()
    {
        var (client, services, sink) = CreateClientWithFakes();
        await AuthenticateAsAdminAsync(client);
        var workspaceId = await CreateWorkspaceAsync(client, $"Notify Lab {Guid.NewGuid():N}"[..24]);
        await SwitchWorkspaceAsync(client, workspaceId);

        // 1. Configure Email + WhatsApp (Fonnte) + Telegram
        var saveChannelsResp = await client.PutAsJsonAsync("/api/v1/settings/notification-channels", new
        {
            emailEnabled = true,
            useSystemSmtp = true,
            smtpPort = 587,
            smtpUseSsl = true,
            fromName = "Notify Lab Desk",
            staffNotificationEmails = "ops@notifylab.test",
            whatsAppEnabled = true,
            whatsAppProvider = "Fonnte",
            whatsAppApiToken = "fonnte_live_secret_9876",
            telegramEnabled = true,
            telegramBotToken = "123456:TG_BOT_SECRET_5432",
            telegramChatId = "-10099887766",
            telegramThreadId = 42,
            twoFactorEnforcement = "Optional",
            requireOtpForPublicQuoteApproval = false
        });
        saveChannelsResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var channelsJson = await saveChannelsResp.Content.ReadFromJsonAsync<JsonElement>();
        channelsJson.GetProperty("hasWhatsAppApiToken").GetBoolean().Should().BeTrue();
        channelsJson.GetProperty("whatsAppApiTokenLast4").GetString().Should().Be("9876");
        channelsJson.GetProperty("hasTelegramBotToken").GetBoolean().Should().BeTrue();
        channelsJson.GetProperty("telegramBotTokenLast4").GetString().Should().Be("5432");
        channelsJson.GetRawText().Should().NotContain("fonnte_live_secret_9876");
        channelsJson.GetRawText().Should().NotContain("TG_BOT_SECRET_5432");

        // 2. SSRF guard blocks localhost webhook
        var badWhResp = await client.PostAsJsonAsync("/api/v1/settings/webhooks", new
        {
            name = "Blocked Local Hook",
            targetUrl = "http://127.0.0.1:9999/hook",
            isActive = true,
            subscribedEvents = new[] { "approval.requested" }
        });
        badWhResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 3. Create valid outbound webhook and check secret returned once
        var createWhResp = await client.PostAsJsonAsync("/api/v1/settings/webhooks", new
        {
            name = "n8n Automation",
            targetUrl = "https://hooks.example.com/servora",
            isActive = true,
            subscribedEvents = new[] { "approval.requested", "ticket.created" }
        });
        createWhResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdWhJson = await createWhResp.Content.ReadFromJsonAsync<JsonElement>();
        var signingSecret = createdWhJson.GetProperty("signingSecret").GetString()!;
        signingSecret.Should().StartWith("whsec_");

        var listWhJson = await client.GetFromJsonAsync<JsonElement>("/api/v1/settings/webhooks");
        listWhJson.GetRawText().Should().NotContain(signingSecret);

        // 4. Force webhook failure on next dispatch, then fire approval.requested via CreateApproval endpoint
        var (ticketId, _, _) = await SeedTicketWithApprovalAsync(services, workspaceId);
        sink.FailNextWebhook = true;

        var createApprovalResp = await client.PostAsJsonAsync($"/api/v1/service-tickets/{ticketId}/approvals", new
        {
            title = "Screen Replacement",
            description = "Replace cracked LCD panel.",
            estimatedAmount = 1200000m
        });
        createApprovalResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // Verify Email + WhatsApp + Telegram were dispatched
        sink.Emails.Should().Contain(e => e.ToEmail == "cust-quote@example.com" && e.Subject.Contains("Screen Replacement"));
        sink.WhatsApps.Should().Contain(w => w.Provider == WhatsAppProviderType.Fonnte && w.EventType == "approval.requested");
        sink.Telegrams.Should().Contain(tg => tg.ChatId == "-10099887766" && tg.ThreadId == 42);

        // 5. Verify Delivery Log captured the failed webhook and Retry recovers it to Sent
        var deliveriesJson = await client.GetFromJsonAsync<JsonElement>("/api/v1/settings/notification-deliveries?channel=Webhook&status=Failed");
        var failedItems = deliveriesJson.GetProperty("items");
        failedItems.GetArrayLength().Should().BeGreaterThan(0);
        var failedId = failedItems[0].GetProperty("id").GetInt32();

        var retryResp = await client.PostAsync($"/api/v1/settings/notification-deliveries/{failedId}/retry", null);
        retryResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var retryJson = await retryResp.Content.ReadFromJsonAsync<JsonElement>();
        retryJson.GetProperty("success").GetBoolean().Should().BeTrue();
        sink.Webhooks.Should().Contain(w => w.EventType == "approval.requested" && w.SigningSecret == signingSecret);
    }

    [Fact]
    public async Task AuthEmails_StaffInvite_ForgotPassword_And_ConfigGatedRegistration_WorkEndToEnd()
    {
        var (client, _, sink) = CreateClientWithFakes(allowPublicRegistration: false);

        // 1. Public registration disabled by default
        var cfg = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/config");
        cfg.GetProperty("allowPublicRegistration").GetBoolean().Should().BeFalse();

        var regBlocked = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            firstName = "Blocked",
            lastName = "User",
            email = $"blocked_{Guid.NewGuid():N}@example.com",
            password = "Password123!",
            organizationName = "Blocked Org"
        });
        regBlocked.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 2. Admin creates staff member with sendInviteEmail = true and blank password
        await AuthenticateAsAdminAsync(client);
        var rolesJson = await client.GetFromJsonAsync<JsonElement>("/api/v1/shop-users/roles");
        var roleId = rolesJson[0].GetProperty("id").GetInt32();
        var invitedEmail = $"invited_{Guid.NewGuid():N}@example.com";

        var createUserResp = await client.PostAsJsonAsync("/api/v1/shop-users", new
        {
            email = invitedEmail,
            password = "",
            firstName = "Invited",
            lastName = "Tech",
            roleIds = new[] { roleId },
            employmentType = "FullTime",
            sendInviteEmail = true
        });
        createUserResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var inviteMail = sink.Emails.LastOrDefault(e => e.ToEmail == invitedEmail);
        inviteMail.Should().NotBeNull();
        inviteMail!.PlainTextBody.Should().Contain("/reset-password?email=");

        var tokenMatch = Regex.Match(inviteMail.PlainTextBody, @"token=([^\s&]+)");
        tokenMatch.Success.Should().BeTrue();
        var inviteToken = Uri.UnescapeDataString(tokenMatch.Groups[1].Value);

        // 3. Invited user sets password via /auth/reset-password and signs in
        var setPwResp = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            email = invitedEmail,
            token = inviteToken,
            newPassword = "NewSecurePassword1!"
        });
        setPwResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResp = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = invitedEmail,
            password = "NewSecurePassword1!"
        });
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginJson = await loginResp.Content.ReadFromJsonAsync<JsonElement>();
        loginJson.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();

        // 4. Forgot password flow resets password again
        var forgotResp = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = invitedEmail });
        forgotResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var resetMail = sink.Emails.LastOrDefault(e => e.ToEmail == invitedEmail && e.Subject.Contains("Reset", StringComparison.OrdinalIgnoreCase));
        resetMail.Should().NotBeNull();
        var resetTokenMatch = Regex.Match(resetMail!.PlainTextBody, @"token=([^\s&]+)");
        resetTokenMatch.Success.Should().BeTrue();

        var resetResp = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            email = invitedEmail,
            token = Uri.UnescapeDataString(resetTokenMatch.Groups[1].Value),
            newPassword = "SecondPassword2!"
        });
        resetResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = invitedEmail,
            password = "SecondPassword2!"
        });
        secondLogin.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task StaffTwoFactorAuth_EmailOtp_RecoveryCode_And_AdminReset_WorkEndToEnd()
    {
        var (client, _, sink) = CreateClientWithFakes();
        await AuthenticateAsAdminAsync(client);

        // Create a dedicated staff member for 2FA testing
        var rolesJson = await client.GetFromJsonAsync<JsonElement>("/api/v1/shop-users/roles");
        var roleId = rolesJson[0].GetProperty("id").GetInt32();
        var staffEmail = $"twofa_{Guid.NewGuid():N}@example.com";
        var staffPassword = "TwoFaPassword1!";

        var createStaff = await client.PostAsJsonAsync("/api/v1/shop-users", new
        {
            email = staffEmail,
            password = staffPassword,
            firstName = "Secure",
            lastName = "Staff",
            roleIds = new[] { roleId },
            employmentType = "FullTime"
        });
        createStaff.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdStaffId = (await createStaff.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        // Act as the newly created staff user via TestAuthHandler's X-Test-UserId header
        var staffLogin1 = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = staffEmail, password = staffPassword });
        staffLogin1.StatusCode.Should().Be(HttpStatusCode.OK);
        (await staffLogin1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
        client.DefaultRequestHeaders.Remove("X-Test-UserId");
        client.DefaultRequestHeaders.Add("X-Test-UserId", createdStaffId.ToString());

        // 1. Start Email OTP 2FA enrollment
        var startResp = await client.PostAsync("/api/v1/auth/2fa/email/start", null);
        startResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var startJson = await startResp.Content.ReadFromJsonAsync<JsonElement>();
        var enrollToken = startJson.GetProperty("challengeToken").GetString()!;

        sink.Emails.Should().Contain(e => string.Equals(e.ToEmail, staffEmail, StringComparison.OrdinalIgnoreCase));
        var enrollEmail = sink.Emails.Last(e => string.Equals(e.ToEmail, staffEmail, StringComparison.OrdinalIgnoreCase));
        var enrollCode = Regex.Match(enrollEmail.PlainTextBody, @"\b(\d{6})\b").Groups[1].Value;

        var confirmResp = await client.PostAsJsonAsync("/api/v1/auth/2fa/email/confirm", new
        {
            challengeToken = enrollToken,
            code = enrollCode
        });
        var confirmBody = await confirmResp.Content.ReadAsStringAsync();
        confirmResp.StatusCode.Should().Be(HttpStatusCode.OK, because: $"body was: {confirmBody} (enrollCode='{enrollCode}', plainText='{enrollEmail.PlainTextBody}')");
        var confirmJson = JsonDocument.Parse(confirmBody).RootElement;
        var recoveryCodes = confirmJson.GetProperty("recoveryCodes").EnumerateArray().Select(x => x.GetString()!).ToList();
        recoveryCodes.Should().HaveCount(8);

        // 2. Login now returns requiresTwoFactor challenge instead of JWT
        client.DefaultRequestHeaders.Remove("X-Test-UserId");
        var login2Fa = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = staffEmail, password = staffPassword });
        login2Fa.StatusCode.Should().Be(HttpStatusCode.OK);
        var challengeJson = await login2Fa.Content.ReadFromJsonAsync<JsonElement>();
        challengeJson.GetProperty("requiresTwoFactor").GetBoolean().Should().BeTrue();
        challengeJson.GetProperty("method").GetString().Should().Be("EmailOtp");
        var loginChallengeToken = challengeJson.GetProperty("challengeToken").GetString()!;

        var loginOtpEmail = sink.Emails.Last(e => string.Equals(e.ToEmail, staffEmail, StringComparison.OrdinalIgnoreCase) && e.Subject.Contains("verification code", StringComparison.OrdinalIgnoreCase));
        var loginOtpCode = Regex.Match(loginOtpEmail.PlainTextBody, @"\b(\d{6})\b").Groups[1].Value;

        var verifyResp = await client.PostAsJsonAsync("/api/v1/auth/login/verify-2fa", new
        {
            challengeToken = loginChallengeToken,
            code = loginOtpCode,
            useRecoveryCode = false
        });
        verifyResp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await verifyResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();

        // 3. Login with a single-use recovery code succeeds once and fails on reuse
        var loginRec = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = staffEmail, password = staffPassword });
        var recChallengeToken = (await loginRec.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("challengeToken").GetString()!;
        var firstRecovery = recoveryCodes[0];

        var verifyRec1 = await client.PostAsJsonAsync("/api/v1/auth/login/verify-2fa", new
        {
            challengeToken = recChallengeToken,
            code = firstRecovery,
            useRecoveryCode = true
        });
        verifyRec1.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginRec2 = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = staffEmail, password = staffPassword });
        var recChallengeToken2 = (await loginRec2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("challengeToken").GetString()!;
        var verifyRecReplay = await client.PostAsJsonAsync("/api/v1/auth/login/verify-2fa", new
        {
            challengeToken = recChallengeToken2,
            code = firstRecovery,
            useRecoveryCode = true
        });
        verifyRecReplay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 4. Owner/Admin resets 2FA for locked-out user -> user logs in directly again
        client.DefaultRequestHeaders.Remove("X-Test-UserId");
        var adminResetResp = await client.PostAsync($"/api/v1/shop-users/{createdStaffId}/reset-2fa", null);
        adminResetResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginAfterReset = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = staffEmail, password = staffPassword });
        loginAfterReset.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterResetJson = await loginAfterReset.Content.ReadFromJsonAsync<JsonElement>();
        afterResetJson.TryGetProperty("accessToken", out var directToken).Should().BeTrue();
        directToken.GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PublicQuoteApproval_SupportsPhoneLast4Mode_And_OptionalSixDigitOtpMode()
    {
        var (client, services, sink) = CreateClientWithFakes();
        await AuthenticateAsAdminAsync(client);
        var workspaceId = await CreateWorkspaceAsync(client, $"Quote OTP WS {Guid.NewGuid():N}"[..24]);
        await SwitchWorkspaceAsync(client, workspaceId);

        // Part A: Phone last-4 only mode (default)
        var (_, token1, approvalId1) = await SeedTicketWithApprovalAsync(services, workspaceId, "quote1@example.com", "+6281200001234");
        var anonClient = client;
        var adminAuth = client.DefaultRequestHeaders.Authorization;
        anonClient.DefaultRequestHeaders.Authorization = null;

        var track1 = await anonClient.GetFromJsonAsync<JsonElement>($"/api/v1/track/{token1}");
        track1.GetProperty("pendingApprovalCount").GetInt32().Should().Be(1);
        track1.GetProperty("quoteApprovalRequiresOtp").GetBoolean().Should().BeFalse();

        var approvals1 = await anonClient.PostAsJsonAsync($"/api/v1/track/{token1}/approvals", new { phoneLastFour = "1234" });
        approvals1.StatusCode.Should().Be(HttpStatusCode.OK);

        var respond1 = await anonClient.PostAsJsonAsync($"/api/v1/track/{token1}/approvals/{approvalId1}/respond", new
        {
            phoneLastFour = "1234",
            decision = "Approve",
            note = "Go ahead please!"
        });
        respond1.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var resolved = await db.ApprovalRequests.FirstAsync(a => a.Id == approvalId1);
            resolved.Status.Should().Be(ApprovalStatus.Approved);
        }

        // Part B: Enable 6-digit OTP mode + WhatsApp Fonnte provider
        client.DefaultRequestHeaders.Authorization = adminAuth;
        var enableOtpResp = await client.PutAsJsonAsync("/api/v1/settings/notification-channels", new
        {
            emailEnabled = true,
            useSystemSmtp = true,
            smtpPort = 587,
            smtpUseSsl = true,
            whatsAppEnabled = true,
            whatsAppProvider = "Fonnte",
            whatsAppApiToken = "fonnte_otp_token_4444",
            telegramEnabled = false,
            twoFactorEnforcement = "Optional",
            requireOtpForPublicQuoteApproval = true
        });
        enableOtpResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var (_, token2, approvalId2) = await SeedTicketWithApprovalAsync(services, workspaceId, "quote2@example.com", "+6281200005678");
        anonClient.DefaultRequestHeaders.Authorization = null;

        var track2 = await anonClient.GetFromJsonAsync<JsonElement>($"/api/v1/track/{token2}");
        track2.GetProperty("quoteApprovalRequiresOtp").GetBoolean().Should().BeTrue();
        track2.GetProperty("quoteApprovalOtpChannels").GetArrayLength().Should().Be(2);

        // Attempting to approve without OTP code must fail
        var missingOtpResp = await anonClient.PostAsJsonAsync($"/api/v1/track/{token2}/approvals/{approvalId2}/respond", new
        {
            phoneLastFour = "5678",
            decision = "Approve"
        });
        missingOtpResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Request 6-digit OTP via WhatsApp
        var sendOtpResp = await anonClient.PostAsJsonAsync($"/api/v1/track/{token2}/approvals/send-otp", new
        {
            phoneLastFour = "5678",
            channel = "WhatsApp"
        });
        sendOtpResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var otpJson = await sendOtpResp.Content.ReadFromJsonAsync<JsonElement>();
        var quoteChallengeToken = otpJson.GetProperty("challengeToken").GetString()!;

        var waOtpMsg = sink.WhatsApps.Last(w => w.EventType == "quote.approval_otp");
        var quoteOtpCode = Regex.Match(waOtpMsg.Message, @"\b(\d{6})\b").Groups[1].Value;

        // Submit valid OTP -> approval resolves!
        var respondWithOtp = await anonClient.PostAsJsonAsync($"/api/v1/track/{token2}/approvals/{approvalId2}/respond", new
        {
            phoneLastFour = "5678",
            challengeToken = quoteChallengeToken,
            otpCode = quoteOtpCode,
            decision = "Approve",
            note = "Verified via WhatsApp OTP"
        });
        respondWithOtp.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var resolved = await db.ApprovalRequests.FirstAsync(a => a.Id == approvalId2);
            resolved.Status.Should().Be(ApprovalStatus.Approved);
        }
    }
}
