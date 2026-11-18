using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Shops;

namespace Servora.Api.Features.Settings;

public class AppSetting
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        builder.ToTable("settings");
        builder.HasOne(setting => setting.Shop)
            .WithMany()
            .HasForeignKey(setting => setting.ShopId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(setting => new { setting.ShopId, setting.Key }).IsUnique();
        builder.Property(setting => setting.Key).HasMaxLength(120).IsRequired();
        builder.Property(setting => setting.Value).HasMaxLength(2000).IsRequired();
    }
}
