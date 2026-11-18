using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Servora.Api.Features.Payroll;

public class BonusType
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public static class BonusTypeCodes
{
    public const string MaintenanceCompletion = "MAINTENANCE_COMPLETION";
    public const string CustomerCancellation = "CUSTOMER_CANCELLATION";
    public const string TaskItemized = "TASK_ITEMIZED";
}

public class BonusTypeConfiguration : IEntityTypeConfiguration<BonusType>
{
    public void Configure(EntityTypeBuilder<BonusType> builder)
    {
        builder.ToTable("bonus_types");

        builder.HasKey(type => type.Id);
        builder.Property(type => type.Id).ValueGeneratedOnAdd();
        builder.Property(type => type.Code).IsRequired().HasMaxLength(80);
        builder.Property(type => type.Name).IsRequired().HasMaxLength(150);
        builder.Property(type => type.Description).HasMaxLength(500);
        builder.HasIndex(type => type.Code).IsUnique();
    }
}

