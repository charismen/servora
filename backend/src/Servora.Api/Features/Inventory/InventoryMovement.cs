using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Features.Shops;

namespace Servora.Api.Features.Inventory;

public class InventoryMovement
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public int InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;
    public int? ServiceTicketId { get; set; }
    public ServiceTicket? ServiceTicket { get; set; }
    public int? TicketInventoryUsageId { get; set; }
    public TicketInventoryUsage? TicketInventoryUsage { get; set; }
    public InventoryMovementType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum InventoryMovementType
{
    StockIn,
    Adjustment,
    Reserve,
    Release,
    Consume,
    Return
}

public class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.ToTable("inventory_movements");
        builder.HasKey(movement => movement.Id);
        builder.Property(movement => movement.Id).ValueGeneratedOnAdd();
        builder.Property(movement => movement.Type).HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(movement => movement.Quantity).HasPrecision(18, 2);
        builder.Property(movement => movement.UnitCost).HasPrecision(18, 2);
        builder.Property(movement => movement.Reason).HasMaxLength(500);
        builder.HasIndex(movement => new { movement.ShopId, movement.InventoryItemId, movement.CreatedAt });
        builder.HasOne(movement => movement.Shop)
            .WithMany()
            .HasForeignKey(movement => movement.ShopId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(movement => movement.InventoryItem)
            .WithMany()
            .HasForeignKey(movement => movement.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(movement => movement.ServiceTicket)
            .WithMany()
            .HasForeignKey(movement => movement.ServiceTicketId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(movement => movement.TicketInventoryUsage)
            .WithMany()
            .HasForeignKey(movement => movement.TicketInventoryUsageId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
