using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Identity.Users;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Features.Shops;

namespace Servora.Api.Features.Assets;

public class AssetMaintenanceLog
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public int AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
    public int? ServiceTicketId { get; set; }
    public ServiceTicket? ServiceTicket { get; set; }
    public AssetMaintenanceType MaintenanceType { get; set; } = AssetMaintenanceType.Preventive;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Cost { get; set; }
    public string? PerformedBy { get; set; }
    public DateTimeOffset PerformedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? NextDueAt { get; set; }
    public int? RecordedByUserId { get; set; }
    public ApplicationUser? RecordedByUser { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum AssetMaintenanceType
{
    Preventive,
    Corrective,
    Calibration,
    Upgrade
}

public class AssetMaintenanceLogConfiguration : IEntityTypeConfiguration<AssetMaintenanceLog>
{
    public void Configure(EntityTypeBuilder<AssetMaintenanceLog> builder)
    {
        builder.ToTable("workspace_asset_maintenance_logs");
        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).ValueGeneratedOnAdd();
        builder.Property(log => log.ShopId).HasColumnName("workspace_id");
        builder.Property(log => log.AssetId).HasColumnName("asset_id");
        builder.Property(log => log.ServiceTicketId).HasColumnName("service_ticket_id");
        builder.Property(log => log.MaintenanceType).HasColumnName("maintenance_type").HasConversion<string>().IsRequired().HasMaxLength(40);
        builder.Property(log => log.Title).HasColumnName("title").IsRequired().HasMaxLength(180);
        builder.Property(log => log.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(log => log.Cost).HasColumnName("cost").HasPrecision(18, 2);
        builder.Property(log => log.PerformedBy).HasColumnName("performed_by").HasMaxLength(180);
        builder.Property(log => log.PerformedAt).HasColumnName("performed_at");
        builder.Property(log => log.NextDueAt).HasColumnName("next_due_at");
        builder.Property(log => log.RecordedByUserId).HasColumnName("recorded_by_user_id");
        builder.Property(log => log.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(log => new { log.ShopId, log.AssetId, log.PerformedAt });

        builder.HasOne(log => log.Shop)
            .WithMany()
            .HasForeignKey(log => log.ShopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(log => log.Asset)
            .WithMany(asset => asset.MaintenanceLogs)
            .HasForeignKey(log => log.AssetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(log => log.ServiceTicket)
            .WithMany()
            .HasForeignKey(log => log.ServiceTicketId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(log => log.RecordedByUser)
            .WithMany()
            .HasForeignKey(log => log.RecordedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
