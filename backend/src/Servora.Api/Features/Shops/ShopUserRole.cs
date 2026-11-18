using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Identity.Roles;

namespace Servora.Api.Features.Shops;

public class ShopUserRole
{
    public int Id { get; set; }
    public int ShopUserId { get; set; }
    public ShopUser ShopUser { get; set; } = null!;
    public int RoleId { get; set; }
    public ApplicationRole Role { get; set; } = null!;
}

public class ShopUserRoleConfiguration : IEntityTypeConfiguration<ShopUserRole>
{
    public void Configure(EntityTypeBuilder<ShopUserRole> builder)
    {
        builder.ToTable("workspace_user_roles");

        builder.HasKey(role => role.Id);
        builder.Property(role => role.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(role => role.ShopUserId).HasColumnName("workspace_user_id");
        builder.Property(role => role.RoleId).HasColumnName("role_id");
        builder.HasIndex(role => new { role.ShopUserId, role.RoleId }).IsUnique();
        builder.HasOne(role => role.ShopUser)
            .WithMany(shopUser => shopUser.Roles)
            .HasForeignKey(role => role.ShopUserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(role => role.Role)
            .WithMany()
            .HasForeignKey(role => role.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
