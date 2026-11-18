using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Identity.Users;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.ServiceTickets;

public class ServiceTicketAttachment : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public int ServiceTicketId { get; set; }
    public ServiceTicket ServiceTicket { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public ServiceTicketAttachmentType AttachmentType { get; set; } = ServiceTicketAttachmentType.Other;
    public bool IsPublicVisible { get; set; }
    public int UploadedByUserId { get; set; }
    public ApplicationUser UploadedByUser { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public enum ServiceTicketAttachmentType
{
    DeviceFront,
    DeviceBack,
    Screen,
    Keyboard,
    Accessories,
    Damage,
    Receipt,
    Other,
    FrontView,
    BackView,
    ExteriorWalkaround,
    Odometer,
    EngineBay,
    IndoorUnit,
    OutdoorUnit,
    GaugeReading,
    SiteLocation,
    EquipmentTag,
    SafetyCheck,
    SerialOrTag,
    BeforeRepair,
    AfterRepair
}

public class ServiceTicketAttachmentConfiguration : IEntityTypeConfiguration<ServiceTicketAttachment>
{
    public void Configure(EntityTypeBuilder<ServiceTicketAttachment> builder)
    {
        builder.ToTable("service_ticket_attachments");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedOnAdd();
        builder.Property(item => item.FileName).IsRequired().HasMaxLength(255);
        builder.Property(item => item.StoredFileName).IsRequired().HasMaxLength(255);
        builder.Property(item => item.MimeType).IsRequired().HasMaxLength(80);
        builder.Property(item => item.AttachmentType).HasConversion<string>().IsRequired().HasMaxLength(40);
        builder.HasIndex(item => new { item.ShopId, item.ServiceTicketId });
        builder.HasOne(item => item.Shop).WithMany().HasForeignKey(item => item.ShopId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.ServiceTicket).WithMany().HasForeignKey(item => item.ServiceTicketId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.UploadedByUser).WithMany().HasForeignKey(item => item.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
