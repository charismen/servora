using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Identity.Users;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.Organizations;

public enum OrganizationRole
{
    Owner,
    Admin,
    Executive,
    Member
}

public enum OrganizationUserStatus
{
    Active,
    Inactive
}

public class OrganizationUser : IAuditableEntity
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public OrganizationRole Role { get; set; } = OrganizationRole.Member;
    public OrganizationUserStatus Status { get; set; } = OrganizationUserStatus.Active;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class OrganizationUserConfiguration : IEntityTypeConfiguration<OrganizationUser>
{
    public void Configure(EntityTypeBuilder<OrganizationUser> builder)
    {
        builder.ToTable("organization_users");

        builder.HasKey(ou => ou.Id);
        builder.Property(ou => ou.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(ou => ou.OrganizationId).HasColumnName("organization_id");
        builder.Property(ou => ou.UserId).HasColumnName("user_id");
        builder.Property(ou => ou.Role).HasColumnName("role").HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(ou => ou.Status).HasColumnName("status").HasConversion<string>().IsRequired().HasMaxLength(30);
        builder.Property(ou => ou.CreatedAt).HasColumnName("created_at");
        builder.Property(ou => ou.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(ou => new { ou.OrganizationId, ou.UserId }).IsUnique();
        builder.HasIndex(ou => new { ou.UserId, ou.Status });

        builder.HasOne(ou => ou.Organization)
            .WithMany()
            .HasForeignKey(ou => ou.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ou => ou.User)
            .WithMany()
            .HasForeignKey(ou => ou.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
