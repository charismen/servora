using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Identity.Users;
using Servora.Api.Features.Shops;

namespace Servora.Api.Features.Assets;

public class AssetCustodyLog
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public int AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
    public int? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public AssetCustodyActionType ActionType { get; set; } = AssetCustodyActionType.CheckOut;
    public string? Location { get; set; }
    public AssetCondition Condition { get; set; } = AssetCondition.Good;
    public string? Notes { get; set; }
    public int? RecordedByUserId { get; set; }
    public ApplicationUser? RecordedByUser { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum AssetCustodyActionType
{
    CheckOut,
    CheckIn,
    Transfer
}

public class AssetCustodyLogConfiguration : IEntityTypeConfiguration<AssetCustodyLog>
{
    public void Configure(EntityTypeBuilder<AssetCustodyLog> builder)
    {
        builder.ToTable("workspace_asset_custody_logs");
        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).ValueGeneratedOnAdd();
        builder.Property(log => log.ShopId).HasColumnName("workspace_id");
        builder.Property(log => log.AssetId).HasColumnName("asset_id");
        builder.Property(log => log.UserId).HasColumnName("user_id");
        builder.Property(log => log.ActionType).HasColumnName("action_type").HasConversion<string>().IsRequired().HasMaxLength(40);
        builder.Property(log => log.Location).HasColumnName("location").HasMaxLength(180);
        builder.Property(log => log.Condition).HasColumnName("condition").HasConversion<string>().IsRequired().HasMaxLength(40);
        builder.Property(log => log.Notes).HasColumnName("notes").HasMaxLength(500);
        builder.Property(log => log.RecordedByUserId).HasColumnName("recorded_by_user_id");
        builder.Property(log => log.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(log => new { log.ShopId, log.AssetId, log.CreatedAt });

        builder.HasOne(log => log.Shop)
            .WithMany()
            .HasForeignKey(log => log.ShopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(log => log.Asset)
            .WithMany(asset => asset.CustodyLogs)
            .HasForeignKey(log => log.AssetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(log => log.User)
            .WithMany()
            .HasForeignKey(log => log.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(log => log.RecordedByUser)
            .WithMany()
            .HasForeignKey(log => log.RecordedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
