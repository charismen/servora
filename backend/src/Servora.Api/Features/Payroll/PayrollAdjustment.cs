using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Identity.Users;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.Payroll;

public class PayrollAdjustment : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public int ShopUserId { get; set; }
    public ShopUser ShopUser { get; set; } = null!;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class PayrollAdjustmentConfiguration : IEntityTypeConfiguration<PayrollAdjustment>
{
    public void Configure(EntityTypeBuilder<PayrollAdjustment> builder)
    {
        builder.ToTable("payroll_adjustments");

        builder.HasKey(adjustment => adjustment.Id);
        builder.Property(adjustment => adjustment.Id).ValueGeneratedOnAdd();
        builder.Property(adjustment => adjustment.Amount).HasPrecision(18, 2);
        builder.Property(adjustment => adjustment.Reason).IsRequired().HasMaxLength(500);
        builder.HasIndex(adjustment => new { adjustment.ShopId, adjustment.ShopUserId, adjustment.PeriodStart, adjustment.PeriodEnd });
        builder.HasOne(adjustment => adjustment.Shop)
            .WithMany()
            .HasForeignKey(adjustment => adjustment.ShopId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(adjustment => adjustment.ShopUser)
            .WithMany()
            .HasForeignKey(adjustment => adjustment.ShopUserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(adjustment => adjustment.CreatedByUser)
            .WithMany()
            .HasForeignKey(adjustment => adjustment.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

