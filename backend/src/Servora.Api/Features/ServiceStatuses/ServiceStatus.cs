using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Shops;

namespace Servora.Api.Features.ServiceStatuses;

public class ServiceStatus
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#64748b";
    public int SortOrder { get; set; }
    public bool IsInitial { get; set; }
    public bool IsFinal { get; set; }
    public bool IsCancelled { get; set; }
    public bool IsBlocked { get; set; }
}

public class ServiceStatusConfiguration : IEntityTypeConfiguration<ServiceStatus>
{
    public void Configure(EntityTypeBuilder<ServiceStatus> builder)
    {
        builder.ToTable("service_statuses");

        builder.HasKey(status => status.Id);
        builder.Property(status => status.Id).ValueGeneratedOnAdd();
        builder.HasOne(status => status.Shop)
            .WithMany()
            .HasForeignKey(status => status.ShopId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(status => status.Name).IsRequired().HasMaxLength(80);
        builder.Property(status => status.Color).IsRequired().HasMaxLength(20);
        builder.HasIndex(status => new { status.ShopId, status.SortOrder });
    }
}
