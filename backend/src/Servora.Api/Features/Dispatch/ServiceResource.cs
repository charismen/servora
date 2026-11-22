using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.Dispatch;

public enum ServiceResourceType
{
    Bay,
    Lift,
    Bench,
    Vehicle,
    Room,
    Other
}

public class ServiceResource : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public ServiceResourceType ResourceType { get; set; } = ServiceResourceType.Bay;
    public string Color { get; set; } = "#b45039";
    public string? Notes { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class ServiceResourceConfiguration : IEntityTypeConfiguration<ServiceResource>
{
    public void Configure(EntityTypeBuilder<ServiceResource> builder)
    {
        builder.ToTable("service_resources");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.ShopId).HasColumnName("workspace_id");
        builder.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(30);
        builder.Property(x => x.ResourceType).HasColumnName("resource_type").HasConversion<string>().IsRequired().HasMaxLength(30).HasDefaultValue(ServiceResourceType.Bay);
        builder.Property(x => x.Color).HasColumnName("color").IsRequired().HasMaxLength(20).HasDefaultValue("#b45039");
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(500);
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => new { x.ShopId, x.Name }).IsUnique();
        builder.HasIndex(x => new { x.ShopId, x.IsActive, x.SortOrder });

        builder.HasOne(x => x.Shop)
            .WithMany()
            .HasForeignKey(x => x.ShopId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
