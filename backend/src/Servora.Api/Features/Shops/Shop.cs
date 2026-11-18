using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Organizations;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.Shops;

public enum OperatingMode
{
    Commercial,
    InternalMaintenance,
    Hybrid
}

public class Shop : IAuditableEntity
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? LogoUrl { get; set; }
    public OperatingMode OperatingMode { get; set; } = OperatingMode.Commercial;
    public string IndustryBlueprintKey { get; set; } = "it_computer";
    public string? TerminologyConfig { get; set; }
    public string DefaultLocale { get; set; } = "en";
    public string TimeZone { get; set; } = "Asia/Jakarta";
    public string CurrencyCode { get; set; } = "IDR";
    public bool SetupCompleted { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class ShopConfiguration : IEntityTypeConfiguration<Shop>
{
    public void Configure(EntityTypeBuilder<Shop> builder)
    {
        builder.ToTable("workspaces");

        builder.HasKey(shop => shop.Id);
        builder.Property(shop => shop.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(shop => shop.OrganizationId).HasColumnName("organization_id");
        builder.Property(shop => shop.Name).HasColumnName("name").IsRequired().HasMaxLength(150);
        builder.Property(shop => shop.Slug).HasColumnName("slug").IsRequired().HasMaxLength(80);
        builder.Property(shop => shop.PhoneNumber).HasColumnName("phone_number").HasMaxLength(30);
        builder.Property(shop => shop.Address).HasColumnName("address").HasMaxLength(500);
        builder.Property(shop => shop.LogoUrl).HasColumnName("logo_url").HasMaxLength(500);
        builder.Property(shop => shop.OperatingMode).HasColumnName("operating_mode").HasConversion<string>().IsRequired().HasMaxLength(40).HasDefaultValue(OperatingMode.Commercial);
        builder.Property(shop => shop.IndustryBlueprintKey).HasColumnName("industry_blueprint_key").IsRequired().HasMaxLength(60).HasDefaultValue("it_computer");
        builder.Property(shop => shop.TerminologyConfig).HasColumnName("terminology_config").HasColumnType("text");
        builder.Property(shop => shop.DefaultLocale).HasColumnName("default_locale").IsRequired().HasMaxLength(12);
        builder.Property(shop => shop.TimeZone).HasColumnName("timezone").IsRequired().HasMaxLength(80).HasDefaultValue("Asia/Jakarta");
        builder.Property(shop => shop.CurrencyCode).HasColumnName("currency_code").IsRequired().HasMaxLength(10).HasDefaultValue("IDR");
        builder.Property(shop => shop.SetupCompleted).HasColumnName("setup_completed").HasDefaultValue(false);
        builder.Property(shop => shop.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(shop => shop.CreatedAt).HasColumnName("created_at");
        builder.Property(shop => shop.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(shop => shop.Slug).IsUnique();
        builder.HasIndex(shop => new { shop.OrganizationId, shop.IsActive });

        builder.HasOne(shop => shop.Organization)
            .WithMany()
            .HasForeignKey(shop => shop.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
