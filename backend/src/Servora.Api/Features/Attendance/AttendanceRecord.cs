using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Identity.Users;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.Attendance;

public class AttendanceRecord : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public int ShopUserId { get; set; }
    public ShopUser ShopUser { get; set; } = null!;
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public DateOnly WorkDate { get; set; }
    public DateTimeOffset CheckInAt { get; set; }
    public DateTimeOffset? CheckOutAt { get; set; }
    public int BreakMinutes { get; set; }
    public decimal TotalHours { get; set; }
    public bool IsAdminOverride { get; set; }
    public string? OverrideReason { get; set; }
    public string? Notes { get; set; }
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Pending;
    public int? ApprovedByUserId { get; set; }
    public ApplicationUser? ApprovedByUser { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public enum AttendanceStatus
{
    Pending,
    Approved,
    Rejected
}

public class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("attendances");

        builder.HasKey(attendance => attendance.Id);
        builder.Property(attendance => attendance.Id).ValueGeneratedOnAdd();
        builder.Property(attendance => attendance.TotalHours).HasPrecision(6, 2);
        builder.Property(attendance => attendance.OverrideReason).HasMaxLength(500);
        builder.Property(attendance => attendance.Notes).HasMaxLength(1000);
        builder.Property(attendance => attendance.Status).HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.HasIndex(attendance => new { attendance.ShopId, attendance.WorkDate, attendance.Status });
        builder.HasIndex(attendance => new { attendance.ShopId, attendance.ShopUserId, attendance.WorkDate });
        builder.HasOne(attendance => attendance.Shop)
            .WithMany()
            .HasForeignKey(attendance => attendance.ShopId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(attendance => attendance.ShopUser)
            .WithMany()
            .HasForeignKey(attendance => attendance.ShopUserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(attendance => attendance.User)
            .WithMany()
            .HasForeignKey(attendance => attendance.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(attendance => attendance.ApprovedByUser)
            .WithMany()
            .HasForeignKey(attendance => attendance.ApprovedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

