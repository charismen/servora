using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.Inventory;

public class TicketInventoryUsage : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public int ServiceTicketId { get; set; }
    public ServiceTicket ServiceTicket { get; set; } = null!;
    public int InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal ConsumedQuantity { get; set; }
    public decimal ReleasedQuantity { get; set; }
    public decimal ReturnedQuantity { get; set; }
    public decimal? UnitPriceSnapshot { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class TicketInventoryUsageConfiguration : IEntityTypeConfiguration<TicketInventoryUsage>
{
    public void Configure(EntityTypeBuilder<TicketInventoryUsage> builder)
    {
        builder.ToTable("ticket_inventory_usages");
        builder.HasKey(usage => usage.Id);
        builder.Property(usage => usage.Id).ValueGeneratedOnAdd();
        builder.Property(usage => usage.Quantity).HasPrecision(18, 2);
        builder.Property(usage => usage.ConsumedQuantity).HasPrecision(18, 2);
        builder.Property(usage => usage.ReleasedQuantity).HasPrecision(18, 2);
        builder.Property(usage => usage.ReturnedQuantity).HasPrecision(18, 2);
        builder.Property(usage => usage.UnitPriceSnapshot).HasPrecision(18, 2);
        builder.Property(usage => usage.Notes).HasMaxLength(500);
        builder.HasIndex(usage => new { usage.ShopId, usage.ServiceTicketId });
        builder.HasOne(usage => usage.Shop)
            .WithMany()
            .HasForeignKey(usage => usage.ShopId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(usage => usage.ServiceTicket)
            .WithMany()
            .HasForeignKey(usage => usage.ServiceTicketId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(usage => usage.InventoryItem)
            .WithMany()
            .HasForeignKey(usage => usage.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
