using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Identity.Users;
using Servora.Api.Features.Payroll;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.Shops;

public class ShopUser : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public ShopUserStatus Status { get; set; } = ShopUserStatus.Active;
    public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;
    public FreelanceProfile? FreelanceProfile { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public ICollection<ShopUserRole> Roles { get; set; } = new List<ShopUserRole>();
}

public enum ShopUserStatus
{
    Active,
    Inactive
}

public enum EmploymentType
{
    FullTime,
    Freelance
}

public class ShopUserConfiguration : IEntityTypeConfiguration<ShopUser>
{
    public void Configure(EntityTypeBuilder<ShopUser> builder)
    {
        builder.ToTable("workspace_users");

        builder.HasKey(shopUser => shopUser.Id);
        builder.Property(shopUser => shopUser.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(shopUser => shopUser.ShopId).HasColumnName("workspace_id");
        builder.Property(shopUser => shopUser.UserId).HasColumnName("user_id");
        builder.Property(shopUser => shopUser.Status).HasColumnName("status").HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(shopUser => shopUser.EmploymentType).HasColumnName("employment_type").HasConversion<string>().IsRequired().HasMaxLength(30).HasDefaultValue(EmploymentType.FullTime);
        builder.Property(shopUser => shopUser.CreatedAt).HasColumnName("created_at");
        builder.Property(shopUser => shopUser.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(shopUser => new { shopUser.ShopId, shopUser.UserId }).IsUnique();
        builder.HasIndex(shopUser => new { shopUser.ShopId, shopUser.Status });
        builder.HasIndex(shopUser => new { shopUser.ShopId, shopUser.EmploymentType });
        builder.HasOne(shopUser => shopUser.Shop)
            .WithMany()
            .HasForeignKey(shopUser => shopUser.ShopId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(shopUser => shopUser.User)
            .WithMany()
            .HasForeignKey(shopUser => shopUser.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
