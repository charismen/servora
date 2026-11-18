using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Identity.Users;

namespace Servora.Api.Features.ServiceTickets;

public class PaymentRecord
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public int ServiceTicketId { get; set; }
    public ServiceTicket ServiceTicket { get; set; } = null!;
    public PaymentStatus Status { get; set; } = PaymentStatus.Unpaid;
    public PaymentEntryType EntryType { get; set; } = PaymentEntryType.Payment;
    public decimal TransactionAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal? TotalAmount { get; set; }
    public string? Method { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public int UpdatedByUserId { get; set; }
    public ApplicationUser UpdatedByUser { get; set; } = null!;
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAt { get; set; }
    public int? VoidedByUserId { get; set; }
    public ApplicationUser? VoidedByUser { get; set; }
    public string? VoidReason { get; set; }
}

public enum PaymentEntryType
{
    Payment,
    Refund,
    BillingUpdate
}

public class PaymentRecordConfiguration : IEntityTypeConfiguration<PaymentRecord>
{
    public void Configure(EntityTypeBuilder<PaymentRecord> builder)
    {
        builder.ToTable("payment_records");

        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).ValueGeneratedOnAdd();
        builder.Property(record => record.Status).HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(record => record.EntryType).HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(record => record.TransactionAmount).HasPrecision(18, 2);
        builder.Property(record => record.AmountPaid).HasPrecision(18, 2);
        builder.Property(record => record.TotalAmount).HasPrecision(18, 2);
        builder.Property(record => record.Method).HasMaxLength(80);
        builder.Property(record => record.ReferenceNumber).HasMaxLength(120);
        builder.Property(record => record.Notes).HasMaxLength(1000);
        builder.Property(record => record.VoidReason).HasMaxLength(500);
        builder.HasIndex(record => new { record.ShopId, record.ServiceTicketId, record.CreatedAt });
        builder.HasIndex(record => new { record.ShopId, record.CreatedAt });
        builder.HasIndex(record => new { record.ShopId, record.IsVoided, record.RecordedAt });
        builder.HasOne(record => record.ServiceTicket)
            .WithMany()
            .HasForeignKey(record => record.ServiceTicketId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(record => record.UpdatedByUser)
            .WithMany()
            .HasForeignKey(record => record.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(record => record.VoidedByUser)
            .WithMany()
            .HasForeignKey(record => record.VoidedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
