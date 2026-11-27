using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.Payroll;

public class CompensationTemplate : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public BasePayMode BasePayMode { get; set; } = BasePayMode.TieredDaily;
    public decimal BaseHourlyRate { get; set; }
    public string TieredRatesJson { get; set; } = FreelanceProfile.DefaultTieredRatesJson;
    public string PayPolicyJson { get; set; } = FreelanceProfile.DefaultPayPolicyJson;
    public string BonusConfigsJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class CompensationTemplateConfiguration : IEntityTypeConfiguration<CompensationTemplate>
{
    public void Configure(EntityTypeBuilder<CompensationTemplate> builder)
    {
        builder.ToTable("compensation_templates");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedOnAdd();
        builder.Property(item => item.Name).IsRequired().HasMaxLength(120);
        builder.Property(item => item.Description).HasMaxLength(500);
        builder.Property(item => item.BasePayMode).HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(item => item.BaseHourlyRate).HasPrecision(18, 2);
        builder.Property(item => item.TieredRatesJson).IsRequired().HasColumnType("json");
        builder.Property(item => item.PayPolicyJson).IsRequired().HasColumnType("json");
        builder.Property(item => item.BonusConfigsJson).IsRequired().HasColumnType("json");

        builder.HasIndex(item => new { item.ShopId, item.Name }).IsUnique();
        builder.HasIndex(item => new { item.ShopId, item.IsActive, item.SortOrder });

        builder.HasOne(item => item.Shop)
            .WithMany()
            .HasForeignKey(item => item.ShopId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
