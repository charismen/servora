using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Customers;
using Servora.Api.Features.Devices;
using Servora.Api.Features.Dispatch;
using Servora.Api.Features.Identity.Users;
using Servora.Api.Features.ServiceStatuses;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.ServiceTickets;

public enum ServiceLocationType
{
    InShop,
    OnSite,
    PickupDelivery
}

public enum DispatchStatus
{
    Unscheduled,
    Scheduled,
    EnRoute,
    ArrivedOnSite,
    VisitCompleted
}

public class ServiceTicket : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public int StatusId { get; set; }
    public ServiceStatus Status { get; set; } = null!;
    public int? AssignedTechnicianUserId { get; set; }
    public ApplicationUser? AssignedTechnicianUser { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string? IdempotencyKey { get; set; }
    public string PublicTrackingToken { get; set; } = string.Empty;
    public bool PublicTrackingEnabled { get; set; } = true;
    public string CustomerNameSnapshot { get; set; } = string.Empty;
    public string CustomerPhoneSnapshot { get; set; } = string.Empty;
    public string DeviceSnapshot { get; set; } = string.Empty;
    public ServiceTicketPriority Priority { get; set; } = ServiceTicketPriority.Normal;
    public bool IsInternalWorkOrder { get; set; }
    public string? DepartmentCode { get; set; }
    public string? CostCenter { get; set; }
    public DateTimeOffset? SlaDueAt { get; set; }
    public ServiceLocationType ServiceLocationType { get; set; } = ServiceLocationType.InShop;
    public int? SiteLocationId { get; set; }
    public CustomerLocation? SiteLocation { get; set; }
    public string? SiteAddressSnapshot { get; set; }
    public DateTimeOffset? ScheduledVisitStart { get; set; }
    public DateTimeOffset? ScheduledVisitEnd { get; set; }
    public int? ServiceResourceId { get; set; }
    public ServiceResource? ServiceResource { get; set; }
    public DispatchStatus DispatchStatus { get; set; } = DispatchStatus.Unscheduled;
    public DateTimeOffset? VisitStartedAt { get; set; }
    public DateTimeOffset? VisitCompletedAt { get; set; }
    public string? DispatchNotes { get; set; }
    public int? ParentTicketId { get; set; }
    public ServiceTicket? ParentTicket { get; set; }
    public string Complaint { get; set; } = string.Empty;
    public string? InitialDiagnosis { get; set; }
    public string? PhysicalConditionNotes { get; set; }
    public string? AccessorySummary { get; set; }
    public string? PhotoNotes { get; set; }
    public string? SecurityPasscode { get; set; }
    public string? SecurityPattern { get; set; }
    public string? PreRepairChecklist { get; set; }
    public string? Diagnosis { get; set; }
    public string? RepairNotes { get; set; }
    public DateTimeOffset? WarrantyUntil { get; set; }
    public string? WarrantyTerms { get; set; }
    public bool ApprovalRequired { get; set; }
    public bool IsPreApproved { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    public decimal? TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal DiscountAmount { get; set; }
    public bool IsTotalManuallyOverridden { get; set; }
    public string? PickupOverrideReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public enum ServiceTicketPriority
{
    Low,
    Normal,
    High,
    Urgent
}

public enum PaymentStatus
{
    Unpaid,
    Partial,
    Paid,
    Waived
}

public class ServiceTicketConfiguration : IEntityTypeConfiguration<ServiceTicket>
{
    public void Configure(EntityTypeBuilder<ServiceTicket> builder)
    {
        builder.ToTable("service_tickets");

        builder.HasKey(ticket => ticket.Id);
        builder.Property(ticket => ticket.Id).ValueGeneratedOnAdd();
        builder.Property(ticket => ticket.TicketNumber).IsRequired().HasMaxLength(40);
        builder.Property(ticket => ticket.IdempotencyKey).HasMaxLength(120);
        builder.Property(ticket => ticket.PublicTrackingToken).IsRequired().HasMaxLength(80);
        builder.Property(ticket => ticket.CustomerNameSnapshot).IsRequired().HasMaxLength(150);
        builder.Property(ticket => ticket.CustomerPhoneSnapshot).IsRequired().HasMaxLength(30);
        builder.Property(ticket => ticket.DeviceSnapshot).IsRequired().HasMaxLength(300);
        builder.Property(ticket => ticket.Priority).HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(ticket => ticket.IsInternalWorkOrder).HasColumnName("is_internal_work_order").HasDefaultValue(false);
        builder.Property(ticket => ticket.DepartmentCode).HasColumnName("department_code").HasMaxLength(50);
        builder.Property(ticket => ticket.CostCenter).HasColumnName("cost_center").HasMaxLength(60);
        builder.Property(ticket => ticket.SlaDueAt).HasColumnName("sla_due_at");
        builder.Property(ticket => ticket.ServiceLocationType).HasColumnName("service_location_type").HasConversion<string>().IsRequired().HasMaxLength(30).HasDefaultValue(ServiceLocationType.InShop);
        builder.Property(ticket => ticket.SiteLocationId).HasColumnName("site_location_id");
        builder.Property(ticket => ticket.SiteAddressSnapshot).HasColumnName("site_address_snapshot").HasMaxLength(500);
        builder.Property(ticket => ticket.ScheduledVisitStart).HasColumnName("scheduled_visit_start");
        builder.Property(ticket => ticket.ScheduledVisitEnd).HasColumnName("scheduled_visit_end");
        builder.Property(ticket => ticket.ServiceResourceId).HasColumnName("service_resource_id");
        builder.Property(ticket => ticket.DispatchStatus).HasColumnName("dispatch_status").HasConversion<string>().IsRequired().HasMaxLength(30).HasDefaultValue(DispatchStatus.Unscheduled);
        builder.Property(ticket => ticket.VisitStartedAt).HasColumnName("visit_started_at");
        builder.Property(ticket => ticket.VisitCompletedAt).HasColumnName("visit_completed_at");
        builder.Property(ticket => ticket.DispatchNotes).HasColumnName("dispatch_notes").HasMaxLength(1000);
        builder.Property(ticket => ticket.ParentTicketId).HasColumnName("parent_ticket_id");
        builder.Property(ticket => ticket.Complaint).IsRequired().HasMaxLength(2000);
        builder.Property(ticket => ticket.InitialDiagnosis).HasMaxLength(2000);
        builder.Property(ticket => ticket.PhysicalConditionNotes).HasColumnType("text");
        builder.Property(ticket => ticket.AccessorySummary).HasColumnType("text");
        builder.Property(ticket => ticket.PhotoNotes).HasColumnType("text");
        builder.Property(ticket => ticket.SecurityPasscode).HasMaxLength(200);
        builder.Property(ticket => ticket.SecurityPattern).HasMaxLength(100);
        builder.Property(ticket => ticket.PreRepairChecklist).HasColumnType("text");
        builder.Property(ticket => ticket.Diagnosis).HasMaxLength(4000);
        builder.Property(ticket => ticket.RepairNotes).HasMaxLength(4000);
        builder.Property(ticket => ticket.WarrantyTerms).HasMaxLength(1000);
        builder.Property(ticket => ticket.PaymentStatus).HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(ticket => ticket.TotalAmount).HasPrecision(18, 2);
        builder.Property(ticket => ticket.AmountPaid).HasPrecision(18, 2);
        builder.Property(ticket => ticket.DiscountAmount).HasPrecision(18, 2);
        builder.Property(ticket => ticket.PickupOverrideReason).HasMaxLength(500);

        builder.HasIndex(ticket => new { ticket.ShopId, ticket.TicketNumber }).IsUnique();
        builder.HasIndex(ticket => new { ticket.ShopId, ticket.IdempotencyKey }).IsUnique();
        builder.HasIndex(ticket => ticket.PublicTrackingToken).IsUnique();
        builder.HasIndex(ticket => new { ticket.ShopId, ticket.StatusId });
        builder.HasIndex(ticket => new { ticket.ShopId, ticket.ServiceLocationType, ticket.ScheduledVisitStart });
        builder.HasIndex(ticket => new { ticket.ShopId, ticket.ScheduledVisitStart, ticket.ScheduledVisitEnd });
        builder.HasIndex(ticket => new { ticket.ShopId, ticket.IsInternalWorkOrder });
        builder.HasIndex(ticket => new { ticket.ShopId, ticket.DeletedAt, ticket.CreatedAt });
        builder.HasIndex(ticket => new { ticket.ShopId, ticket.DeletedAt, ticket.UpdatedAt });
        builder.HasIndex(ticket => new { ticket.ShopId, ticket.DeletedAt, ticket.Priority, ticket.CreatedAt });
        builder.HasIndex(ticket => new { ticket.ShopId, ticket.WarrantyUntil });

        builder.HasOne(ticket => ticket.Shop)
            .WithMany()
            .HasForeignKey(ticket => ticket.ShopId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(ticket => ticket.Customer)
            .WithMany()
            .HasForeignKey(ticket => ticket.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(ticket => ticket.Device)
            .WithMany()
            .HasForeignKey(ticket => ticket.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(ticket => ticket.Status)
            .WithMany()
            .HasForeignKey(ticket => ticket.StatusId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(ticket => ticket.AssignedTechnicianUser)
            .WithMany()
            .HasForeignKey(ticket => ticket.AssignedTechnicianUserId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(ticket => ticket.SiteLocation)
            .WithMany()
            .HasForeignKey(ticket => ticket.SiteLocationId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(ticket => ticket.ServiceResource)
            .WithMany()
            .HasForeignKey(ticket => ticket.ServiceResourceId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(ticket => ticket.ParentTicket)
            .WithMany()
            .HasForeignKey(ticket => ticket.ParentTicketId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
