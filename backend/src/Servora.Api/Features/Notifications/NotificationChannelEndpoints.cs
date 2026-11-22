using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Features.Payments;
using Servora.Api.Infrastructure.Activity;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.Security;
using Servora.Api.Infrastructure.ShopContext;

namespace Servora.Api.Features.Notifications;

public static class NotificationChannelEndpoints
{
    public sealed record NotificationChannelsDto(
        bool EmailEnabled,
        bool UseSystemSmtp,
        bool SystemSmtpConfigured,
        string? SmtpHost,
        int SmtpPort,
        bool SmtpUseSsl,
        string? SmtpUsername,
        bool HasSmtpPassword,
        string? SmtpPasswordLast4,
        string? FromEmail,
        string? FromName,
        string? StaffNotificationEmails,
        bool WhatsAppEnabled,
        string WhatsAppProvider,
        string? WhatsAppApiUrl,
        string? WhatsAppPhoneNumberId,
        bool HasWhatsAppApiToken,
        string? WhatsAppApiTokenLast4,
        string? WhatsAppAuthHeaderName,
        string? WhatsAppCustomPayloadTemplate,
        bool TelegramEnabled,
        bool HasTelegramBotToken,
        string? TelegramBotTokenLast4,
        string? TelegramChatId,
        int? TelegramThreadId,
        string TwoFactorEnforcement,
        bool RequireOtpForPublicQuoteApproval,
        bool CanStoreSecrets);

    public sealed record UpdateNotificationChannelsRequest(
        bool EmailEnabled,
        bool UseSystemSmtp,
        string? SmtpHost,
        int SmtpPort,
        bool SmtpUseSsl,
        string? SmtpUsername,
        string? SmtpPassword,
        bool ClearSmtpPassword,
        string? FromEmail,
        string? FromName,
        string? StaffNotificationEmails,
        bool WhatsAppEnabled,
        string WhatsAppProvider,
        string? WhatsAppApiUrl,
        string? WhatsAppPhoneNumberId,
        string? WhatsAppApiToken,
        bool ClearWhatsAppApiToken,
        string? WhatsAppAuthHeaderName,
        string? WhatsAppCustomPayloadTemplate,
        bool TelegramEnabled,
        string? TelegramBotToken,
        bool ClearTelegramBotToken,
        string? TelegramChatId,
        int? TelegramThreadId,
        string TwoFactorEnforcement,
        bool RequireOtpForPublicQuoteApproval);

    public sealed record TestChannelRequest(
        string Channel,
        string? Recipient);

    public sealed record NotificationRuleDto(
        string EventType,
        string EventKey,
        bool SendCustomerEmail,
        bool SendCustomerWhatsApp,
        bool SendStaffEmail,
        bool SendStaffTelegram,
        bool SendWebhook);

    public sealed record UpdateNotificationRulesRequest(
        IReadOnlyList<NotificationRuleDto> Rules);

    public sealed record WebhookEndpointDto(
        int Id,
        string Name,
        string TargetUrl,
        bool IsActive,
        string SigningSecretLast4,
        IReadOnlyList<string> SubscribedEvents,
        IReadOnlyDictionary<string, string>? CustomHeaders,
        DateTimeOffset? LastTriggeredAt,
        int? LastStatusCode,
        DateTimeOffset CreatedAt);

    public sealed record UpsertWebhookRequest(
        string Name,
        string TargetUrl,
        bool IsActive,
        IReadOnlyList<string>? SubscribedEvents,
        IReadOnlyDictionary<string, string>? CustomHeaders);

