using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.Payroll;

public class BonusTaskDefinition : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public string TaskKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public decimal DefaultStandardRate { get; set; }
    public decimal DefaultGamingRate { get; set; }
    public decimal DefaultCustomerPrice { get; set; }
    public WorkItemType DefaultWorkItemType { get; set; } = WorkItemType.Service;
    public bool DefaultIsInternalOnly { get; set; }
    public string? LegacyKeywords { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class BonusTaskDefinitionConfiguration : IEntityTypeConfiguration<BonusTaskDefinition>
{
    public void Configure(EntityTypeBuilder<BonusTaskDefinition> builder)
    {
        builder.ToTable("bonus_task_definitions");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedOnAdd();
        builder.Property(item => item.TaskKey).IsRequired().HasMaxLength(60);
        builder.Property(item => item.Label).IsRequired().HasMaxLength(150);
        builder.Property(item => item.Description).HasMaxLength(500);
        builder.Property(item => item.Category).HasMaxLength(60);
        builder.Property(item => item.DefaultStandardRate).HasPrecision(18, 2);
        builder.Property(item => item.DefaultGamingRate).HasPrecision(18, 2);
        builder.Property(item => item.DefaultCustomerPrice).HasPrecision(18, 2);
        builder.Property(item => item.DefaultWorkItemType).HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(item => item.LegacyKeywords).HasMaxLength(500);

        builder.HasIndex(item => new { item.ShopId, item.TaskKey }).IsUnique();
        builder.HasIndex(item => new { item.ShopId, item.IsActive, item.SortOrder });

        builder.HasOne(item => item.Shop)
            .WithMany()
            .HasForeignKey(item => item.ShopId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
