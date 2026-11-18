using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Customers;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.Devices;

/// <summary>
/// Generalized customer/internal service asset (device, vehicle, AC unit, or facility equipment).
/// Stored in physical table <c>service_assets</c>.
/// </summary>
public class Device : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string DeviceType { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string? IdentifierLabel { get; set; }
    public decimal? MeterReading { get; set; }
    public string? MeterUnit { get; set; }
    public int? SiteLocationId { get; set; }
    public CustomerLocation? SiteLocation { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("service_assets");

        builder.HasKey(device => device.Id);
        builder.Property(device => device.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(device => device.ShopId).HasColumnName("workspace_id");
        builder.Property(device => device.CustomerId).HasColumnName("owner_customer_id");
        builder.Property(device => device.DeviceType).HasColumnName("asset_category").IsRequired().HasMaxLength(80);
        builder.Property(device => device.Brand).HasColumnName("brand").IsRequired().HasMaxLength(80);
        builder.Property(device => device.Model).HasColumnName("model").IsRequired().HasMaxLength(120);
        builder.Property(device => device.SerialNumber).HasColumnName("primary_identifier").HasMaxLength(120);
        builder.Property(device => device.IdentifierLabel).HasColumnName("secondary_identifier").HasMaxLength(150);
        builder.Property(device => device.MeterReading).HasColumnName("meter_reading").HasPrecision(14, 2);
        builder.Property(device => device.MeterUnit).HasColumnName("meter_unit").HasMaxLength(30);
        builder.Property(device => device.SiteLocationId).HasColumnName("site_address_id");
        builder.Property(device => device.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(device => device.CreatedAt).HasColumnName("created_at");
        builder.Property(device => device.UpdatedAt).HasColumnName("updated_at");
        builder.Property(device => device.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(device => new { device.ShopId, device.CustomerId });
        builder.HasIndex(device => new { device.ShopId, device.SerialNumber });
        builder.HasIndex(device => new { device.ShopId, device.SiteLocationId });

        builder.HasOne(device => device.Shop)
            .WithMany()
            .HasForeignKey(device => device.ShopId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(device => device.Customer)
            .WithMany()
            .HasForeignKey(device => device.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(device => device.SiteLocation)
            .WithMany()
            .HasForeignKey(device => device.SiteLocationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
