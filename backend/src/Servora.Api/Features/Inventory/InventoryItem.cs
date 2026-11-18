using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.Inventory;

public class InventoryItem : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public int? CategoryId { get; set; }
    public InventoryCategory? Category { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Unit { get; set; } = "pcs";
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public InventoryItemType ItemType { get; set; } = InventoryItemType.SparePart;
    public InventoryCondition Condition { get; set; } = InventoryCondition.New;
    public decimal PurchasePrice { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal MarginPercent { get; set; }
    public decimal MarginAmount { get; set; }
    public decimal TaxPercent { get; set; } = 11;
    public decimal TaxAmount { get; set; }
    public decimal PriceBeforeTax { get; set; }
    public decimal PriceAfterTax { get; set; }
    public decimal FixedSalePrice { get; set; }
    public string? Quality { get; set; }
    public int? WarrantyDays { get; set; }
    public string? SupplierName { get; set; }
    public string? BarcodeValue { get; set; }
    public string? LocationNote { get; set; }
    public DateTimeOffset? LastCountedAt { get; set; }
    public decimal OnHandQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public int StockVersion { get; set; }
    public decimal ReorderLevel { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public enum InventoryItemType
{
    SparePart,
    Accessory,
    SecondhandPart,
    SecondhandPcBuild,
    SecondhandLaptop,
    ServiceConsumable,
    RefurbishedUnit,
    Equipment,
    ToolOrSupply
}

public enum InventoryCondition
{
    New,
    Used,
    Refurbished,
    ForParts
}

public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("inventory_items");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedOnAdd();
        builder.Property(item => item.Sku).IsRequired().HasMaxLength(80);
        builder.Property(item => item.Name).IsRequired().HasMaxLength(180);
        builder.Property(item => item.Description).HasMaxLength(1000);
        builder.Property(item => item.Unit).IsRequired().HasMaxLength(20);
        builder.Property(item => item.CostPrice).HasPrecision(18, 2);
        builder.Property(item => item.SalePrice).HasPrecision(18, 2);
        builder.Property(item => item.ItemType).HasColumnName("item_type").HasConversion<string>().IsRequired().HasMaxLength(40);
        builder.Property(item => item.Condition).HasColumnName("condition").HasConversion<string>().IsRequired().HasMaxLength(40);
        builder.Property(item => item.PurchasePrice).HasColumnName("purchase_price").HasPrecision(18, 2);
        builder.Property(item => item.ShippingCost).HasColumnName("shipping_cost").HasPrecision(18, 2);
        builder.Property(item => item.ServiceFee).HasColumnName("service_fee").HasPrecision(18, 2);
        builder.Property(item => item.MarginPercent).HasColumnName("margin_percent").HasPrecision(9, 4);
        builder.Property(item => item.MarginAmount).HasColumnName("margin_amount").HasPrecision(18, 2);
        builder.Property(item => item.TaxPercent).HasColumnName("tax_percent").HasPrecision(9, 4);
        builder.Property(item => item.TaxAmount).HasColumnName("tax_amount").HasPrecision(18, 2);
        builder.Property(item => item.PriceBeforeTax).HasColumnName("price_before_tax").HasPrecision(18, 2);
        builder.Property(item => item.PriceAfterTax).HasColumnName("price_after_tax").HasPrecision(18, 2);
        builder.Property(item => item.FixedSalePrice).HasColumnName("fixed_sale_price").HasPrecision(18, 2);
        builder.Property(item => item.Quality).HasColumnName("quality").HasMaxLength(120);
        builder.Property(item => item.WarrantyDays).HasColumnName("warranty_days");
        builder.Property(item => item.SupplierName).HasColumnName("supplier_name").HasMaxLength(180);
        builder.Property(item => item.BarcodeValue).HasColumnName("barcode_value").HasMaxLength(120);
        builder.Property(item => item.LocationNote).HasColumnName("location_note").HasMaxLength(180);
        builder.Property(item => item.LastCountedAt).HasColumnName("last_counted_at");
        builder.Property(item => item.OnHandQuantity).HasPrecision(18, 2);
        builder.Property(item => item.ReservedQuantity).HasPrecision(18, 2);
        builder.Property(item => item.StockVersion).HasColumnName("stock_version").IsConcurrencyToken();
        builder.Property(item => item.ReorderLevel).HasPrecision(18, 2);
        builder.HasIndex(item => new { item.ShopId, item.Sku }).IsUnique();
        builder.HasIndex(item => new { item.ShopId, item.Name });
        builder.HasOne(item => item.Shop)
            .WithMany()
            .HasForeignKey(item => item.ShopId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Category)
            .WithMany()
            .HasForeignKey(item => item.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
