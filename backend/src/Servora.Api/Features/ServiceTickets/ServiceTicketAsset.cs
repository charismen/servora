using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Devices;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.ServiceTickets;

/// <summary>
/// Links additional service assets/units to a single field visit or multi-unit work order.
/// </summary>
public class ServiceTicketAsset : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public int ServiceTicketId { get; set; }
    public ServiceTicket ServiceTicket { get; set; } = null!;
    public int ServiceAssetId { get; set; }
    public Device ServiceAsset { get; set; } = null!;
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class ServiceTicketAssetConfiguration : IEntityTypeConfiguration<ServiceTicketAsset>
{
    public void Configure(EntityTypeBuilder<ServiceTicketAsset> builder)
    {
        builder.ToTable("service_ticket_assets");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.ShopId).HasColumnName("workspace_id");
        builder.Property(x => x.ServiceTicketId).HasColumnName("service_ticket_id");
        builder.Property(x => x.ServiceAssetId).HasColumnName("service_asset_id");
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => new { x.ServiceTicketId, x.ServiceAssetId }).IsUnique();
        builder.HasIndex(x => new { x.ShopId, x.ServiceTicketId });

        builder.HasOne(x => x.Shop)
            .WithMany()
            .HasForeignKey(x => x.ShopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ServiceTicket)
            .WithMany()
            .HasForeignKey(x => x.ServiceTicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ServiceAsset)
            .WithMany()
            .HasForeignKey(x => x.ServiceAssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
