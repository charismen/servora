using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Identity.Users;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.Notifications;

public enum WhatsAppProviderType
{
    Manual,
    MetaCloud,
    Fonnte,
    Waha,
    CustomHttp
}

public enum NotificationDeliveryStatus
{
    Pending,
    Sent,
    Failed,
    Skipped
}

public enum TwoFactorMethod
{
    None,
    AuthenticatorTotp,
    EmailOtp
}

public enum TwoFactorEnforcementPolicy
{
    Optional,
    AdminsOnly,
    AllStaff
}

public enum OtpPurpose
{
    StaffLogin2Fa,
    Staff2FaEnrollment,
    PublicQuoteApproval
}

public enum NotificationEventType
{
    TicketCreated,
    TicketStatusChanged,
    ApprovalRequested,
    ApprovalResolved,
    PaymentSettled,
    DispatchEnRoute,
    DispatchArrived,
    TicketPickupOverride,
    UserRolesUpdated,
    MaintenanceDue,
    InventoryLowStock
}

public static class NotificationEventKeys
{
    public static string ToEventKey(this NotificationEventType eventType) => eventType switch
    {
        NotificationEventType.TicketCreated => "ticket.created",
        NotificationEventType.TicketStatusChanged => "ticket.status_changed",
        NotificationEventType.ApprovalRequested => "approval.requested",
        NotificationEventType.ApprovalResolved => "approval.resolved",
        NotificationEventType.PaymentSettled => "payment.settled",
        NotificationEventType.DispatchEnRoute => "dispatch.en_route",
        NotificationEventType.DispatchArrived => "dispatch.arrived",
        NotificationEventType.TicketPickupOverride => "ticket.pickup_override",
        NotificationEventType.UserRolesUpdated => "user.roles_updated",
        NotificationEventType.MaintenanceDue => "maintenance.due",
        NotificationEventType.InventoryLowStock => "inventory.low_stock",
        _ => eventType.ToString().ToLowerInvariant()
    };

    public static readonly IReadOnlyList<NotificationEventType> All = Enum.GetValues<NotificationEventType>();
}

