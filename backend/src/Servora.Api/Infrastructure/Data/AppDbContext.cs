using Servora.Api.Infrastructure.Data.Interceptors;
using Servora.Api.Features.ServiceStatuses;
using Servora.Api.Features.Settings;
using Servora.Api.Features.Customers;
using Servora.Api.Features.Devices;
using Servora.Api.Features.Dispatch;
using Servora.Api.Features.ServiceTickets;
using Servora.Api.Features.ServiceTemplates;
using Servora.Api.Features.Notifications;
using Servora.Api.Features.Inventory;
using Servora.Api.Features.Activity;
using Servora.Api.Features.Shops;
using Servora.Api.Features.Organizations;
using Servora.Api.Features.Blueprints;
using Servora.Api.Features.Maintenance;
using Servora.Api.Features.Payments;
using Servora.Api.Features.Attendance;
using Servora.Api.Features.Payroll;
using Servora.Api.Features.Assets;
using Servora.Api.Features.Identity.Users;
using Servora.Api.Features.Identity.Roles;
using Servora.Api.Features.Identity.Permissions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Servora.Api.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<
    ApplicationUser,
    ApplicationRole,
    int,
    ApplicationUserClaim,
    ApplicationUserRole,
    ApplicationUserLogin,
    ApplicationRoleClaim,
    ApplicationUserToken>
{
    private readonly AuditableEntityInterceptor _auditableEntityInterceptor;
    private readonly AuditLogInterceptor _auditLogInterceptor;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        AuditableEntityInterceptor auditableEntityInterceptor,
        AuditLogInterceptor auditLogInterceptor)
        : base(options)
    {
        _auditableEntityInterceptor = auditableEntityInterceptor;
        _auditLogInterceptor = auditLogInterceptor;
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationUser> OrganizationUsers => Set<OrganizationUser>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerLocation> CustomerLocations => Set<CustomerLocation>();
    public DbSet<ServiceStatus> ServiceStatuses => Set<ServiceStatus>();
    public DbSet<Shop> Shops => Set<Shop>();
    public DbSet<ShopUser> ShopUsers => Set<ShopUser>();
    public DbSet<ShopUserRole> ShopUserRoles => Set<ShopUserRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<AssetFieldDefinition> AssetFieldDefinitions => Set<AssetFieldDefinition>();
    public DbSet<AssetFieldValue> AssetFieldValues => Set<AssetFieldValue>();
    public DbSet<TicketFieldValue> TicketFieldValues => Set<TicketFieldValue>();
    public DbSet<InspectionChecklistTemplate> InspectionChecklistTemplates => Set<InspectionChecklistTemplate>();
    public DbSet<TicketInspectionResult> TicketInspectionResults => Set<TicketInspectionResult>();
    public DbSet<ServiceTicket> ServiceTickets => Set<ServiceTicket>();
    public DbSet<ServiceTicketAsset> ServiceTicketAssets => Set<ServiceTicketAsset>();
    public DbSet<ServiceTicketAccessory> ServiceTicketAccessories => Set<ServiceTicketAccessory>();
    public DbSet<ServiceTicketAttachment> ServiceTicketAttachments => Set<ServiceTicketAttachment>();
    public DbSet<ServiceTemplate> ServiceTemplates => Set<ServiceTemplate>();
    public DbSet<ServiceTemplateChecklistItem> ServiceTemplateChecklistItems => Set<ServiceTemplateChecklistItem>();
    public DbSet<ServiceTicketChecklistItem> ServiceTicketChecklistItems => Set<ServiceTicketChecklistItem>();
    public DbSet<ServiceTicketHistory> ServiceTicketHistories => Set<ServiceTicketHistory>();
    public DbSet<StatusTransitionRule> StatusTransitionRules => Set<StatusTransitionRule>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();
    public DbSet<WorkspacePaymentGateway> WorkspacePaymentGateways => Set<WorkspacePaymentGateway>();
    public DbSet<PaymentCheckoutIntent> PaymentCheckoutIntents => Set<PaymentCheckoutIntent>();
    public DbSet<PaymentWebhookEvent> PaymentWebhookEvents => Set<PaymentWebhookEvent>();
    public DbSet<ServiceTicketWorkItem> ServiceTicketWorkItems => Set<ServiceTicketWorkItem>();
    public DbSet<MaintenanceSchedule> MaintenanceSchedules => Set<MaintenanceSchedule>();
    public DbSet<ServiceResource> ServiceResources => Set<ServiceResource>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<WorkspaceNotificationChannel> WorkspaceNotificationChannels => Set<WorkspaceNotificationChannel>();
    public DbSet<WorkspaceNotificationRule> WorkspaceNotificationRules => Set<WorkspaceNotificationRule>();
    public DbSet<WorkspaceWebhookEndpoint> WorkspaceWebhookEndpoints => Set<WorkspaceWebhookEndpoint>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public DbSet<OtpVerificationChallenge> OtpVerificationChallenges => Set<OtpVerificationChallenge>();
    public DbSet<InventoryCategory> InventoryCategories => Set<InventoryCategory>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<TicketInventoryUsage> TicketInventoryUsages => Set<TicketInventoryUsage>();
    public DbSet<InventoryImportBatch> InventoryImportBatches => Set<InventoryImportBatch>();
    public DbSet<InventoryImportRow> InventoryImportRows => Set<InventoryImportRow>();
    public DbSet<FreelanceProfile> FreelanceProfiles => Set<FreelanceProfile>();
    public DbSet<BonusType> BonusTypes => Set<BonusType>();
    public DbSet<WorkerBonusConfig> WorkerBonusConfigs => Set<WorkerBonusConfig>();
    public DbSet<PayrollAdjustment> PayrollAdjustments => Set<PayrollAdjustment>();
    public DbSet<AttendanceRecord> Attendances => Set<AttendanceRecord>();
    public DbSet<AssetCategory> AssetCategories => Set<AssetCategory>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetCustodyLog> AssetCustodyLogs => Set<AssetCustodyLog>();
    public DbSet<AssetMaintenanceLog> AssetMaintenanceLogs => Set<AssetMaintenanceLog>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(builder =>
        {
            builder.ToTable("users");
            builder.Property(user => user.Id).ValueGeneratedOnAdd();
            builder.Property(user => user.FirstName).HasColumnName("first_name");
            builder.Property(user => user.LastName).HasColumnName("last_name");
            builder.Property(user => user.IsActive).HasColumnName("is_active");
            builder.Property(user => user.PreferredTwoFactorMethod).HasColumnName("preferred_two_factor_method").HasConversion<string>().IsRequired().HasMaxLength(30).HasDefaultValue(TwoFactorMethod.None);
            builder.Property(user => user.CreatedAt).HasColumnName("created_at");
            builder.Property(user => user.UpdatedAt).HasColumnName("updated_at");
            builder.HasIndex(user => new { user.FirstName, user.LastName });
            builder.HasIndex(user => user.Email);
        });

        modelBuilder.Entity<ApplicationRole>(builder =>
        {
            builder.ToTable("roles");
            builder.Property(role => role.Id).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<ApplicationUserRole>(builder =>
        {
            builder.ToTable("user_roles");
            builder.HasKey(userRole => userRole.Id);
            builder.Property(userRole => userRole.Id).ValueGeneratedOnAdd();
            builder.HasIndex(userRole => new { userRole.UserId, userRole.RoleId }).IsUnique();
        });

        modelBuilder.Entity<ApplicationUserClaim>(builder =>
        {
            builder.ToTable("user_claims");
            builder.Property(userClaim => userClaim.Id).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<ApplicationUserLogin>(builder =>
        {
            builder.ToTable("user_logins");
            builder.HasKey(userLogin => userLogin.Id);
            builder.Property(userLogin => userLogin.Id).ValueGeneratedOnAdd();
            builder.HasIndex(userLogin => new { userLogin.LoginProvider, userLogin.ProviderKey }).IsUnique();
        });

        modelBuilder.Entity<ApplicationRoleClaim>(builder =>
        {
            builder.ToTable("role_claims");
            builder.Property(roleClaim => roleClaim.Id).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<ApplicationUserToken>(builder =>
        {
            builder.ToTable("user_tokens");
            builder.HasKey(userToken => userToken.Id);
            builder.Property(userToken => userToken.Id).ValueGeneratedOnAdd();
            builder.HasIndex(userToken => new { userToken.UserId, userToken.LoginProvider, userToken.Name }).IsUnique();
        });

        modelBuilder.Entity<RolePermission>()
            .ToTable("role_permissions");

        modelBuilder.Entity<RolePermission>()
            .Property(rp => rp.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<RolePermission>()
            .HasIndex(rp => new { rp.RoleId, rp.PermissionId })
            .IsUnique();

        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId)
            .IsRequired();

        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.Permission)
            .WithMany()
            .HasForeignKey(rp => rp.PermissionId)
            .IsRequired();

        modelBuilder.Entity<AuditLog>(builder =>
        {
            builder.ToTable("audit_logs");
            builder.Property(log => log.Id).ValueGeneratedOnAdd();
        });

        // Apply all IEntityTypeConfiguration<T> implementations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Enforce consistent workspace_id / workspace_user_id physical column names across all scoped tables
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.Name == "ShopId")
                {
                    property.SetColumnName("workspace_id");
                }
                else if (property.Name == "ShopUserId")
                {
                    property.SetColumnName("workspace_user_id");
                }
            }
        }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(_auditableEntityInterceptor, _auditLogInterceptor);
        base.OnConfiguring(optionsBuilder);
    }
}
