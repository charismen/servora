using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Identity.Users;
using Servora.Api.Features.Shops;

namespace Servora.Api.Features.Activity;

public class ActivityLog
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public int? ActorUserId { get; set; }
    public ApplicationUser? ActorUser { get; set; }
    public string? ActorRoleContext { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? MetadataJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable("activity_logs");

        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).ValueGeneratedOnAdd();
        builder.Property(log => log.ActorRoleContext).HasMaxLength(300);
        builder.Property(log => log.EntityType).IsRequired().HasMaxLength(80);
        builder.Property(log => log.ActionType).IsRequired().HasMaxLength(80);
        builder.Property(log => log.Title).IsRequired().HasMaxLength(180);
        builder.Property(log => log.Description).HasMaxLength(1000);
        builder.Property(log => log.MetadataJson).HasColumnType("json");
        builder.HasIndex(log => new { log.ShopId, log.CreatedAt });
        builder.HasIndex(log => new { log.ShopId, log.EntityType, log.CreatedAt });
        builder.HasIndex(log => new { log.ShopId, log.ActionType, log.CreatedAt });
        builder.HasOne(log => log.Shop)
            .WithMany()
            .HasForeignKey(log => log.ShopId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(log => log.ActorUser)
            .WithMany()
            .HasForeignKey(log => log.ActorUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
