using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.Payroll;

public class FreelanceProfile : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public int ShopUserId { get; set; }
    public ShopUser ShopUser { get; set; } = null!;
    public int? CompensationTemplateId { get; set; }
    public CompensationTemplate? CompensationTemplate { get; set; }
    public BasePayMode BasePayMode { get; set; } = BasePayMode.TieredDaily;
    public decimal BaseHourlyRate { get; set; }
    public string TieredRatesJson { get; set; } = DefaultTieredRatesJson;
    public string PayPolicyJson { get; set; } = DefaultPayPolicyJson;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public ICollection<WorkerBonusConfig> BonusConfigs { get; set; } = new List<WorkerBonusConfig>();

    public const string DefaultTieredRatesJson = """[{"minHours":5.0,"maxHours":6.0,"flatRate":35000,"hourlyRate":null},{"minHours":8.0,"maxHours":24.0,"flatRate":60000,"hourlyRate":null}]""";
    public const string DefaultPayPolicyJson = """{}""";
}

public enum BasePayMode
{
    Hourly,
    TieredDaily,
    TieredDailyAverage,
    TieredMonthly,
    FixedDaily,
    FixedMonthly,
    CommissionOnly,
    RevenueShare
}

public enum FallbackPayStrategy
{
    ProratedHourly,
    LowerTierFloor,
    Zero
}

public sealed record WageTierBracket(
    decimal MinHours,
    decimal MaxHours,
    decimal FlatRate,
    decimal? HourlyRate = null);

public sealed record FreelancePayPolicy(
    decimal FixedDailyRate = 0m,
    decimal FixedMonthlySalary = 0m,
    int StandardWorkDaysPerMonth = 26,
    bool ProrateMonthlyByAttendance = false,
    decimal RevenueSharePercent = 0m,
    FallbackPayStrategy FallbackStrategy = FallbackPayStrategy.ProratedHourly,
    decimal? OvertimeThresholdHoursPerDay = null,
    decimal OvertimeMultiplier = 1.0m,
    decimal WeekendMultiplier = 1.0m,
    string TaskBonusTrigger = "TicketFinalized");

public class FreelanceProfileConfiguration : IEntityTypeConfiguration<FreelanceProfile>
{
    public void Configure(EntityTypeBuilder<FreelanceProfile> builder)
    {
        builder.ToTable("freelance_profiles");

        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.Id).ValueGeneratedOnAdd();
        builder.Property(profile => profile.BasePayMode).HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(profile => profile.BaseHourlyRate).HasPrecision(18, 2);
        builder.Property(profile => profile.TieredRatesJson).IsRequired().HasColumnType("json");
        builder.Property(profile => profile.PayPolicyJson).IsRequired().HasColumnType("json");
        builder.HasIndex(profile => new { profile.ShopId, profile.ShopUserId }).IsUnique();
        builder.HasIndex(profile => profile.CompensationTemplateId);
        builder.HasOne(profile => profile.Shop)
            .WithMany()
            .HasForeignKey(profile => profile.ShopId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(profile => profile.ShopUser)
            .WithOne(shopUser => shopUser.FreelanceProfile)
            .HasForeignKey<FreelanceProfile>(profile => profile.ShopUserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(profile => profile.CompensationTemplate)
            .WithMany()
            .HasForeignKey(profile => profile.CompensationTemplateId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
