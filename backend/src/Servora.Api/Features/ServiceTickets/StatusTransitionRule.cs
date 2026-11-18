using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.ServiceStatuses;
using Servora.Api.Features.Shops;

namespace Servora.Api.Features.ServiceTickets;

public class StatusTransitionRule
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public int FromStatusId { get; set; }
    public ServiceStatus FromStatus { get; set; } = null!;
    public int ToStatusId { get; set; }
    public ServiceStatus ToStatus { get; set; } = null!;
    public bool RequiresReason { get; set; }
    public bool RequiresApproval { get; set; }
    public bool RequiresPaymentCompleted { get; set; }
    public string? RequiredPermission { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class StatusTransitionRuleConfiguration : IEntityTypeConfiguration<StatusTransitionRule>
{
    public void Configure(EntityTypeBuilder<StatusTransitionRule> builder)
    {
        builder.ToTable("status_transition_rules");

        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.Id).ValueGeneratedOnAdd();
        builder.Property(rule => rule.RequiredPermission).HasMaxLength(120);
        builder.HasIndex(rule => new { rule.ShopId, rule.FromStatusId, rule.ToStatusId }).IsUnique();
        builder.HasOne(rule => rule.Shop)
            .WithMany()
            .HasForeignKey(rule => rule.ShopId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(rule => rule.FromStatus)
            .WithMany()
            .HasForeignKey(rule => rule.FromStatusId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(rule => rule.ToStatus)
            .WithMany()
            .HasForeignKey(rule => rule.ToStatusId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
