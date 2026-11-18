using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Infrastructure.Data.Interceptors;

namespace Servora.Api.Features.Organizations;

public class Organization : IAuditableEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations");

        builder.HasKey(org => org.Id);
        builder.Property(org => org.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(org => org.Name).HasColumnName("name").IsRequired().HasMaxLength(150);
        builder.Property(org => org.Slug).HasColumnName("slug").IsRequired().HasMaxLength(80);
        builder.Property(org => org.LogoUrl).HasColumnName("logo_url").HasMaxLength(500);
        builder.Property(org => org.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(org => org.CreatedAt).HasColumnName("created_at");
        builder.Property(org => org.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(org => org.Slug).IsUnique();
    }
}
