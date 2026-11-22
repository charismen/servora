using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.Notifications;

public class NotificationTemplate : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public string Key { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; } = NotificationChannel.WhatsApp;
    public string Provider { get; set; } = "manual";
    public string Locale { get; set; } = "en";
    public string Name { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public enum NotificationChannel
{
    WhatsApp,
    Email,
    Telegram,
    Sms,
    InApp,
    Webhook
}

public class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("notification_templates");
        builder.HasKey(template => template.Id);
        builder.Property(template => template.Id).ValueGeneratedOnAdd();
        builder.Property(template => template.Key).IsRequired().HasMaxLength(80);
        builder.Property(template => template.Channel).HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(template => template.Provider).IsRequired().HasMaxLength(50);
        builder.Property(template => template.Locale).IsRequired().HasMaxLength(10);
        builder.Property(template => template.Name).IsRequired().HasMaxLength(120);
        builder.Property(template => template.Body).IsRequired().HasMaxLength(2000);
        builder.HasIndex(template => new { template.ShopId, template.Channel, template.Key, template.Locale }).IsUnique();
        builder.HasIndex(template => new { template.ShopId, template.Channel, template.Locale, template.Key });
        builder.HasOne(template => template.Shop)
            .WithMany()
            .HasForeignKey(template => template.ShopId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