    public sealed record NotificationDeliveryDto(
        int Id,
        string Channel,
        string Provider,
        string EventType,
        string Recipient,
        string? Subject,
        string PayloadPreview,
        string Status,
        int? HttpStatusCode,
        string? ErrorMessage,
        int AttemptCount,
        DateTimeOffset CreatedAt,
        DateTimeOffset? SentAt);

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/settings/notification-channels", GetChannelsAsync)
            .WithName("GetNotificationChannels")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsRead);

        app.MapPut("/settings/notification-channels", UpdateChannelsAsync)
            .WithName("UpdateNotificationChannels")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPost("/settings/notification-channels/test", TestChannelAsync)
            .WithName("TestNotificationChannel")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapGet("/settings/notification-rules", GetRulesAsync)
            .WithName("GetNotificationRules")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsRead);

        app.MapPut("/settings/notification-rules", UpdateRulesAsync)
            .WithName("UpdateNotificationRules")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapGet("/settings/webhooks", ListWebhooksAsync)
            .WithName("ListWorkspaceWebhooks")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsRead);

        app.MapPost("/settings/webhooks", CreateWebhookAsync)
            .WithName("CreateWorkspaceWebhook")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPut("/settings/webhooks/{id:int}", UpdateWebhookAsync)
            .WithName("UpdateWorkspaceWebhook")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapDelete("/settings/webhooks/{id:int}", DeleteWebhookAsync)
            .WithName("DeleteWorkspaceWebhook")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPost("/settings/webhooks/{id:int}/rotate-secret", RotateWebhookSecretAsync)
            .WithName("RotateWorkspaceWebhookSecret")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPost("/settings/webhooks/{id:int}/test", TestWebhookAsync)
            .WithName("TestWorkspaceWebhook")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapGet("/settings/notification-deliveries", ListDeliveriesAsync)
            .WithName("ListNotificationDeliveries")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsRead);

        app.MapPost("/settings/notification-deliveries/{id:int}/retry", RetryDeliveryAsync)
            .WithName("RetryNotificationDelivery")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);
    }

    private static async Task<IResult> GetChannelsAsync(
        IShopContext shopContext,
        NotificationDispatcherService dispatcher,
        ISecretProtector secretProtector,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var config = await dispatcher.GetOrCreateChannelConfigAsync(shopId, cancellationToken);
        return Results.Ok(ToChannelsDto(config, dispatcher.IsSystemSmtpConfigured(), secretProtector.CanStoreSecrets));
    }

    private static async Task<IResult> UpdateChannelsAsync(
        [FromBody] UpdateNotificationChannelsRequest request,
        AppDbContext context,
        IShopContext shopContext,
        NotificationDispatcherService dispatcher,
        ISecretProtector secretProtector,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<WhatsAppProviderType>(request.WhatsAppProvider, true, out var waProvider) || !Enum.IsDefined(waProvider))
        {
            return Results.BadRequest("Invalid WhatsApp provider.");
        }

        if (!Enum.TryParse<TwoFactorEnforcementPolicy>(request.TwoFactorEnforcement, true, out var tfaPolicy) || !Enum.IsDefined(tfaPolicy))
        {
            return Results.BadRequest("Invalid 2FA enforcement policy.");
        }

        var wantsToStoreSecret = !string.IsNullOrWhiteSpace(request.SmtpPassword)
            || !string.IsNullOrWhiteSpace(request.WhatsAppApiToken)
            || !string.IsNullOrWhiteSpace(request.TelegramBotToken);
        if (wantsToStoreSecret && !secretProtector.CanStoreSecrets)
        {
            return Results.BadRequest("Security:SecretEncryptionKey must be configured in Production before storing notification credentials.");
        }

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var config = await dispatcher.GetOrCreateChannelConfigAsync(shopId, cancellationToken);

        config.EmailEnabled = request.EmailEnabled;
        config.UseSystemSmtp = request.UseSystemSmtp;
        config.SmtpHost = TrimOrNull(request.SmtpHost, 200);
        config.SmtpPort = Math.Clamp(request.SmtpPort > 0 ? request.SmtpPort : 587, 1, 65535);
        config.SmtpUseSsl = request.SmtpUseSsl;
        config.SmtpUsername = TrimOrNull(request.SmtpUsername, 200);
        config.FromEmail = TrimOrNull(request.FromEmail, 200);
        config.FromName = TrimOrNull(request.FromName, 150);
        config.StaffNotificationEmails = TrimOrNull(request.StaffNotificationEmails, 1000);

        if (request.ClearSmtpPassword)
        {
            config.SmtpPasswordEncrypted = null;
            config.SmtpPasswordLast4 = null;
        }
        else if (!string.IsNullOrWhiteSpace(request.SmtpPassword))
        {
            var clean = request.SmtpPassword.Trim();
            config.SmtpPasswordEncrypted = secretProtector.Protect(clean);
            config.SmtpPasswordLast4 = PaymentCheckoutService.ExtractLast4(clean);
        }

        config.WhatsAppEnabled = request.WhatsAppEnabled && waProvider != WhatsAppProviderType.Manual;
        config.WhatsAppProvider = waProvider;
        config.WhatsAppApiUrl = TrimOrNull(request.WhatsAppApiUrl, 500);
        config.WhatsAppPhoneNumberId = TrimOrNull(request.WhatsAppPhoneNumberId, 100);
        config.WhatsAppAuthHeaderName = TrimOrNull(request.WhatsAppAuthHeaderName, 80);
        config.WhatsAppCustomPayloadTemplate = string.IsNullOrWhiteSpace(request.WhatsAppCustomPayloadTemplate)
            ? null
            : request.WhatsAppCustomPayloadTemplate.Trim();

        if (request.ClearWhatsAppApiToken)
        {
            config.WhatsAppApiTokenEncrypted = null;
            config.WhatsAppApiTokenLast4 = null;
        }
        else if (!string.IsNullOrWhiteSpace(request.WhatsAppApiToken))
        {
            var clean = request.WhatsAppApiToken.Trim();
            config.WhatsAppApiTokenEncrypted = secretProtector.Protect(clean);
            config.WhatsAppApiTokenLast4 = PaymentCheckoutService.ExtractLast4(clean);
        }

        config.TelegramEnabled = request.TelegramEnabled;
        config.TelegramChatId = TrimOrNull(request.TelegramChatId, 100);
        config.TelegramThreadId = request.TelegramThreadId is > 0 ? request.TelegramThreadId : null;

        if (request.ClearTelegramBotToken)
        {
            config.TelegramBotTokenEncrypted = null;
            config.TelegramBotTokenLast4 = null;
        }
        else if (!string.IsNullOrWhiteSpace(request.TelegramBotToken))
        {
            var clean = request.TelegramBotToken.Trim();
            config.TelegramBotTokenEncrypted = secretProtector.Protect(clean);
            config.TelegramBotTokenLast4 = PaymentCheckoutService.ExtractLast4(clean);
        }

        config.TwoFactorEnforcement = tfaPolicy;
        config.RequireOtpForPublicQuoteApproval = request.RequireOtpForPublicQuoteApproval;

        await context.SaveChangesAsync(cancellationToken);
        await activityLogger.LogAsync("Settings", shopId, "NotificationChannelsUpdated", "Notification channels & security policies updated", cancellationToken: cancellationToken);

        return Results.Ok(ToChannelsDto(config, dispatcher.IsSystemSmtpConfigured(), secretProtector.CanStoreSecrets));
    }

    private static async Task<IResult> TestChannelAsync(
        [FromBody] TestChannelRequest request,
        IShopContext shopContext,
        NotificationDispatcherService dispatcher,
        CancellationToken cancellationToken)
    {
        var workspace = await shopContext.GetCurrentWorkspaceAsync(cancellationToken);
        var channel = request.Channel?.Trim().ToLowerInvariant();
        ChannelSendResult result;

        switch (channel)
        {
            case "email":
                if (string.IsNullOrWhiteSpace(request.Recipient) || !request.Recipient.Contains('@'))
                    return Results.BadRequest("Enter a valid test recipient email address.");
                result = await dispatcher.SendDirectEmailAsync(
                    workspace.Id,
                    request.Recipient.Trim(),
                    $"[{workspace.Name}] Test Email from Servora",
                    $"This is a test email sent from {workspace.Name} to confirm SMTP notification delivery is working.",
                    null,
                    "channel.test",
                    cancellationToken: cancellationToken);
                break;

            case "whatsapp":
                if (string.IsNullOrWhiteSpace(request.Recipient))
                    return Results.BadRequest("Enter a test recipient WhatsApp phone number.");
                result = await dispatcher.SendDirectWhatsAppAsync(
                    workspace.Id,
                    request.Recipient.Trim(),
                    $"[{workspace.Name}] Test WhatsApp message from Servora. Your automated WhatsApp channel is connected!",
                    "channel.test",
                    cancellationToken: cancellationToken);
                break;

            case "telegram":
                result = await dispatcher.SendDirectTelegramAsync(
                    workspace.Id,
                    $"<b>{System.Net.WebUtility.HtmlEncode(workspace.Name)}</b>\nTest Telegram notification from Servora. Your bot connection is working!",
                    "channel.test",
                    cancellationToken: cancellationToken);
                break;

            default:
                return Results.BadRequest("Unsupported test channel. Choose Email, WhatsApp, or Telegram.");
        }

        return Results.Ok(new { success = result.Succeeded, message = result.Succeeded ? "Test notification sent successfully." : (result.ErrorMessage ?? "Test delivery failed.") });
    }

    private static async Task<IResult> GetRulesAsync(
        IShopContext shopContext,
        NotificationDispatcherService dispatcher,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var rules = await dispatcher.GetEffectiveRulesAsync(shopId, cancellationToken);
        return Results.Ok(rules.Select(r => new NotificationRuleDto(
            r.EventType.ToString(),
            r.EventType.ToEventKey(),
            r.SendCustomerEmail,
            r.SendCustomerWhatsApp,
            r.SendStaffEmail,
            r.SendStaffTelegram,
            r.SendWebhook)).ToList());
    }

    private static async Task<IResult> UpdateRulesAsync(
        [FromBody] UpdateNotificationRulesRequest request,
        AppDbContext context,
        IShopContext shopContext,
        NotificationDispatcherService dispatcher,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var existing = await context.WorkspaceNotificationRules
            .Where(x => x.ShopId == shopId)
            .ToDictionaryAsync(x => x.EventType, cancellationToken);

        foreach (var item in request.Rules)
        {
            if (!Enum.TryParse<NotificationEventType>(item.EventType, true, out var eventType) || !Enum.IsDefined(eventType))
            {
                continue;
            }

            if (existing.TryGetValue(eventType, out var entity))
            {
                entity.SendCustomerEmail = item.SendCustomerEmail;
                entity.SendCustomerWhatsApp = item.SendCustomerWhatsApp;
                entity.SendStaffEmail = item.SendStaffEmail;
                entity.SendStaffTelegram = item.SendStaffTelegram;
                entity.SendWebhook = item.SendWebhook;
            }
            else
            {
                context.WorkspaceNotificationRules.Add(new WorkspaceNotificationRule
                {
                    ShopId = shopId,
                    EventType = eventType,
                    SendCustomerEmail = item.SendCustomerEmail,
                    SendCustomerWhatsApp = item.SendCustomerWhatsApp,
                    SendStaffEmail = item.SendStaffEmail,
                    SendStaffTelegram = item.SendStaffTelegram,
                    SendWebhook = item.SendWebhook
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        await activityLogger.LogAsync("Settings", shopId, "NotificationRulesUpdated", "Notification routing rules updated", cancellationToken: cancellationToken);

        var updated = await dispatcher.GetEffectiveRulesAsync(shopId, cancellationToken);
        return Results.Ok(updated.Select(r => new NotificationRuleDto(
            r.EventType.ToString(),
            r.EventType.ToEventKey(),
            r.SendCustomerEmail,
            r.SendCustomerWhatsApp,
            r.SendStaffEmail,
            r.SendStaffTelegram,
            r.SendWebhook)).ToList());
    }

    private static async Task<IResult> ListWebhooksAsync(
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var rows = await context.WorkspaceWebhookEndpoints.AsNoTracking()
            .Where(x => x.ShopId == shopId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return Results.Ok(rows.Select(ToWebhookDto).ToList());
    }

    private static async Task<IResult> CreateWebhookAsync(
        [FromBody] UpsertWebhookRequest request,
        AppDbContext context,
        IShopContext shopContext,
        ISecretProtector secretProtector,
        IHostEnvironment environment,
        IConfiguration configuration,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        if (!secretProtector.CanStoreSecrets)
        {
            return Results.BadRequest("Security:SecretEncryptionKey must be configured in Production before creating webhooks.");
        }

        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
        {
            return Results.BadRequest("Webhook name is required (max 120 chars).");
        }

        var allowPrivate = configuration.GetValue("Webhooks:AllowPrivateTargets", !environment.IsProduction());
        var urlCheck = WebhookUrlValidator.ValidateStaticUrl(request.TargetUrl, environment.IsProduction(), allowPrivate);
        if (!urlCheck.IsValid)
        {
            return Results.BadRequest(urlCheck.ErrorMessage);
        }

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var plainSecret = $"whsec_{OtpCodeHelper.GenerateToken(24)}";
        var events = request.SubscribedEvents is { Count: > 0 } ? request.SubscribedEvents : ["*"];

        var entity = new WorkspaceWebhookEndpoint
        {
            ShopId = shopId,
            Name = name,
            TargetUrl = request.TargetUrl.Trim(),
            IsActive = request.IsActive,
            SigningSecretEncrypted = secretProtector.Protect(plainSecret),
            SigningSecretLast4 = PaymentCheckoutService.ExtractLast4(plainSecret),
            SubscribedEventsJson = JsonSerializer.Serialize(events),
            CustomHeadersJson = request.CustomHeaders is { Count: > 0 } ? JsonSerializer.Serialize(request.CustomHeaders) : null
        };

        context.WorkspaceWebhookEndpoints.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        await activityLogger.LogAsync("Webhook", entity.Id, "WebhookCreated", $"Outbound webhook created: {entity.Name}", cancellationToken: cancellationToken);

        return Results.Created($"/api/v1/settings/webhooks/{entity.Id}", new
        {
            webhook = ToWebhookDto(entity),
            signingSecret = plainSecret // Shown once upon creation!
        });
    }

    private static async Task<IResult> UpdateWebhookAsync(
        int id,
        [FromBody] UpsertWebhookRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IHostEnvironment environment,
        IConfiguration configuration,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
        {
            return Results.BadRequest("Webhook name is required (max 120 chars).");
        }

        var allowPrivate = configuration.GetValue("Webhooks:AllowPrivateTargets", !environment.IsProduction());
        var urlCheck = WebhookUrlValidator.ValidateStaticUrl(request.TargetUrl, environment.IsProduction(), allowPrivate);
        if (!urlCheck.IsValid)
        {
            return Results.BadRequest(urlCheck.ErrorMessage);
        }

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var entity = await context.WorkspaceWebhookEndpoints
            .FirstOrDefaultAsync(x => x.Id == id && x.ShopId == shopId, cancellationToken);
        if (entity is null) return Results.NotFound();

        var events = request.SubscribedEvents is { Count: > 0 } ? request.SubscribedEvents : ["*"];
        entity.Name = name;
        entity.TargetUrl = request.TargetUrl.Trim();
        entity.IsActive = request.IsActive;
        entity.SubscribedEventsJson = JsonSerializer.Serialize(events);
        entity.CustomHeadersJson = request.CustomHeaders is { Count: > 0 } ? JsonSerializer.Serialize(request.CustomHeaders) : null;

        await context.SaveChangesAsync(cancellationToken);
        await activityLogger.LogAsync("Webhook", entity.Id, "WebhookUpdated", $"Outbound webhook updated: {entity.Name}", cancellationToken: cancellationToken);

        return Results.Ok(ToWebhookDto(entity));
    }

    private static async Task<IResult> DeleteWebhookAsync(
        int id,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var entity = await context.WorkspaceWebhookEndpoints
            .FirstOrDefaultAsync(x => x.Id == id && x.ShopId == shopId, cancellationToken);
        if (entity is null) return Results.NotFound();

        context.WorkspaceWebhookEndpoints.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        await activityLogger.LogAsync("Webhook", id, "WebhookDeleted", $"Outbound webhook deleted: {entity.Name}", cancellationToken: cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> RotateWebhookSecretAsync(
        int id,
        AppDbContext context,
        IShopContext shopContext,
        ISecretProtector secretProtector,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var entity = await context.WorkspaceWebhookEndpoints
            .FirstOrDefaultAsync(x => x.Id == id && x.ShopId == shopId, cancellationToken);
        if (entity is null) return Results.NotFound();

        var plainSecret = $"whsec_{OtpCodeHelper.GenerateToken(24)}";
        entity.SigningSecretEncrypted = secretProtector.Protect(plainSecret);
        entity.SigningSecretLast4 = PaymentCheckoutService.ExtractLast4(plainSecret);
        await context.SaveChangesAsync(cancellationToken);
        await activityLogger.LogAsync("Webhook", id, "WebhookSecretRotated", $"Signing secret rotated for webhook: {entity.Name}", cancellationToken: cancellationToken);

        return Results.Ok(new { webhook = ToWebhookDto(entity), signingSecret = plainSecret });
    }

    private static async Task<IResult> TestWebhookAsync(
        int id,
        AppDbContext context,
        IShopContext shopContext,
        NotificationDispatcherService dispatcher,
        CancellationToken cancellationToken)
    {
        var workspace = await shopContext.GetCurrentWorkspaceAsync(cancellationToken);
        var entity = await context.WorkspaceWebhookEndpoints.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.ShopId == workspace.Id, cancellationToken);
        if (entity is null) return Results.NotFound();

        var payload = JsonSerializer.Serialize(new
        {
            @event = "webhook.test",
            workspaceId = workspace.Id,
            workspaceName = workspace.Name,
            occurredAt = DateTimeOffset.UtcNow,
            title = "Servora Webhook Test Ping",
            message = $"Test webhook ping from {workspace.Name}."
        });

        var delivery = new NotificationDelivery
        {
            ShopId = workspace.Id,
            Channel = NotificationChannel.Webhook,
            Provider = "Webhook",
            EventType = "webhook.test",
            Recipient = entity.TargetUrl,
            Subject = "Test Ping",
            PayloadPreview = payload,
            RawPayloadJson = payload,
            Status = NotificationDeliveryStatus.Pending,
            WebhookEndpointId = entity.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };
        context.NotificationDeliveries.Add(delivery);
        await context.SaveChangesAsync(cancellationToken);

        var result = await dispatcher.ExecuteDeliveryAsync(delivery.Id, cancellationToken);
        return Results.Ok(new
        {
            success = result.Succeeded,
            httpStatusCode = result.HttpStatusCode,
            message = result.Succeeded ? $"Webhook responded with HTTP {result.HttpStatusCode}." : (result.ErrorMessage ?? "Webhook delivery failed.")
        });
    }

    private static async Task<IResult> ListDeliveriesAsync(
        string? channel,
        string? status,
        int? page,
        int? pageSize,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var query = context.NotificationDeliveries.AsNoTracking().Where(x => x.ShopId == shopId);

        if (!string.IsNullOrWhiteSpace(channel) && Enum.TryParse<NotificationChannel>(channel, true, out var parsedChannel))
        {
            query = query.Where(x => x.Channel == parsedChannel);
        }
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<NotificationDeliveryStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(x => x.Status == parsedStatus);
        }

        var currentPage = Math.Max(1, page ?? 1);
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((currentPage - 1) * size)
            .Take(size)
            .Select(x => new NotificationDeliveryDto(
                x.Id,
                x.Channel.ToString(),
                x.Provider,
                x.EventType,
                x.Recipient,
                x.Subject,
                x.PayloadPreview,
                x.Status.ToString(),
                x.HttpStatusCode,
                x.ErrorMessage,
                x.AttemptCount,
                x.CreatedAt,
                x.SentAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(new
        {
            items,
            page = currentPage,
            pageSize = size,
            totalItems = total,
            totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)size))
        });
    }

    private static async Task<IResult> RetryDeliveryAsync(
        int id,
        AppDbContext context,
        IShopContext shopContext,
        NotificationDispatcherService dispatcher,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var exists = await context.NotificationDeliveries.AsNoTracking()
            .AnyAsync(x => x.Id == id && x.ShopId == shopId, cancellationToken);
        if (!exists) return Results.NotFound();

        var result = await dispatcher.ExecuteDeliveryAsync(id, cancellationToken);
        return Results.Ok(new
        {
            success = result.Succeeded,
            httpStatusCode = result.HttpStatusCode,
            message = result.Succeeded ? "Delivery retried successfully." : (result.ErrorMessage ?? "Retry failed.")
        });
    }

    private static NotificationChannelsDto ToChannelsDto(WorkspaceNotificationChannel c, bool systemSmtpConfigured, bool canStoreSecrets)
        => new(
            c.EmailEnabled,
            c.UseSystemSmtp,
            systemSmtpConfigured,
            c.SmtpHost,
            c.SmtpPort,
            c.SmtpUseSsl,
            c.SmtpUsername,
            !string.IsNullOrWhiteSpace(c.SmtpPasswordEncrypted),
            c.SmtpPasswordLast4,
            c.FromEmail,
            c.FromName,
            c.StaffNotificationEmails,
            c.WhatsAppEnabled,
            c.WhatsAppProvider.ToString(),
            c.WhatsAppApiUrl,
            c.WhatsAppPhoneNumberId,
            !string.IsNullOrWhiteSpace(c.WhatsAppApiTokenEncrypted),
            c.WhatsAppApiTokenLast4,
            c.WhatsAppAuthHeaderName,
            c.WhatsAppCustomPayloadTemplate,
            c.TelegramEnabled,
            !string.IsNullOrWhiteSpace(c.TelegramBotTokenEncrypted),
            c.TelegramBotTokenLast4,
            c.TelegramChatId,
            c.TelegramThreadId,
            c.TwoFactorEnforcement.ToString(),
            c.RequireOtpForPublicQuoteApproval,
            canStoreSecrets);

    private static WebhookEndpointDto ToWebhookDto(WorkspaceWebhookEndpoint w)
    {
        List<string> events;
        try { events = JsonSerializer.Deserialize<List<string>>(w.SubscribedEventsJson) ?? ["*"]; }
        catch { events = ["*"]; }

        Dictionary<string, string>? headers = null;
        if (!string.IsNullOrWhiteSpace(w.CustomHeadersJson))
        {
            try { headers = JsonSerializer.Deserialize<Dictionary<string, string>>(w.CustomHeadersJson); }
            catch { headers = null; }
        }

        return new WebhookEndpointDto(
            w.Id,
            w.Name,
            w.TargetUrl,
            w.IsActive,
            w.SigningSecretLast4,
            events,
            headers,
            w.LastTriggeredAt,
            w.LastStatusCode,
            w.CreatedAt);
    }

    private static string? TrimOrNull(string? val, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(val)) return null;
        var t = val.Trim();
        return t.Length <= maxLength ? t : t[..maxLength];
    }
}
