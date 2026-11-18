using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Identity.Users;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.Assets;

public class Asset : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public int? CategoryId { get; set; }
    public AssetCategory? Category { get; set; }
    public string AssetTag { get; set; } = string.Empty;
    public string PublicToken { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CompanyName { get; set; }
    public string? Department { get; set; }
    public string? Location { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public AssetStatus Status { get; set; } = AssetStatus.Active;
    public AssetCondition Condition { get; set; } = AssetCondition.New;
    public int? AssignedUserId { get; set; }
    public ApplicationUser? AssignedUser { get; set; }
    public DateTimeOffset? AssignedAt { get; set; }
    public string? SupplierName { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTimeOffset PurchaseDate { get; set; }
    public DateTimeOffset InServiceDate { get; set; }
    public DateTimeOffset? WarrantyExpiresAt { get; set; }
    public decimal PurchaseCost { get; set; }
    public decimal SalvageValue { get; set; }
    public int UsefulLifeMonths { get; set; } = 36;
    public DepreciationMethod DepreciationMethod { get; set; } = DepreciationMethod.StraightLine;
    public decimal? EstimatedSellingValue { get; set; }
    public DateTimeOffset? DisposedAt { get; set; }
    public decimal? ActualSoldPrice { get; set; }
    public string? DisposalNotes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public ICollection<AssetCustodyLog> CustodyLogs { get; set; } = new List<AssetCustodyLog>();
    public ICollection<AssetMaintenanceLog> MaintenanceLogs { get; set; } = new List<AssetMaintenanceLog>();
}

public enum AssetStatus
{
    Active,
    Available,
    UnderMaintenance,
    Retired,
    Sold,
    WrittenOff
}

public enum AssetCondition
{
    New,
    Good,
    Fair,
    Poor,
    Damaged
}

public enum DepreciationMethod
{
    StraightLine,
    None
}

public class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("workspace_assets");
        builder.HasKey(asset => asset.Id);
        builder.Property(asset => asset.Id).ValueGeneratedOnAdd();
        builder.Property(asset => asset.ShopId).HasColumnName("workspace_id");
        builder.Property(asset => asset.CategoryId).HasColumnName("category_id");
        builder.Property(asset => asset.AssetTag).HasColumnName("asset_tag").IsRequired().HasMaxLength(80);
        builder.Property(asset => asset.PublicToken).HasColumnName("public_token").IsRequired().HasMaxLength(64);
        builder.Property(asset => asset.Name).HasColumnName("name").IsRequired().HasMaxLength(180);
        builder.Property(asset => asset.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(asset => asset.CompanyName).HasColumnName("company_name").HasMaxLength(180);
        builder.Property(asset => asset.Department).HasColumnName("department").HasMaxLength(120);
        builder.Property(asset => asset.Location).HasColumnName("location").HasMaxLength(180);
        builder.Property(asset => asset.Brand).HasColumnName("brand").HasMaxLength(120);
        builder.Property(asset => asset.Model).HasColumnName("model").HasMaxLength(120);
        builder.Property(asset => asset.SerialNumber).HasColumnName("serial_number").HasMaxLength(120);
        builder.Property(asset => asset.Status).HasColumnName("status").HasConversion<string>().IsRequired().HasMaxLength(40);
        builder.Property(asset => asset.Condition).HasColumnName("condition").HasConversion<string>().IsRequired().HasMaxLength(40);
        builder.Property(asset => asset.AssignedUserId).HasColumnName("assigned_user_id");
        builder.Property(asset => asset.AssignedAt).HasColumnName("assigned_at");
        builder.Property(asset => asset.SupplierName).HasColumnName("supplier_name").HasMaxLength(180);
        builder.Property(asset => asset.InvoiceNumber).HasColumnName("invoice_number").HasMaxLength(120);
        builder.Property(asset => asset.PurchaseDate).HasColumnName("purchase_date");
        builder.Property(asset => asset.InServiceDate).HasColumnName("in_service_date");
        builder.Property(asset => asset.WarrantyExpiresAt).HasColumnName("warranty_expires_at");
        builder.Property(asset => asset.PurchaseCost).HasColumnName("purchase_cost").HasPrecision(18, 2);
        builder.Property(asset => asset.SalvageValue).HasColumnName("salvage_value").HasPrecision(18, 2);
        builder.Property(asset => asset.UsefulLifeMonths).HasColumnName("useful_life_months").HasDefaultValue(36);
        builder.Property(asset => asset.DepreciationMethod).HasColumnName("depreciation_method").HasConversion<string>().IsRequired().HasMaxLength(40);
        builder.Property(asset => asset.EstimatedSellingValue).HasColumnName("estimated_selling_value").HasPrecision(18, 2);
        builder.Property(asset => asset.DisposedAt).HasColumnName("disposed_at");
        builder.Property(asset => asset.ActualSoldPrice).HasColumnName("actual_sold_price").HasPrecision(18, 2);
        builder.Property(asset => asset.DisposalNotes).HasColumnName("disposal_notes").HasMaxLength(500);
        builder.Property(asset => asset.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(asset => asset.CreatedAt).HasColumnName("created_at");
        builder.Property(asset => asset.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(asset => new { asset.ShopId, asset.AssetTag }).IsUnique();
        builder.HasIndex(asset => asset.PublicToken).IsUnique();
        builder.HasIndex(asset => new { asset.ShopId, asset.Status });
        builder.HasIndex(asset => new { asset.ShopId, asset.CompanyName });
        builder.HasIndex(asset => new { asset.ShopId, asset.AssignedUserId });

        builder.HasOne(asset => asset.Shop)
            .WithMany()
            .HasForeignKey(asset => asset.ShopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(asset => asset.Category)
            .WithMany()
            .HasForeignKey(asset => asset.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(asset => asset.AssignedUser)
            .WithMany()
            .HasForeignKey(asset => asset.AssignedUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
