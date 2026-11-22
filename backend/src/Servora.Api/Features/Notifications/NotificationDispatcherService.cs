using System.Net;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.Security;

namespace Servora.Api.Features.Notifications;

public sealed record NotificationEventEnvelope(
    int ShopId,
    NotificationEventType EventType,
    string Title,
    string Message,
    string? DeepLinkUrl = null,
    string? CustomerName = null,
    string? CustomerEmail = null,
    string? CustomerPhone = null,
    string? AdditionalStaffEmail = null,
    string? ReferenceEntityType = null,
    int? ReferenceEntityId = null,
    object? Data = null);

public sealed class NotificationDeliveryJob
{
    private readonly NotificationDispatcherService _dispatcher;

    public NotificationDeliveryJob(NotificationDispatcherService dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 120, 600 })]
    public async Task DeliverAsync(int deliveryId, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.ExecuteDeliveryAsync(deliveryId, cancellationToken);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Notification delivery failed.");
        }
    }
}

public sealed class NotificationDispatcherService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ISecretProtector _secretProtector;
    private readonly IEmailSender _emailSender;
    private readonly IWhatsAppSender _whatsAppSender;
    private readonly ITelegramSender _telegramSender;
    private readonly IWebhookSender _webhookSender;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationDispatcherService> _logger;

    public NotificationDispatcherService(
        AppDbContext context,
        IConfiguration configuration,
        ISecretProtector secretProtector,
        IEmailSender emailSender,
        IWhatsAppSender whatsAppSender,
        ITelegramSender telegramSender,
        IWebhookSender webhookSender,
        IServiceProvider serviceProvider,
        ILogger<NotificationDispatcherService> logger)
    {
        _context = context;
        _configuration = configuration;
        _secretProtector = secretProtector;
        _emailSender = emailSender;
        _whatsAppSender = whatsAppSender;
        _telegramSender = telegramSender;
        _webhookSender = webhookSender;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public SmtpConfig? GetSystemSmtpConfig()
    {
        var host = _configuration["Email:Smtp:Host"]?.Trim();
        var fromEmail = _configuration["Email:Smtp:FromEmail"]?.Trim();
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromEmail))
        {
            return null;
        }

        return new SmtpConfig(
            Host: host,
            Port: _configuration.GetValue("Email:Smtp:Port", 587),
            UseSsl: _configuration.GetValue("Email:Smtp:UseSsl", true),
            Username: _configuration["Email:Smtp:Username"]?.Trim(),
            Password: _configuration["Email:Smtp:Password"],
            FromEmail: fromEmail,
            FromName: _configuration["Email:Smtp:FromName"]?.Trim() ?? "Servora");
    }

    public bool IsSystemSmtpConfigured() => GetSystemSmtpConfig() is not null;

    public SmtpConfig? ResolveEffectiveSmtpConfig(WorkspaceNotificationChannel? workspaceChannel, string? fallbackBrandName = null)
    {
        if (workspaceChannel is not null && !workspaceChannel.UseSystemSmtp && !string.IsNullOrWhiteSpace(workspaceChannel.SmtpHost))
        {
            string? password = null;
            if (!string.IsNullOrWhiteSpace(workspaceChannel.SmtpPasswordEncrypted))
            {
                try
                {
                    password = _secretProtector.Unprotect(workspaceChannel.SmtpPasswordEncrypted);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not decrypt custom SMTP password for workspace {ShopId}.", workspaceChannel.ShopId);
                }
            }

            var fromEmail = !string.IsNullOrWhiteSpace(workspaceChannel.FromEmail)
                ? workspaceChannel.FromEmail.Trim()
                : (workspaceChannel.SmtpUsername?.Trim() ?? "noreply@servora.local");

            return new SmtpConfig(
                Host: workspaceChannel.SmtpHost.Trim(),
                Port: workspaceChannel.SmtpPort > 0 ? workspaceChannel.SmtpPort : 587,
                UseSsl: workspaceChannel.SmtpUseSsl,
                Username: workspaceChannel.SmtpUsername?.Trim(),
                Password: password,
                FromEmail: fromEmail,
                FromName: !string.IsNullOrWhiteSpace(workspaceChannel.FromName)
                    ? workspaceChannel.FromName.Trim()
                    : (fallbackBrandName ?? "Servora"));
        }

        var system = GetSystemSmtpConfig();
        if (system is null) return null;

        if (workspaceChannel is not null && (!string.IsNullOrWhiteSpace(workspaceChannel.FromName) || !string.IsNullOrWhiteSpace(fallbackBrandName)))
        {
            return system with
            {
                FromName = !string.IsNullOrWhiteSpace(workspaceChannel.FromName)
                    ? workspaceChannel.FromName.Trim()
                    : (fallbackBrandName ?? system.FromName)
            };
        }

        return system;
    }

    public async Task<WorkspaceNotificationChannel> GetOrCreateChannelConfigAsync(int shopId, CancellationToken cancellationToken)
    {
        var existing = await _context.WorkspaceNotificationChannels
            .FirstOrDefaultAsync(x => x.ShopId == shopId, cancellationToken);
        if (existing is not null) return existing;

        var created = new WorkspaceNotificationChannel
        {
            ShopId = shopId,
            EmailEnabled = false,
            UseSystemSmtp = true,
            SmtpPort = 587,
            SmtpUseSsl = true,
            WhatsAppEnabled = false,
            WhatsAppProvider = WhatsAppProviderType.Manual,
            TelegramEnabled = false,
            TwoFactorEnforcement = TwoFactorEnforcementPolicy.Optional,
            RequireOtpForPublicQuoteApproval = false
        };

        _context.WorkspaceNotificationChannels.Add(created);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return created;
        }
        catch (DbUpdateException)
        {
            _context.Entry(created).State = EntityState.Detached;
            return await _context.WorkspaceNotificationChannels.FirstAsync(x => x.ShopId == shopId, cancellationToken);
        }
    }

    public static WorkspaceNotificationRule BuildDefaultRule(int shopId, NotificationEventType eventType) => eventType switch
    {
        NotificationEventType.TicketCreated => new() { ShopId = shopId, EventType = eventType, SendCustomerEmail = true, SendCustomerWhatsApp = true, SendWebhook = true },
        NotificationEventType.TicketStatusChanged => new() { ShopId = shopId, EventType = eventType, SendCustomerEmail = true, SendCustomerWhatsApp = true, SendWebhook = true },
        NotificationEventType.ApprovalRequested => new() { ShopId = shopId, EventType = eventType, SendCustomerEmail = true, SendCustomerWhatsApp = true, SendStaffTelegram = true, SendWebhook = true },
        NotificationEventType.ApprovalResolved => new() { ShopId = shopId, EventType = eventType, SendStaffEmail = true, SendStaffTelegram = true, SendWebhook = true },
        NotificationEventType.PaymentSettled => new() { ShopId = shopId, EventType = eventType, SendCustomerEmail = true, SendStaffTelegram = true, SendWebhook = true },
        NotificationEventType.DispatchEnRoute => new() { ShopId = shopId, EventType = eventType, SendCustomerEmail = true, SendCustomerWhatsApp = true, SendWebhook = true },
        NotificationEventType.DispatchArrived => new() { ShopId = shopId, EventType = eventType, SendCustomerEmail = true, SendCustomerWhatsApp = true, SendWebhook = true },
        NotificationEventType.TicketPickupOverride => new() { ShopId = shopId, EventType = eventType, SendStaffEmail = true, SendStaffTelegram = true, SendWebhook = true },
        NotificationEventType.UserRolesUpdated => new() { ShopId = shopId, EventType = eventType, SendStaffEmail = true, SendStaffTelegram = true, SendWebhook = true },
        NotificationEventType.MaintenanceDue => new() { ShopId = shopId, EventType = eventType, SendStaffEmail = true, SendStaffTelegram = true, SendWebhook = true },
        NotificationEventType.InventoryLowStock => new() { ShopId = shopId, EventType = eventType, SendStaffEmail = true, SendStaffTelegram = true, SendWebhook = true },
        _ => new() { ShopId = shopId, EventType = eventType, SendWebhook = true }
    };

    public async Task<IReadOnlyList<WorkspaceNotificationRule>> GetEffectiveRulesAsync(int shopId, CancellationToken cancellationToken)
    {
        var saved = await _context.WorkspaceNotificationRules.AsNoTracking()
            .Where(x => x.ShopId == shopId)
            .ToDictionaryAsync(x => x.EventType, cancellationToken);

        return NotificationEventKeys.All
            .Select(evt => saved.TryGetValue(evt, out var rule) ? rule : BuildDefaultRule(shopId, evt))
            .ToList();
    }

    public async Task<ChannelSendResult> SendDirectEmailAsync(
        int? shopId,
        string toEmail,
        string subject,
        string plainTextBody,
        string? htmlBody,
        string eventType,
        string? referenceEntityType = null,
        int? referenceEntityId = null,
        CancellationToken cancellationToken = default)
    {
        WorkspaceNotificationChannel? workspaceChannel = null;
        string? brandName = null;
        if (shopId.HasValue)
        {
            workspaceChannel = await _context.WorkspaceNotificationChannels.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ShopId == shopId.Value, cancellationToken);
            brandName = await _context.Shops.AsNoTracking()
                .Where(s => s.Id == shopId.Value)
                .Select(s => s.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var smtp = ResolveEffectiveSmtpConfig(workspaceChannel, brandName);
        var delivery = new NotificationDelivery
        {
            ShopId = shopId,
            Channel = NotificationChannel.Email,
            Provider = workspaceChannel is not null && !workspaceChannel.UseSystemSmtp ? "WorkspaceSmtp" : "SystemSmtp",
            EventType = eventType,
            Recipient = Truncate(toEmail.Trim(), 250),
            Subject = Truncate(subject, 200),
            PayloadPreview = Truncate(plainTextBody, 1000),
            RawPayloadJson = JsonSerializer.Serialize(new { toEmail, subject, plainTextBody, htmlBody }),
            Status = NotificationDeliveryStatus.Pending,
            ReferenceEntityType = referenceEntityType,
            ReferenceEntityId = referenceEntityId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _context.NotificationDeliveries.Add(delivery);

        if (smtp is null)
        {
            delivery.Status = NotificationDeliveryStatus.Skipped;
            delivery.ErrorMessage = "SMTP is not configured.";
            await _context.SaveChangesAsync(cancellationToken);
            return new ChannelSendResult(false, null, delivery.ErrorMessage);
        }

        delivery.AttemptCount = 1;
        var result = await _emailSender.SendAsync(smtp, toEmail, subject, plainTextBody, htmlBody, cancellationToken);
        delivery.Status = result.Succeeded ? NotificationDeliveryStatus.Sent : NotificationDeliveryStatus.Failed;
        delivery.HttpStatusCode = result.HttpStatusCode;
        delivery.ErrorMessage = result.ErrorMessage is null ? null : Truncate(result.ErrorMessage, 1000);
        delivery.SentAt = result.Succeeded ? DateTimeOffset.UtcNow : null;
        await _context.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<ChannelSendResult> SendDirectWhatsAppAsync(
        int shopId,
        string toPhone,
        string message,
        string eventType,
        string? referenceEntityType = null,
        int? referenceEntityId = null,
        CancellationToken cancellationToken = default)
    {
        var channelConfig = await _context.WorkspaceNotificationChannels.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ShopId == shopId, cancellationToken);

        var delivery = new NotificationDelivery
        {
            ShopId = shopId,
            Channel = NotificationChannel.WhatsApp,
            Provider = channelConfig?.WhatsAppProvider.ToString() ?? "Manual",
            EventType = eventType,
            Recipient = Truncate(toPhone.Trim(), 250),
            PayloadPreview = Truncate(message, 1000),
            RawPayloadJson = JsonSerializer.Serialize(new { toPhone, message, eventType }),
            Status = NotificationDeliveryStatus.Pending,
            ReferenceEntityType = referenceEntityType,
            ReferenceEntityId = referenceEntityId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _context.NotificationDeliveries.Add(delivery);

        if (channelConfig is null || !channelConfig.WhatsAppEnabled || channelConfig.WhatsAppProvider == WhatsAppProviderType.Manual)
        {
            delivery.Status = NotificationDeliveryStatus.Skipped;
            delivery.ErrorMessage = "Automated WhatsApp API is not enabled for this workspace.";
            await _context.SaveChangesAsync(cancellationToken);
            return new ChannelSendResult(false, null, delivery.ErrorMessage);
        }

        var waConfig = BuildWhatsAppConfig(channelConfig);
        delivery.AttemptCount = 1;
        var result = await _whatsAppSender.SendAsync(waConfig, toPhone, message, eventType, cancellationToken);
        delivery.Status = result.Succeeded ? NotificationDeliveryStatus.Sent : NotificationDeliveryStatus.Failed;
        delivery.HttpStatusCode = result.HttpStatusCode;
        delivery.ErrorMessage = result.ErrorMessage is null ? null : Truncate(result.ErrorMessage, 1000);
        delivery.SentAt = result.Succeeded ? DateTimeOffset.UtcNow : null;
        await _context.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<ChannelSendResult> SendDirectTelegramAsync(
        int shopId,
        string htmlMessage,
        string eventType,
        string? referenceEntityType = null,
        int? referenceEntityId = null,
        CancellationToken cancellationToken = default)
    {
        var channelConfig = await _context.WorkspaceNotificationChannels.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ShopId == shopId, cancellationToken);

        var delivery = new NotificationDelivery
        {
            ShopId = shopId,
            Channel = NotificationChannel.Telegram,
            Provider = "TelegramBot",
            EventType = eventType,
            Recipient = Truncate(channelConfig?.TelegramChatId ?? "-", 250),
            PayloadPreview = Truncate(htmlMessage, 1000),
            RawPayloadJson = JsonSerializer.Serialize(new { htmlMessage, eventType }),
            Status = NotificationDeliveryStatus.Pending,
            ReferenceEntityType = referenceEntityType,
            ReferenceEntityId = referenceEntityId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _context.NotificationDeliveries.Add(delivery);

        if (channelConfig is null || !channelConfig.TelegramEnabled || string.IsNullOrWhiteSpace(channelConfig.TelegramBotTokenEncrypted) || string.IsNullOrWhiteSpace(channelConfig.TelegramChatId))
        {
            delivery.Status = NotificationDeliveryStatus.Skipped;
            delivery.ErrorMessage = "Telegram Bot is not enabled or configured for this workspace.";
            await _context.SaveChangesAsync(cancellationToken);
            return new ChannelSendResult(false, null, delivery.ErrorMessage);
        }

        var botToken = _secretProtector.Unprotect(channelConfig.TelegramBotTokenEncrypted);
        delivery.AttemptCount = 1;
        var result = await _telegramSender.SendAsync(
            new TelegramConfig(botToken, channelConfig.TelegramChatId, channelConfig.TelegramThreadId),
            htmlMessage,
            cancellationToken);

        delivery.Status = result.Succeeded ? NotificationDeliveryStatus.Sent : NotificationDeliveryStatus.Failed;
        delivery.HttpStatusCode = result.HttpStatusCode;
        delivery.ErrorMessage = result.ErrorMessage is null ? null : Truncate(result.ErrorMessage, 1000);
        delivery.SentAt = result.Succeeded ? DateTimeOffset.UtcNow : null;
        await _context.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task PublishEventAsync(NotificationEventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        try
        {
            var channelConfig = await _context.WorkspaceNotificationChannels.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ShopId == envelope.ShopId, cancellationToken);
            var activeWebhooks = await _context.WorkspaceWebhookEndpoints.AsNoTracking()
                .Where(w => w.ShopId == envelope.ShopId && w.IsActive)
                .ToListAsync(cancellationToken);

            if (channelConfig is null && activeWebhooks.Count == 0)
            {
                return;
            }

            var rule = await _context.WorkspaceNotificationRules.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ShopId == envelope.ShopId && x.EventType == envelope.EventType, cancellationToken)
                ?? BuildDefaultRule(envelope.ShopId, envelope.EventType);

            var shopName = await _context.Shops.AsNoTracking()
                .Where(s => s.Id == envelope.ShopId)
                .Select(s => s.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? "Servora";

            var eventKey = envelope.EventType.ToEventKey();
            var queuedDeliveryIds = new List<int>();
            var bodyWithLink = string.IsNullOrWhiteSpace(envelope.DeepLinkUrl)
                ? envelope.Message
                : $"{envelope.Message}\n\n{envelope.DeepLinkUrl}";
            var htmlEmail = MailKitEmailSender.BuildDefaultHtmlEmail(
                shopName,
                envelope.Title,
                envelope.Message,
                envelope.DeepLinkUrl,
                string.IsNullOrWhiteSpace(envelope.DeepLinkUrl) ? null : "View in Servora");

            // 1. Customer Email
            if (rule.SendCustomerEmail && channelConfig?.EmailEnabled == true && !string.IsNullOrWhiteSpace(envelope.CustomerEmail))
            {
                var d = CreatePendingDelivery(
                    envelope.ShopId,
                    NotificationChannel.Email,
                    channelConfig.UseSystemSmtp ? "SystemSmtp" : "WorkspaceSmtp",
                    eventKey,
                    envelope.CustomerEmail.Trim(),
                    envelope.Title,
                    bodyWithLink,
                    JsonSerializer.Serialize(new { toEmail = envelope.CustomerEmail.Trim(), subject = envelope.Title, plainTextBody = bodyWithLink, htmlBody = htmlEmail }),
                    null,
                    envelope.ReferenceEntityType,
                    envelope.ReferenceEntityId);
                _context.NotificationDeliveries.Add(d);
                queuedDeliveryIds.Add(d.Id);
            }

            // 2. Customer WhatsApp
            if (rule.SendCustomerWhatsApp
                && channelConfig?.WhatsAppEnabled == true
                && channelConfig.WhatsAppProvider != WhatsAppProviderType.Manual
                && !string.IsNullOrWhiteSpace(envelope.CustomerPhone))
            {
                var d = CreatePendingDelivery(
                    envelope.ShopId,
                    NotificationChannel.WhatsApp,
                    channelConfig.WhatsAppProvider.ToString(),
                    eventKey,
                    envelope.CustomerPhone.Trim(),
                    null,
                    bodyWithLink,
                    JsonSerializer.Serialize(new { toPhone = envelope.CustomerPhone.Trim(), message = bodyWithLink, eventType = eventKey }),
                    null,
                    envelope.ReferenceEntityType,
                    envelope.ReferenceEntityId);
                _context.NotificationDeliveries.Add(d);
            }

            // 3. Staff Email(s)
            if (rule.SendStaffEmail && channelConfig?.EmailEnabled == true)
            {
                var recipients = ParseEmailList(channelConfig.StaffNotificationEmails);
                if (!string.IsNullOrWhiteSpace(envelope.AdditionalStaffEmail))
                {
                    recipients.Add(envelope.AdditionalStaffEmail.Trim());
                }

                foreach (var staffEmail in recipients)
                {
                    var d = CreatePendingDelivery(
                        envelope.ShopId,
                        NotificationChannel.Email,
                        channelConfig.UseSystemSmtp ? "SystemSmtp" : "WorkspaceSmtp",
                        eventKey,
                        staffEmail,
                        envelope.Title,
                        bodyWithLink,
                        JsonSerializer.Serialize(new { toEmail = staffEmail, subject = envelope.Title, plainTextBody = bodyWithLink, htmlBody = htmlEmail }),
                        null,
                        envelope.ReferenceEntityType,
                        envelope.ReferenceEntityId);
                    _context.NotificationDeliveries.Add(d);
                }
            }

            // 4. Staff Telegram
            if (rule.SendStaffTelegram
                && channelConfig?.TelegramEnabled == true
                && !string.IsNullOrWhiteSpace(channelConfig.TelegramBotTokenEncrypted)
                && !string.IsNullOrWhiteSpace(channelConfig.TelegramChatId))
            {
                var tgText = $"<b>{WebUtility.HtmlEncode(shopName)} — {WebUtility.HtmlEncode(envelope.Title)}</b>\n{WebUtility.HtmlEncode(envelope.Message)}"
                    + (string.IsNullOrWhiteSpace(envelope.DeepLinkUrl) ? string.Empty : $"\n<a href=\"{WebUtility.HtmlEncode(envelope.DeepLinkUrl)}\">Open</a>");

                var d = CreatePendingDelivery(
                    envelope.ShopId,
                    NotificationChannel.Telegram,
                    "TelegramBot",
                    eventKey,
                    channelConfig.TelegramChatId.Trim(),
                    envelope.Title,
                    bodyWithLink,
                    JsonSerializer.Serialize(new { htmlMessage = tgText, eventType = eventKey }),
                    null,
                    envelope.ReferenceEntityType,
                    envelope.ReferenceEntityId);
                _context.NotificationDeliveries.Add(d);
            }

            // 5. Outbound Webhooks
            if (rule.SendWebhook && activeWebhooks.Count > 0)
            {
                var webhookPayload = JsonSerializer.Serialize(new
                {
                    @event = eventKey,
                    workspaceId = envelope.ShopId,
                    workspaceName = shopName,
                    occurredAt = DateTimeOffset.UtcNow,
                    title = envelope.Title,
                    message = envelope.Message,
                    deepLinkUrl = envelope.DeepLinkUrl,
                    entityType = envelope.ReferenceEntityType,
                    entityId = envelope.ReferenceEntityId,
                    data = envelope.Data
                });

                foreach (var endpoint in activeWebhooks)
                {
                    if (!EndpointSubscribesTo(endpoint.SubscribedEventsJson, eventKey))
                    {
                        continue;
                    }

                    var d = CreatePendingDelivery(
                        envelope.ShopId,
                        NotificationChannel.Webhook,
                        "Webhook",
                        eventKey,
                        endpoint.TargetUrl,
                        envelope.Title,
                        Truncate(webhookPayload, 1000),
                        webhookPayload,
                        endpoint.Id,
                        envelope.ReferenceEntityType,
                        envelope.ReferenceEntityId);
                    _context.NotificationDeliveries.Add(d);
                }
            }

            var addedDeliveries = _context.ChangeTracker.Entries<NotificationDelivery>()
                .Where(e => e.State == EntityState.Added)
                .Select(e => e.Entity)
                .ToList();

            if (addedDeliveries.Count == 0)
            {
                return;
            }

            await _context.SaveChangesAsync(cancellationToken);

            var bgClient = _serviceProvider.GetService<IBackgroundJobClient>();
            var hangfireEnabled = _configuration.GetValue("Hangfire:Enabled", true);

            foreach (var delivery in addedDeliveries)
            {
                if (hangfireEnabled && bgClient is not null)
                {
                    bgClient.Enqueue<NotificationDeliveryJob>(job => job.DeliverAsync(delivery.Id, CancellationToken.None));
                }
                else
                {
                    await ExecuteDeliveryAsync(delivery.Id, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish notification event {EventType} for workspace {ShopId}.", envelope.EventType, envelope.ShopId);
        }
    }

    public async Task<ChannelSendResult> ExecuteDeliveryAsync(int deliveryId, CancellationToken cancellationToken = default)
    {
        var delivery = await _context.NotificationDeliveries
            .Include(d => d.WebhookEndpoint)
            .FirstOrDefaultAsync(d => d.Id == deliveryId, cancellationToken);
        if (delivery is null)
        {
            return new ChannelSendResult(false, null, "Delivery record not found.");
        }

        WorkspaceNotificationChannel? workspaceChannel = null;
        string? shopName = null;
        if (delivery.ShopId.HasValue)
        {
            workspaceChannel = await _context.WorkspaceNotificationChannels.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ShopId == delivery.ShopId.Value, cancellationToken);
            shopName = await _context.Shops.AsNoTracking()
                .Where(s => s.Id == delivery.ShopId.Value)
                .Select(s => s.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        delivery.AttemptCount += 1;
        ChannelSendResult result;

        try
        {
            switch (delivery.Channel)
            {
                case NotificationChannel.Email:
                {
                    var smtp = ResolveEffectiveSmtpConfig(workspaceChannel, shopName);
                    if (smtp is null)
                    {
                        result = new ChannelSendResult(false, null, "SMTP is not configured.");
                        break;
                    }

                    using var doc = JsonDocument.Parse(delivery.RawPayloadJson ?? "{}");
                    var root = doc.RootElement;
                    var toEmail = GetJsonString(root, "toEmail") ?? delivery.Recipient;
                    var subject = GetJsonString(root, "subject") ?? delivery.Subject ?? delivery.EventType;
                    var plain = GetJsonString(root, "plainTextBody") ?? delivery.PayloadPreview;
                    var html = GetJsonString(root, "htmlBody");
                    result = await _emailSender.SendAsync(smtp, toEmail, subject, plain, html, cancellationToken);
                    break;
                }

                case NotificationChannel.WhatsApp:
                {
                    if (workspaceChannel is null || !workspaceChannel.WhatsAppEnabled || workspaceChannel.WhatsAppProvider == WhatsAppProviderType.Manual)
                    {
                        result = new ChannelSendResult(false, null, "Automated WhatsApp API is not enabled.");
                        break;
                    }

                    using var doc = JsonDocument.Parse(delivery.RawPayloadJson ?? "{}");
                    var root = doc.RootElement;
                    var toPhone = GetJsonString(root, "toPhone") ?? delivery.Recipient;
                    var message = GetJsonString(root, "message") ?? delivery.PayloadPreview;
                    result = await _whatsAppSender.SendAsync(BuildWhatsAppConfig(workspaceChannel), toPhone, message, delivery.EventType, cancellationToken);
                    break;
                }

                case NotificationChannel.Telegram:
                {
                    if (workspaceChannel is null || !workspaceChannel.TelegramEnabled || string.IsNullOrWhiteSpace(workspaceChannel.TelegramBotTokenEncrypted) || string.IsNullOrWhiteSpace(workspaceChannel.TelegramChatId))
                    {
                        result = new ChannelSendResult(false, null, "Telegram Bot is not configured.");
                        break;
                    }

                    using var doc = JsonDocument.Parse(delivery.RawPayloadJson ?? "{}");
                    var htmlMsg = GetJsonString(doc.RootElement, "htmlMessage") ?? delivery.PayloadPreview;
                    var botToken = _secretProtector.Unprotect(workspaceChannel.TelegramBotTokenEncrypted);
                    result = await _telegramSender.SendAsync(
                        new TelegramConfig(botToken, workspaceChannel.TelegramChatId, workspaceChannel.TelegramThreadId),
                        htmlMsg,
                        cancellationToken);
                    break;
                }

                case NotificationChannel.Webhook:
                {
                    if (delivery.WebhookEndpoint is null || !delivery.WebhookEndpoint.IsActive)
                    {
                        result = new ChannelSendResult(false, null, "Webhook endpoint is deleted or inactive.");
                        break;
                    }

                    var secret = _secretProtector.Unprotect(delivery.WebhookEndpoint.SigningSecretEncrypted);
                    Dictionary<string, string>? customHeaders = null;
                    if (!string.IsNullOrWhiteSpace(delivery.WebhookEndpoint.CustomHeadersJson))
                    {
                        try
                        {
                            customHeaders = JsonSerializer.Deserialize<Dictionary<string, string>>(delivery.WebhookEndpoint.CustomHeadersJson);
                        }
                        catch (JsonException)
                        {
                        }
                    }

                    result = await _webhookSender.SendAsync(
                        new WebhookDispatchRequest(
                            delivery.WebhookEndpoint.TargetUrl,
                            secret,
                            delivery.EventType,
                            delivery.Id.ToString(),
                            delivery.RawPayloadJson ?? "{}",
                            customHeaders),
                        cancellationToken);

                    delivery.WebhookEndpoint.LastTriggeredAt = DateTimeOffset.UtcNow;
                    delivery.WebhookEndpoint.LastStatusCode = result.HttpStatusCode;
                    break;
                }

                default:
                    result = new ChannelSendResult(false, null, $"Unsupported delivery channel '{delivery.Channel}'.");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Delivery execution failed for delivery {DeliveryId}.", deliveryId);
            result = new ChannelSendResult(false, null, Truncate(ex.Message, 900));
        }

        delivery.Status = result.Succeeded ? NotificationDeliveryStatus.Sent : NotificationDeliveryStatus.Failed;
        delivery.HttpStatusCode = result.HttpStatusCode;
        delivery.ErrorMessage = result.ErrorMessage is null ? null : Truncate(result.ErrorMessage, 1000);
        if (result.Succeeded)
        {
            delivery.SentAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return result;
    }

    private WhatsAppConfig BuildWhatsAppConfig(WorkspaceNotificationChannel channel)
    {
        string? token = null;
        if (!string.IsNullOrWhiteSpace(channel.WhatsAppApiTokenEncrypted))
        {
            token = _secretProtector.Unprotect(channel.WhatsAppApiTokenEncrypted);
        }

        return new WhatsAppConfig(
            channel.WhatsAppProvider,
            channel.WhatsAppApiUrl,
            channel.WhatsAppPhoneNumberId,
            token,
            channel.WhatsAppAuthHeaderName,
            channel.WhatsAppCustomPayloadTemplate);
    }

    public static bool EndpointSubscribesTo(string? subscribedEventsJson, string eventKey)
    {
        if (string.IsNullOrWhiteSpace(subscribedEventsJson)) return true;
        try
        {
            var events = JsonSerializer.Deserialize<List<string>>(subscribedEventsJson);
            if (events is null || events.Count == 0) return true;
            return events.Any(e => e == "*" || string.Equals(e, eventKey, StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private static HashSet<string> ParseEmailList(string? raw)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw)) return result;
        foreach (var item in raw.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (item.Contains('@')) result.Add(item);
        }
        return result;
    }

    private static NotificationDelivery CreatePendingDelivery(
        int shopId,
        NotificationChannel channel,
        string provider,
        string eventKey,
        string recipient,
        string? subject,
        string preview,
        string rawPayloadJson,
        int? webhookEndpointId,
        string? entityType,
        int? entityId) => new()
    {
        ShopId = shopId,
        Channel = channel,
        Provider = Truncate(provider, 40),
        EventType = Truncate(eventKey, 60),
        Recipient = Truncate(recipient, 250),
        Subject = subject is null ? null : Truncate(subject, 200),
        PayloadPreview = Truncate(preview, 1000),
        RawPayloadJson = rawPayloadJson,
        Status = NotificationDeliveryStatus.Pending,
        WebhookEndpointId = webhookEndpointId,
        ReferenceEntityType = entityType,
        ReferenceEntityId = entityId,
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static string? GetJsonString(JsonElement root, string name)
        => root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
