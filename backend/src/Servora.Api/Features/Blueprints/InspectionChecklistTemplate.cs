using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.Blueprints;

public enum InspectionItemStatus
{
    Unchecked,
    Pass,
    Issue,
    NotApplicable
}

public class InspectionChecklistTemplate : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public string ItemKey { get; set; } = string.Empty;
    public string LabelEn { get; set; } = string.Empty;
    public string LabelId { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int SortOrder { get; set; }
    public bool IsRequired { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class TicketInspectionResult : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public int ServiceTicketId { get; set; }
    public ServiceTicket ServiceTicket { get; set; } = null!;
    public int? ChecklistTemplateId { get; set; }
    public InspectionChecklistTemplate? ChecklistTemplate { get; set; }
    public string Label { get; set; } = string.Empty;
    public InspectionItemStatus Status { get; set; } = InspectionItemStatus.Unchecked;
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class InspectionChecklistTemplateConfiguration : IEntityTypeConfiguration<InspectionChecklistTemplate>
{
    public void Configure(EntityTypeBuilder<InspectionChecklistTemplate> builder)
    {
        builder.ToTable("inspection_checklist_templates");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.ShopId).HasColumnName("workspace_id");
        builder.Property(x => x.ItemKey).HasColumnName("item_key").IsRequired().HasMaxLength(60);
        builder.Property(x => x.LabelEn).HasColumnName("label_en").IsRequired().HasMaxLength(150);
        builder.Property(x => x.LabelId).HasColumnName("label_id").IsRequired().HasMaxLength(150);
        builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(60);
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
        builder.Property(x => x.IsRequired).HasColumnName("is_required").HasDefaultValue(false);
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => new { x.ShopId, x.ItemKey }).IsUnique();
        builder.HasIndex(x => new { x.ShopId, x.IsActive, x.SortOrder });

        builder.HasOne(x => x.Shop)
            .WithMany()
            .HasForeignKey(x => x.ShopId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TicketInspectionResultConfiguration : IEntityTypeConfiguration<TicketInspectionResult>
{
    public void Configure(EntityTypeBuilder<TicketInspectionResult> builder)
    {
        builder.ToTable("ticket_inspection_results");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.ShopId).HasColumnName("workspace_id");
        builder.Property(x => x.ServiceTicketId).HasColumnName("service_ticket_id");
        builder.Property(x => x.ChecklistTemplateId).HasColumnName("checklist_template_id");
        builder.Property(x => x.Label).HasColumnName("label").IsRequired().HasMaxLength(150);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired().HasMaxLength(30).HasDefaultValue(InspectionItemStatus.Unchecked);
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => new { x.ShopId, x.ServiceTicketId });

        builder.HasOne(x => x.Shop)
            .WithMany()
            .HasForeignKey(x => x.ShopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ServiceTicket)
            .WithMany()
            .HasForeignKey(x => x.ServiceTicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ChecklistTemplate)
            .WithMany()
            .HasForeignKey(x => x.ChecklistTemplateId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
