using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Identity.Users;

namespace Servora.Api.Features.ServiceTickets;

public class ApprovalRequest
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public int ServiceTicketId { get; set; }
    public ServiceTicket ServiceTicket { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal? EstimatedAmount { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public int RequestedByUserId { get; set; }
    public ApplicationUser RequestedByUser { get; set; } = null!;
    public int? ResolvedByUserId { get; set; }
    public ApplicationUser? ResolvedByUser { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
}

public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled
}

public class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> builder)
    {
        builder.ToTable("approval_requests");

        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).ValueGeneratedOnAdd();
        builder.Property(request => request.Title).IsRequired().HasMaxLength(150);
        builder.Property(request => request.Description).IsRequired().HasMaxLength(2000);
        builder.Property(request => request.EstimatedAmount).HasPrecision(18, 2);
        builder.Property(request => request.Status).HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(request => request.ResolutionNote).HasMaxLength(1000);
        builder.HasIndex(request => new { request.ShopId, request.ServiceTicketId, request.Status });
        builder.HasIndex(request => new { request.ShopId, request.CreatedAt });
        builder.HasOne(request => request.ServiceTicket)
            .WithMany()
            .HasForeignKey(request => request.ServiceTicketId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(request => request.RequestedByUser)
            .WithMany()
            .HasForeignKey(request => request.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(request => request.ResolvedByUser)
            .WithMany()
            .HasForeignKey(request => request.ResolvedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
