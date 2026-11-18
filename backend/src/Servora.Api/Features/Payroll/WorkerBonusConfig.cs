using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.Payroll;

public class WorkerBonusConfig : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public int FreelanceProfileId { get; set; }
    public FreelanceProfile FreelanceProfile { get; set; } = null!;
    public int BonusTypeId { get; set; }
    public BonusType BonusType { get; set; } = null!;
    public string ParametersJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class WorkerBonusConfigConfiguration : IEntityTypeConfiguration<WorkerBonusConfig>
{
    public void Configure(EntityTypeBuilder<WorkerBonusConfig> builder)
    {
        builder.ToTable("worker_bonus_configs");

        builder.HasKey(config => config.Id);
        builder.Property(config => config.Id).ValueGeneratedOnAdd();
        builder.Property(config => config.ParametersJson).IsRequired().HasColumnType("json");
        builder.HasIndex(config => new { config.FreelanceProfileId, config.BonusTypeId }).IsUnique();
        builder.HasIndex(config => new { config.ShopId, config.FreelanceProfileId, config.IsActive });
        builder.HasOne(config => config.Shop)
            .WithMany()
            .HasForeignKey(config => config.ShopId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(config => config.FreelanceProfile)
            .WithMany(profile => profile.BonusConfigs)
            .HasForeignKey(config => config.FreelanceProfileId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(config => config.BonusType)
            .WithMany()
            .HasForeignKey(config => config.BonusTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

