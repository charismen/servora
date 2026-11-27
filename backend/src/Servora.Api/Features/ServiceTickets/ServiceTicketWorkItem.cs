using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Identity.Users;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.ServiceTickets;

public class ServiceTicketWorkItem : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public int ServiceTicketId { get; set; }
    public ServiceTicket ServiceTicket { get; set; } = null!;
    public int? ApprovalRequestId { get; set; }
    public ApprovalRequest? ApprovalRequest { get; set; }
    public int? CreatedByUserId { get; set; }
    public ApplicationUser? CreatedByUser { get; set; }
    public WorkItemType ItemType { get; set; } = WorkItemType.Service;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Quantity { get; set; } = 1m;
    public decimal UnitPrice { get; set; }
    public WorkItemStatus Status { get; set; } = WorkItemStatus.Approved;
    public bool IsCompleted { get; set; }
    public string? BonusTaskKey { get; set; }
    public int? AssignedTechnicianUserId { get; set; }
    public ApplicationUser? AssignedTechnicianUser { get; set; }
    public int? CompletedByUserId { get; set; }
    public ApplicationUser? CompletedByUser { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? DeviceCategoryOverride { get; set; }
    public bool IsInternalOnly { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public enum WorkItemType
{
    Service,
    NonStockPart,
    Fee
}

public enum WorkItemStatus
{
    PendingApproval,
    Approved,
    Rejected,
    Cancelled
}

public class ServiceTicketWorkItemConfiguration : IEntityTypeConfiguration<ServiceTicketWorkItem>
{
    public void Configure(EntityTypeBuilder<ServiceTicketWorkItem> builder)
    {
        builder.ToTable("service_ticket_work_items");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedOnAdd();
        builder.Property(item => item.ItemType).HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(item => item.Title).IsRequired().HasMaxLength(150);
        builder.Property(item => item.Description).HasMaxLength(1000);
        builder.Property(item => item.Quantity).HasPrecision(18, 2);
        builder.Property(item => item.UnitPrice).HasPrecision(18, 2);
        builder.Property(item => item.Status).HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(item => item.BonusTaskKey).HasMaxLength(60);
        builder.Property(item => item.DeviceCategoryOverride).HasMaxLength(20);

        builder.HasIndex(item => new { item.ShopId, item.ServiceTicketId, item.Status });
        builder.HasIndex(item => item.ApprovalRequestId);
        builder.HasIndex(item => new { item.ShopId, item.BonusTaskKey, item.IsCompleted });

        builder.HasOne(item => item.ServiceTicket)
            .WithMany()
            .HasForeignKey(item => item.ServiceTicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.ApprovalRequest)
            .WithMany()
            .HasForeignKey(item => item.ApprovalRequestId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(item => item.CreatedByUser)
            .WithMany()
            .HasForeignKey(item => item.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(item => item.AssignedTechnicianUser)
            .WithMany()
            .HasForeignKey(item => item.AssignedTechnicianUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(item => item.CompletedByUser)
            .WithMany()
            .HasForeignKey(item => item.CompletedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
