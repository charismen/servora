using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Features.Shops;

namespace Servora.Api.Features.ServiceTemplates;

public class ServiceTemplate
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public List<ServiceTemplateChecklistItem> ChecklistItems { get; set; } = [];
}

public class ServiceTemplateChecklistItem
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public int ServiceTemplateId { get; set; }
    public ServiceTemplate ServiceTemplate { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}

public class ServiceTicketChecklistItem
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public int ServiceTicketId { get; set; }
    public ServiceTicket ServiceTicket { get; set; } = null!;
    public int? ServiceTemplateId { get; set; }
    public ServiceTemplate? ServiceTemplate { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsCompleted { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public class ServiceTemplateConfiguration : IEntityTypeConfiguration<ServiceTemplate>
{
    public void Configure(EntityTypeBuilder<ServiceTemplate> builder)
    {
        builder.ToTable("service_templates");
        builder.HasKey(template => template.Id);
        builder.Property(template => template.Id).ValueGeneratedOnAdd();
        builder.Property(template => template.Name).IsRequired().HasMaxLength(120);
        builder.Property(template => template.Description).HasMaxLength(500);
        builder.HasOne(template => template.Shop).WithMany().HasForeignKey(template => template.ShopId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(template => new { template.ShopId, template.Name }).IsUnique();
    }
}

public class ServiceTemplateChecklistItemConfiguration : IEntityTypeConfiguration<ServiceTemplateChecklistItem>
{
    public void Configure(EntityTypeBuilder<ServiceTemplateChecklistItem> builder)
    {
        builder.ToTable("service_template_checklist_items");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedOnAdd();
        builder.Property(item => item.Title).IsRequired().HasMaxLength(200);
        builder.Property(item => item.Description).HasMaxLength(500);
        builder.HasOne<Shop>().WithMany().HasForeignKey(item => item.ShopId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ServiceTemplate).WithMany(template => template.ChecklistItems).HasForeignKey(item => item.ServiceTemplateId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(item => new { item.ShopId, item.ServiceTemplateId, item.SortOrder });
    }
}

public class ServiceTicketChecklistItemConfiguration : IEntityTypeConfiguration<ServiceTicketChecklistItem>
{
    public void Configure(EntityTypeBuilder<ServiceTicketChecklistItem> builder)
    {
        builder.ToTable("service_ticket_checklist_items");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedOnAdd();
        builder.Property(item => item.Title).IsRequired().HasMaxLength(200);
        builder.Property(item => item.Description).HasMaxLength(500);
        builder.HasOne<Shop>().WithMany().HasForeignKey(item => item.ShopId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ServiceTicket).WithMany().HasForeignKey(item => item.ServiceTicketId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.ServiceTemplate).WithMany().HasForeignKey(item => item.ServiceTemplateId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(item => new { item.ShopId, item.ServiceTicketId, item.SortOrder });
    }
}
