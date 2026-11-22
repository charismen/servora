using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Identity.Users;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.Payments;

public enum PaymentGatewayProvider
{
    None,
    Xendit,
    Stripe
}

public enum PaymentGatewayEnvironment
{
    Sandbox,
    Live
}

public enum PaymentCheckoutStatus
{
    Pending,
    Paid,
    Expired,
    Failed,
    Cancelled,
    Superseded
}

public class WorkspacePaymentGateway : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public PaymentGatewayProvider Provider { get; set; } = PaymentGatewayProvider.None;
    public PaymentGatewayEnvironment Environment { get; set; } = PaymentGatewayEnvironment.Sandbox;
    public bool IsEnabled { get; set; }
    public string? ApiKeyEncrypted { get; set; }
    public string? ApiKeyLast4 { get; set; }
    public string? WebhookSecretEncrypted { get; set; }
    public string? WebhookSecretLast4 { get; set; }
    public string WebhookToken { get; set; } = string.Empty;
    public int DefaultLinkExpiryHours { get; set; } = 24;
    public bool EnablePublicTrackingPayment { get; set; }
    public bool RequirePhoneVerificationForPublicPayment { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class PaymentCheckoutIntent : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public int ServiceTicketId { get; set; }
    public ServiceTicket ServiceTicket { get; set; } = null!;
    public PaymentGatewayProvider Provider { get; set; }
    public PaymentGatewayEnvironment Environment { get; set; }
    public PaymentCheckoutStatus Status { get; set; } = PaymentCheckoutStatus.Pending;
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "IDR";
    public string ExternalReference { get; set; } = string.Empty;
    public string? ProviderCheckoutId { get; set; }
    public string? PaymentUrl { get; set; }
    public string? QrPayload { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public string? ProviderPaymentMethod { get; set; }
    public int? SettledPaymentRecordId { get; set; }
    public PaymentRecord? SettledPaymentRecord { get; set; }
    public int? CreatedByUserId { get; set; }
    public ApplicationUser? CreatedByUser { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class PaymentWebhookEvent
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public PaymentGatewayProvider Provider { get; set; }
    public string ProviderEventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string ProcessingStatus { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
}

public class WorkspacePaymentGatewayConfiguration : IEntityTypeConfiguration<WorkspacePaymentGateway>
{
    public void Configure(EntityTypeBuilder<WorkspacePaymentGateway> builder)
    {
        builder.ToTable("workspace_payment_gateways");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.ShopId).HasColumnName("workspace_id");
        builder.Property(x => x.Provider).HasColumnName("provider").HasConversion<string>().IsRequired().HasMaxLength(30).HasDefaultValue(PaymentGatewayProvider.None);
        builder.Property(x => x.Environment).HasColumnName("environment").HasConversion<string>().IsRequired().HasMaxLength(20).HasDefaultValue(PaymentGatewayEnvironment.Sandbox);
        builder.Property(x => x.IsEnabled).HasColumnName("is_enabled").HasDefaultValue(false);
        builder.Property(x => x.ApiKeyEncrypted).HasColumnName("api_key_encrypted").HasColumnType("text");
        builder.Property(x => x.ApiKeyLast4).HasColumnName("api_key_last4").HasMaxLength(8);
        builder.Property(x => x.WebhookSecretEncrypted).HasColumnName("webhook_secret_encrypted").HasColumnType("text");
        builder.Property(x => x.WebhookSecretLast4).HasColumnName("webhook_secret_last4").HasMaxLength(8);
        builder.Property(x => x.WebhookToken).HasColumnName("webhook_token").IsRequired().HasMaxLength(64);
        builder.Property(x => x.DefaultLinkExpiryHours).HasColumnName("default_link_expiry_hours").HasDefaultValue(24);
        builder.Property(x => x.EnablePublicTrackingPayment).HasColumnName("enable_public_tracking_payment").HasDefaultValue(false);
        builder.Property(x => x.RequirePhoneVerificationForPublicPayment).HasColumnName("require_phone_verification_for_public_payment").HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => x.ShopId).IsUnique();
        builder.HasIndex(x => x.WebhookToken).IsUnique();

        builder.HasOne(x => x.Shop)
            .WithMany()
            .HasForeignKey(x => x.ShopId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PaymentCheckoutIntentConfiguration : IEntityTypeConfiguration<PaymentCheckoutIntent>
{
    public void Configure(EntityTypeBuilder<PaymentCheckoutIntent> builder)
    {
        builder.ToTable("payment_checkout_intents");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.ShopId).HasColumnName("workspace_id");
        builder.Property(x => x.ServiceTicketId).HasColumnName("service_ticket_id");
        builder.Property(x => x.Provider).HasColumnName("provider").HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(x => x.Environment).HasColumnName("environment").HasConversion<string>().IsRequired().HasMaxLength(20);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired().HasMaxLength(30).HasDefaultValue(PaymentCheckoutStatus.Pending);
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2);
        builder.Property(x => x.CurrencyCode).HasColumnName("currency_code").IsRequired().HasMaxLength(10);
        builder.Property(x => x.ExternalReference).HasColumnName("external_reference").IsRequired().HasMaxLength(120);
        builder.Property(x => x.ProviderCheckoutId).HasColumnName("provider_checkout_id").HasMaxLength(160);
        builder.Property(x => x.PaymentUrl).HasColumnName("payment_url").HasMaxLength(1000);
        builder.Property(x => x.QrPayload).HasColumnName("qr_payload").HasColumnType("text");
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.PaidAt).HasColumnName("paid_at");
        builder.Property(x => x.ProviderPaymentMethod).HasColumnName("provider_payment_method").HasMaxLength(80);
        builder.Property(x => x.SettledPaymentRecordId).HasColumnName("settled_payment_record_id");
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(x => x.FailureReason).HasColumnName("failure_reason").HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => new { x.ShopId, x.ExternalReference }).IsUnique();
        builder.HasIndex(x => new { x.ShopId, x.ServiceTicketId, x.Status });
        builder.HasIndex(x => new { x.ShopId, x.ProviderCheckoutId });
        builder.HasIndex(x => new { x.Status, x.ExpiresAt });

        builder.HasOne(x => x.Shop)
            .WithMany()
            .HasForeignKey(x => x.ShopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ServiceTicket)
            .WithMany()
            .HasForeignKey(x => x.ServiceTicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.SettledPaymentRecord)
            .WithMany()
            .HasForeignKey(x => x.SettledPaymentRecordId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class PaymentWebhookEventConfiguration : IEntityTypeConfiguration<PaymentWebhookEvent>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookEvent> builder)
    {
        builder.ToTable("payment_webhook_events");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.ShopId).HasColumnName("workspace_id");
        builder.Property(x => x.Provider).HasColumnName("provider").HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(x => x.ProviderEventId).HasColumnName("provider_event_id").IsRequired().HasMaxLength(160);
        builder.Property(x => x.EventType).HasColumnName("event_type").IsRequired().HasMaxLength(80);
        builder.Property(x => x.ProcessingStatus).HasColumnName("processing_status").IsRequired().HasMaxLength(30);
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(500);
        builder.Property(x => x.ReceivedAt).HasColumnName("received_at");
        builder.Property(x => x.ProcessedAt).HasColumnName("processed_at");

        builder.HasIndex(x => new { x.ShopId, x.Provider, x.ProviderEventId }).IsUnique();

        builder.HasOne(x => x.Shop)
            .WithMany()
            .HasForeignKey(x => x.ShopId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