public class WorkspaceNotificationChannel : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;

    // Email SMTP override
    public bool EmailEnabled { get; set; }
    public bool UseSystemSmtp { get; set; } = true;
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool SmtpUseSsl { get; set; } = true;
    public string? SmtpUsername { get; set; }
    public string? SmtpPasswordEncrypted { get; set; }
    public string? SmtpPasswordLast4 { get; set; }
    public string? FromEmail { get; set; }
    public string? FromName { get; set; }
    public string? StaffNotificationEmails { get; set; }

    // WhatsApp API
    public bool WhatsAppEnabled { get; set; }
    public WhatsAppProviderType WhatsAppProvider { get; set; } = WhatsAppProviderType.Manual;
    public string? WhatsAppApiUrl { get; set; }
    public string? WhatsAppPhoneNumberId { get; set; }
    public string? WhatsAppApiTokenEncrypted { get; set; }
    public string? WhatsAppApiTokenLast4 { get; set; }
    public string? WhatsAppAuthHeaderName { get; set; }
    public string? WhatsAppCustomPayloadTemplate { get; set; }

    // Telegram Bot API
    public bool TelegramEnabled { get; set; }
    public string? TelegramBotTokenEncrypted { get; set; }
    public string? TelegramBotTokenLast4 { get; set; }
    public string? TelegramChatId { get; set; }
    public int? TelegramThreadId { get; set; }

    // Workspace 2FA & Customer OTP Policies
    public TwoFactorEnforcementPolicy TwoFactorEnforcement { get; set; } = TwoFactorEnforcementPolicy.Optional;
    public bool RequireOtpForPublicQuoteApproval { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class WorkspaceNotificationRule : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public NotificationEventType EventType { get; set; }
    public bool SendCustomerEmail { get; set; }
    public bool SendCustomerWhatsApp { get; set; }
    public bool SendStaffEmail { get; set; }
    public bool SendStaffTelegram { get; set; }
    public bool SendWebhook { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class WorkspaceWebhookEndpoint : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string SigningSecretEncrypted { get; set; } = string.Empty;
    public string SigningSecretLast4 { get; set; } = string.Empty;
    public string SubscribedEventsJson { get; set; } = "[\"*\"]";
    public string? CustomHeadersJson { get; set; }
    public DateTimeOffset? LastTriggeredAt { get; set; }
    public int? LastStatusCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class NotificationDelivery
{
    public int Id { get; set; }
    public int? ShopId { get; set; }
    public Shop? Shop { get; set; }
    public NotificationChannel Channel { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string PayloadPreview { get; set; } = string.Empty;
    public string? RawPayloadJson { get; set; }
    public NotificationDeliveryStatus Status { get; set; } = NotificationDeliveryStatus.Pending;
    public int? HttpStatusCode { get; set; }
    public string? ErrorMessage { get; set; }
    public int AttemptCount { get; set; }
    public int? WebhookEndpointId { get; set; }
    public WorkspaceWebhookEndpoint? WebhookEndpoint { get; set; }
    public string? ReferenceEntityType { get; set; }
    public int? ReferenceEntityId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }
}

public class OtpVerificationChallenge
{
    public int Id { get; set; }
    public int? ShopId { get; set; }
    public Shop? Shop { get; set; }
    public int? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public int? ServiceTicketId { get; set; }
    public ServiceTicket? ServiceTicket { get; set; }
    public OtpPurpose Purpose { get; set; }
    public NotificationChannel Channel { get; set; }
    public string ChallengeToken { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public string CodeSalt { get; set; } = string.Empty;
    public string MaskedDestination { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class WorkspaceNotificationChannelConfiguration : IEntityTypeConfiguration<WorkspaceNotificationChannel>
{
    public void Configure(EntityTypeBuilder<WorkspaceNotificationChannel> builder)
    {
        builder.ToTable("workspace_notification_channels");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.ShopId).HasColumnName("workspace_id");

        builder.Property(x => x.EmailEnabled).HasColumnName("email_enabled").HasDefaultValue(false);
        builder.Property(x => x.UseSystemSmtp).HasColumnName("use_system_smtp").HasDefaultValue(true);
        builder.Property(x => x.SmtpHost).HasColumnName("smtp_host").HasMaxLength(200);
        builder.Property(x => x.SmtpPort).HasColumnName("smtp_port").HasDefaultValue(587);
        builder.Property(x => x.SmtpUseSsl).HasColumnName("smtp_use_ssl").HasDefaultValue(true);
        builder.Property(x => x.SmtpUsername).HasColumnName("smtp_username").HasMaxLength(200);
        builder.Property(x => x.SmtpPasswordEncrypted).HasColumnName("smtp_password_encrypted").HasColumnType("text");
        builder.Property(x => x.SmtpPasswordLast4).HasColumnName("smtp_password_last4").HasMaxLength(8);
        builder.Property(x => x.FromEmail).HasColumnName("from_email").HasMaxLength(200);
        builder.Property(x => x.FromName).HasColumnName("from_name").HasMaxLength(150);
        builder.Property(x => x.StaffNotificationEmails).HasColumnName("staff_notification_emails").HasMaxLength(1000);

        builder.Property(x => x.WhatsAppEnabled).HasColumnName("whatsapp_enabled").HasDefaultValue(false);
        builder.Property(x => x.WhatsAppProvider).HasColumnName("whatsapp_provider").HasConversion<string>().IsRequired().HasMaxLength(30).HasDefaultValue(WhatsAppProviderType.Manual);
        builder.Property(x => x.WhatsAppApiUrl).HasColumnName("whatsapp_api_url").HasMaxLength(500);
        builder.Property(x => x.WhatsAppPhoneNumberId).HasColumnName("whatsapp_phone_number_id").HasMaxLength(100);
        builder.Property(x => x.WhatsAppApiTokenEncrypted).HasColumnName("whatsapp_api_token_encrypted").HasColumnType("text");
        builder.Property(x => x.WhatsAppApiTokenLast4).HasColumnName("whatsapp_api_token_last4").HasMaxLength(8);
        builder.Property(x => x.WhatsAppAuthHeaderName).HasColumnName("whatsapp_auth_header_name").HasMaxLength(80);
        builder.Property(x => x.WhatsAppCustomPayloadTemplate).HasColumnName("whatsapp_custom_payload_template").HasColumnType("text");

        builder.Property(x => x.TelegramEnabled).HasColumnName("telegram_enabled").HasDefaultValue(false);
        builder.Property(x => x.TelegramBotTokenEncrypted).HasColumnName("telegram_bot_token_encrypted").HasColumnType("text");
        builder.Property(x => x.TelegramBotTokenLast4).HasColumnName("telegram_bot_token_last4").HasMaxLength(8);
        builder.Property(x => x.TelegramChatId).HasColumnName("telegram_chat_id").HasMaxLength(100);
        builder.Property(x => x.TelegramThreadId).HasColumnName("telegram_thread_id");

        builder.Property(x => x.TwoFactorEnforcement).HasColumnName("two_factor_enforcement").HasConversion<string>().IsRequired().HasMaxLength(30).HasDefaultValue(TwoFactorEnforcementPolicy.Optional);
        builder.Property(x => x.RequireOtpForPublicQuoteApproval).HasColumnName("require_otp_for_public_quote_approval").HasDefaultValue(false);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => x.ShopId).IsUnique();
        builder.HasOne(x => x.Shop).WithMany().HasForeignKey(x => x.ShopId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class WorkspaceNotificationRuleConfiguration : IEntityTypeConfiguration<WorkspaceNotificationRule>
{
    public void Configure(EntityTypeBuilder<WorkspaceNotificationRule> builder)
    {
        builder.ToTable("workspace_notification_rules");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.ShopId).HasColumnName("workspace_id");
        builder.Property(x => x.EventType).HasColumnName("event_type").HasConversion<string>().IsRequired().HasMaxLength(50);
        builder.Property(x => x.SendCustomerEmail).HasColumnName("send_customer_email").HasDefaultValue(false);
        builder.Property(x => x.SendCustomerWhatsApp).HasColumnName("send_customer_whatsapp").HasDefaultValue(false);
        builder.Property(x => x.SendStaffEmail).HasColumnName("send_staff_email").HasDefaultValue(false);
        builder.Property(x => x.SendStaffTelegram).HasColumnName("send_staff_telegram").HasDefaultValue(false);
        builder.Property(x => x.SendWebhook).HasColumnName("send_webhook").HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => new { x.ShopId, x.EventType }).IsUnique();
        builder.HasOne(x => x.Shop).WithMany().HasForeignKey(x => x.ShopId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class WorkspaceWebhookEndpointConfiguration : IEntityTypeConfiguration<WorkspaceWebhookEndpoint>
{
    public void Configure(EntityTypeBuilder<WorkspaceWebhookEndpoint> builder)
    {
        builder.ToTable("workspace_webhook_endpoints");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.ShopId).HasColumnName("workspace_id");
        builder.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(120);
        builder.Property(x => x.TargetUrl).HasColumnName("target_url").IsRequired().HasMaxLength(1000);
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(x => x.SigningSecretEncrypted).HasColumnName("signing_secret_encrypted").IsRequired().HasColumnType("text");
        builder.Property(x => x.SigningSecretLast4).HasColumnName("signing_secret_last4").IsRequired().HasMaxLength(8);
        builder.Property(x => x.SubscribedEventsJson).HasColumnName("subscribed_events_json").IsRequired().HasColumnType("text");
        builder.Property(x => x.CustomHeadersJson).HasColumnName("custom_headers_json").HasColumnType("text");
        builder.Property(x => x.LastTriggeredAt).HasColumnName("last_triggered_at");
        builder.Property(x => x.LastStatusCode).HasColumnName("last_status_code");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => new { x.ShopId, x.IsActive });
        builder.HasOne(x => x.Shop).WithMany().HasForeignKey(x => x.ShopId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("notification_deliveries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.ShopId).HasColumnName("workspace_id");
        builder.Property(x => x.Channel).HasColumnName("channel").HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(x => x.Provider).HasColumnName("provider").IsRequired().HasMaxLength(40);
        builder.Property(x => x.EventType).HasColumnName("event_type").IsRequired().HasMaxLength(60);
        builder.Property(x => x.Recipient).HasColumnName("recipient").IsRequired().HasMaxLength(250);
        builder.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(200);
        builder.Property(x => x.PayloadPreview).HasColumnName("payload_preview").IsRequired().HasColumnType("text");
        builder.Property(x => x.RawPayloadJson).HasColumnName("raw_payload_json").HasColumnType("text");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired().HasMaxLength(30).HasDefaultValue(NotificationDeliveryStatus.Pending);
        builder.Property(x => x.HttpStatusCode).HasColumnName("http_status_code");
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(1000);
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count").HasDefaultValue(0);
        builder.Property(x => x.WebhookEndpointId).HasColumnName("webhook_endpoint_id");
        builder.Property(x => x.ReferenceEntityType).HasColumnName("reference_entity_type").HasMaxLength(50);
        builder.Property(x => x.ReferenceEntityId).HasColumnName("reference_entity_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.SentAt).HasColumnName("sent_at");

        builder.HasIndex(x => new { x.ShopId, x.CreatedAt });
        builder.HasIndex(x => new { x.Status, x.CreatedAt });

        builder.HasOne(x => x.Shop).WithMany().HasForeignKey(x => x.ShopId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.WebhookEndpoint).WithMany().HasForeignKey(x => x.WebhookEndpointId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class OtpVerificationChallengeConfiguration : IEntityTypeConfiguration<OtpVerificationChallenge>
{
    public void Configure(EntityTypeBuilder<OtpVerificationChallenge> builder)
    {
        builder.ToTable("otp_verification_challenges");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.ShopId).HasColumnName("workspace_id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.ServiceTicketId).HasColumnName("service_ticket_id");
        builder.Property(x => x.Purpose).HasColumnName("purpose").HasConversion<string>().IsRequired().HasMaxLength(40);
        builder.Property(x => x.Channel).HasColumnName("channel").HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(x => x.ChallengeToken).HasColumnName("challenge_token").IsRequired().HasMaxLength(64);
        builder.Property(x => x.CodeHash).HasColumnName("code_hash").IsRequired().HasMaxLength(128);
        builder.Property(x => x.CodeSalt).HasColumnName("code_salt").IsRequired().HasMaxLength(64);
        builder.Property(x => x.MaskedDestination).HasColumnName("masked_destination").IsRequired().HasMaxLength(120);
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count").HasDefaultValue(0);
        builder.Property(x => x.MaxAttempts).HasColumnName("max_attempts").HasDefaultValue(5);
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.ConsumedAt).HasColumnName("consumed_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(x => x.ChallengeToken).IsUnique();
        builder.HasIndex(x => new { x.Purpose, x.UserId, x.CreatedAt });
        builder.HasIndex(x => new { x.Purpose, x.ServiceTicketId, x.CreatedAt });

        builder.HasOne(x => x.Shop).WithMany().HasForeignKey(x => x.ShopId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ServiceTicket).WithMany().HasForeignKey(x => x.ServiceTicketId).OnDelete(DeleteBehavior.Cascade);
    }
}
