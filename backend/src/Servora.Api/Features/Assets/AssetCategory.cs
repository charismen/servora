using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.Assets;

public class AssetCategory : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DefaultUsefulLifeMonths { get; set; } = 36;
    public decimal DefaultSalvagePercent { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class AssetCategoryConfiguration : IEntityTypeConfiguration<AssetCategory>
{
    public void Configure(EntityTypeBuilder<AssetCategory> builder)
    {
        builder.ToTable("workspace_asset_categories");
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Id).ValueGeneratedOnAdd();
        builder.Property(category => category.ShopId).HasColumnName("workspace_id");
        builder.Property(category => category.Name).HasColumnName("name").IsRequired().HasMaxLength(120);
        builder.Property(category => category.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(category => category.DefaultUsefulLifeMonths).HasColumnName("default_useful_life_months").HasDefaultValue(36);
        builder.Property(category => category.DefaultSalvagePercent).HasColumnName("default_salvage_percent").HasPrecision(9, 4);
        builder.Property(category => category.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(category => category.CreatedAt).HasColumnName("created_at");
        builder.Property(category => category.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(category => new { category.ShopId, category.Name }).IsUnique();

        builder.HasOne(category => category.Shop)
            .WithMany()
            .HasForeignKey(category => category.ShopId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
