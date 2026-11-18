using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.ServiceTickets;

public class ServiceTicketAccessory : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public int ServiceTicketId { get; set; }
    public ServiceTicket ServiceTicket { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class ServiceTicketAccessoryConfiguration : IEntityTypeConfiguration<ServiceTicketAccessory>
{
    public void Configure(EntityTypeBuilder<ServiceTicketAccessory> builder)
    {
        builder.ToTable("service_ticket_accessories");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedOnAdd();
        builder.Property(item => item.Name).IsRequired().HasMaxLength(120);
        builder.Property(item => item.Quantity).HasPrecision(10, 2);
        builder.Property(item => item.Notes).HasMaxLength(500);
        builder.HasIndex(item => new { item.ShopId, item.ServiceTicketId });
        builder.HasOne(item => item.Shop).WithMany().HasForeignKey(item => item.ShopId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.ServiceTicket).WithMany().HasForeignKey(item => item.ServiceTicketId).OnDelete(DeleteBehavior.Cascade);
    }
}
