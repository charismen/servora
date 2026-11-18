using Servora.Api.Infrastructure.Data.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Api.Features.Shops;

namespace Servora.Api.Features.Customers;

public enum RequesterType
{
    Customer,
    InternalEmployee,
    Department,
    Branch
}

public class Customer : IAuditableEntity
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public Shop Shop { get; set; } = null!;
    public RequesterType RequesterType { get; set; } = RequesterType.Customer;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? CompanyName { get; set; }
    public string? DepartmentName { get; set; }
    public string? DepartmentCode { get; set; }
    public string? CostCenter { get; set; }
    public string? EmployeeId { get; set; }
    public bool IsBanned { get; set; }
    public string? BanReason { get; set; }
    public bool IsStarred { get; set; }
    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedOnAdd();
        builder.HasOne(c => c.Shop)
            .WithMany()
            .HasForeignKey(c => c.ShopId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.RequesterType)
            .HasColumnName("requester_type")
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(30)
            .HasDefaultValue(RequesterType.Customer);

        builder.Property(c => c.FullName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(c => c.PhoneNumber)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(c => c.Email)
            .HasMaxLength(256);

        builder.Property(c => c.Address)
            .HasMaxLength(500);

        builder.Property(c => c.CompanyName)
            .HasColumnName("company_name")
            .HasMaxLength(150);

        builder.Property(c => c.DepartmentName)
            .HasColumnName("department_name")
            .HasMaxLength(120);

        builder.Property(c => c.DepartmentCode)
            .HasColumnName("department_code")
            .HasMaxLength(50);

        builder.Property(c => c.CostCenter)
            .HasColumnName("cost_center")
            .HasMaxLength(60);

        builder.Property(c => c.EmployeeId)
            .HasColumnName("employee_id")
            .HasMaxLength(60);

        builder.Property(c => c.BanReason)
            .HasMaxLength(500);

        builder.Property(c => c.Notes)
            .HasMaxLength(2000);

        builder.HasIndex(c => new { c.ShopId, c.FullName });
        builder.HasIndex(c => new { c.ShopId, c.PhoneNumber });
        builder.HasIndex(c => new { c.ShopId, c.RequesterType });
        builder.HasIndex(c => new { c.ShopId, c.DepartmentCode });
        builder.HasIndex(c => new { c.ShopId, c.IsStarred, c.CreatedAt });
        builder.HasIndex(c => new { c.ShopId, c.CreatedAt });
        builder.HasIndex(c => new { c.ShopId, c.DeletedAt, c.CreatedAt });
    }
}
