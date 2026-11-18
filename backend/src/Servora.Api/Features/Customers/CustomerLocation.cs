using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.Customers;

public class CustomerLocation : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Notes { get; set; }
    public bool IsDefault { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class CustomerLocationConfiguration : IEntityTypeConfiguration<CustomerLocation>
{
    public void Configure(EntityTypeBuilder<CustomerLocation> builder)
    {
        builder.ToTable("customer_locations");

        builder.HasKey(loc => loc.Id);
        builder.Property(loc => loc.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(loc => loc.ShopId).HasColumnName("workspace_id");
        builder.Property(loc => loc.CustomerId).HasColumnName("customer_id");
        builder.Property(loc => loc.Label).HasColumnName("label").IsRequired().HasMaxLength(150);
        builder.Property(loc => loc.Address).HasColumnName("address").IsRequired().HasMaxLength(500);
        builder.Property(loc => loc.City).HasColumnName("city").HasMaxLength(100);
        builder.Property(loc => loc.PostalCode).HasColumnName("postal_code").HasMaxLength(30);
        builder.Property(loc => loc.ContactName).HasColumnName("contact_name").HasMaxLength(150);
        builder.Property(loc => loc.ContactPhone).HasColumnName("contact_phone").HasMaxLength(30);
        builder.Property(loc => loc.Latitude).HasColumnName("latitude").HasPrecision(10, 7);
        builder.Property(loc => loc.Longitude).HasColumnName("longitude").HasPrecision(10, 7);
        builder.Property(loc => loc.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.Property(loc => loc.IsDefault).HasColumnName("is_default").HasDefaultValue(false);
        builder.Property(loc => loc.CreatedAt).HasColumnName("created_at");
        builder.Property(loc => loc.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(loc => new { loc.ShopId, loc.CustomerId });

        builder.HasOne(loc => loc.Shop)
            .WithMany()
            .HasForeignKey(loc => loc.ShopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(loc => loc.Customer)
            .WithMany()
            .HasForeignKey(loc => loc.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
