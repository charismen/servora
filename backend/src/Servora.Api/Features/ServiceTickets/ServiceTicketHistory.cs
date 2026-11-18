using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Identity.Users;
using Servora.Api.Features.ServiceStatuses;

namespace Servora.Api.Features.ServiceTickets;

public class ServiceTicketHistory
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public int ServiceTicketId { get; set; }
    public ServiceTicket ServiceTicket { get; set; } = null!;
    public int? ActorUserId { get; set; }
    public ApplicationUser? ActorUser { get; set; }
    public TicketHistoryEventType EventType { get; set; }
    public int? FromStatusId { get; set; }
    public ServiceStatus? FromStatus { get; set; }
    public int? ToStatusId { get; set; }
    public ServiceStatus? ToStatus { get; set; }
    public string? Reason { get; set; }
    public string? Note { get; set; }
    public string? MetadataJson { get; set; }
    public bool IsPublicVisible { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum TicketHistoryEventType
{
    Created,
    Updated,
    StatusChanged,
    NoteAdded,
    Assigned,
    ApprovalRequested,
    ApprovalResolved,
    PaymentUpdated,
    PickupOverride,
    ReceiptPrinted
}

public class ServiceTicketHistoryConfiguration : IEntityTypeConfiguration<ServiceTicketHistory>
{
    public void Configure(EntityTypeBuilder<ServiceTicketHistory> builder)
    {
        builder.ToTable("service_ticket_histories");

        builder.HasKey(history => history.Id);
        builder.Property(history => history.Id).ValueGeneratedOnAdd();
        builder.Property(history => history.EventType).HasConversion<string>().IsRequired().HasMaxLength(40);
        builder.Property(history => history.Reason).HasMaxLength(500);
        builder.Property(history => history.Note).HasMaxLength(4000);
        builder.Property(history => history.MetadataJson).HasColumnType("json");
        builder.HasIndex(history => new { history.ShopId, history.ServiceTicketId, history.CreatedAt });
        builder.HasOne(history => history.ServiceTicket)
            .WithMany()
            .HasForeignKey(history => history.ServiceTicketId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(history => history.ActorUser)
            .WithMany()
            .HasForeignKey(history => history.ActorUserId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(history => history.FromStatus)
            .WithMany()
            .HasForeignKey(history => history.FromStatusId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(history => history.ToStatus)
            .WithMany()
            .HasForeignKey(history => history.ToStatusId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
