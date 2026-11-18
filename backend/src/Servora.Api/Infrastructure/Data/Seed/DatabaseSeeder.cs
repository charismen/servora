using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Servora.Api.Features.Assets;
using Servora.Api.Features.Blueprints;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Features.Identity.Roles;
using Servora.Api.Features.Identity.Users;
using Servora.Api.Features.Organizations;
using Servora.Api.Features.Payroll;
using Servora.Api.Features.Shops;

namespace Servora.Api.Infrastructure.Data.Seed;

public static class DatabaseSeeder
{
    private static readonly RoleSeed[] Roles =
    [
        new("Owner/Admin", "Full access to workspace configuration, blueprints, users, operations, inventory, payroll, and reporting.", ApplicationPermissions.All),
        new("Front Desk", "Can receive service assets, manage customers/requesters, create tickets, print receipts, and manage schedules.",
        [
            ApplicationPermissions.CustomersRead,
            ApplicationPermissions.CustomersWrite,
            ApplicationPermissions.DevicesRead,
            ApplicationPermissions.DevicesWrite,
            ApplicationPermissions.ServiceTicketsRead,
            ApplicationPermissions.ServiceTicketsWrite,
            ApplicationPermissions.ServiceTicketsMove,
            ApplicationPermissions.InventoryRead,
            ApplicationPermissions.AttendanceRead,
            ApplicationPermissions.AssetsRead,
            ApplicationPermissions.MaintenanceRead,
            ApplicationPermissions.MaintenanceWrite
        ]),
        new("Technician", "Can inspect assigned work orders, update custom fields and checklists, consume parts, and log attendance.",
        [
            ApplicationPermissions.CustomersRead,
            ApplicationPermissions.DevicesRead,
            ApplicationPermissions.DevicesWrite,
            ApplicationPermissions.ServiceTicketsRead,
            ApplicationPermissions.ServiceTicketsWrite,
            ApplicationPermissions.ServiceTicketsMove,
            ApplicationPermissions.InventoryRead,
            ApplicationPermissions.InventoryWrite,
            ApplicationPermissions.AttendanceRead,
            ApplicationPermissions.AssetsRead,
            ApplicationPermissions.MaintenanceRead,
            ApplicationPermissions.MaintenanceWrite
        ]),
        new("Viewer/Staff", "Read-only operational visibility across tickets, assets, schedules, and reports.",
        [
            ApplicationPermissions.CustomersRead,
            ApplicationPermissions.DevicesRead,
            ApplicationPermissions.ServiceTicketsRead,
            ApplicationPermissions.InventoryRead,
            ApplicationPermissions.ReportsRead,
            ApplicationPermissions.AttendanceRead,
            ApplicationPermissions.AssetsRead,
            ApplicationPermissions.MaintenanceRead
        ])
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var context = services.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync(cancellationToken);

        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = services.GetRequiredService<IConfiguration>();
        var blueprintService = services.GetRequiredService<BlueprintProvisioningService>();

        var organization = await SeedDefaultOrganizationAsync(context, cancellationToken);
        var defaultWorkspace = await SeedDefaultWorkspaceAsync(context, organization.Id, configuration, cancellationToken);

        await SeedPermissionsAsync(context, cancellationToken);

        foreach (var roleSeed in Roles)
        {
            var role = await roleManager.FindByNameAsync(roleSeed.Name);
            if (role is null)
            {
                role = new ApplicationRole
                {
                    Name = roleSeed.Name,
                    Description = roleSeed.Description
                };

                var result = await roleManager.CreateAsync(role);
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException($"Failed to seed role '{roleSeed.Name}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }

            await SeedRolePermissionsAsync(context, role.Id, roleSeed.Permissions, cancellationToken);
        }

        var adminUser = await SeedAdminUserAsync(userManager, context, configuration, organization.Id, defaultWorkspace.Id, cancellationToken);
        await SeedBonusTypesAsync(context, cancellationToken);
        await blueprintService.ApplyBlueprintAsync(defaultWorkspace, defaultWorkspace.IndustryBlueprintKey, cancellationToken: cancellationToken);
        await SeedAssetCategoriesAsync(context, defaultWorkspace.Id, cancellationToken);

        var hostEnvironment = services.GetService<IHostEnvironment>();
        var isProduction = hostEnvironment?.IsProduction() == true;
        if (configuration.GetValue("SeedData:IncludeSimulationData", !isProduction))
        {
            await blueprintService.SeedSampleDataForBlueprintAsync(defaultWorkspace, cancellationToken);
            await SeedSecondaryInternalWorkspaceAsync(context, blueprintService, organization.Id, adminUser.Id, cancellationToken);
            await SeedDemoStaffAsync(userManager, context, defaultWorkspace.Id, cancellationToken);
        }
    }

    private static async Task<Organization> SeedDefaultOrganizationAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var org = await context.Organizations.FirstOrDefaultAsync(o => o.Slug == "servora-group", cancellationToken);
        if (org is not null) return org;

        org = new Organization
        {
            Name = "Servora Operations Group",
            Slug = "servora-group",
            IsActive = true
        };

        context.Organizations.Add(org);
        await context.SaveChangesAsync(cancellationToken);
        return org;
    }

    private static async Task<Shop> SeedDefaultWorkspaceAsync(
        AppDbContext context,
        int organizationId,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var workspace = await context.Shops.FirstOrDefaultAsync(w => w.Slug == "main-workspace", cancellationToken);
        if (workspace is not null) return workspace;

        var defaultBlueprint = IndustryBlueprintCatalog.GetByKey("it_computer");
        var markSetupCompleted = configuration.GetValue("SeedData:MarkSetupCompleted", false);

        workspace = new Shop
        {
            OrganizationId = organizationId,
            Name = "Servora Service Center",
            Slug = "main-workspace",
            PhoneNumber = "+62 812-0000-0000",
            Address = "Main Service Workshop",
            OperatingMode = OperatingMode.Commercial,
            IndustryBlueprintKey = defaultBlueprint.Key,
            TerminologyConfig = IndustryBlueprintCatalog.SerializeTerminology(defaultBlueprint.Terminology),
            DefaultLocale = "en",
            TimeZone = "Asia/Jakarta",
            CurrencyCode = "IDR",
            SetupCompleted = markSetupCompleted,
            IsActive = true
        };

        context.Shops.Add(workspace);
        await context.SaveChangesAsync(cancellationToken);
        return workspace;
    }

    private static async Task SeedSecondaryInternalWorkspaceAsync(
        AppDbContext context,
        BlueprintProvisioningService blueprintService,
        int organizationId,
        int adminUserId,
        CancellationToken cancellationToken)
    {
        var internalWorkspace = await context.Shops.FirstOrDefaultAsync(w => w.Slug == "internal-facilities", cancellationToken);
        if (internalWorkspace is null)
        {
            var facilityBlueprint = IndustryBlueprintCatalog.GetByKey("facility_internal");
            internalWorkspace = new Shop
            {
                OrganizationId = organizationId,
                Name = "Internal Facilities & IT Division",
                Slug = "internal-facilities",
                PhoneNumber = "+62 21-555-0199",
                Address = "Corporate HQ — Maintenance Wing",
                OperatingMode = OperatingMode.InternalMaintenance,
                IndustryBlueprintKey = facilityBlueprint.Key,
                TerminologyConfig = IndustryBlueprintCatalog.SerializeTerminology(facilityBlueprint.Terminology),
                DefaultLocale = "en",
                TimeZone = "Asia/Jakarta",
                CurrencyCode = "IDR",
                SetupCompleted = true,
                IsActive = true
            };

            context.Shops.Add(internalWorkspace);
            await context.SaveChangesAsync(cancellationToken);
        }

        await EnsureWorkspaceMembershipAsync(context, internalWorkspace.Id, adminUserId, "Owner/Admin", EmploymentType.FullTime, cancellationToken);
        await blueprintService.ApplyBlueprintAsync(internalWorkspace, internalWorkspace.IndustryBlueprintKey, cancellationToken: cancellationToken);
        await SeedAssetCategoriesAsync(context, internalWorkspace.Id, cancellationToken);
        await blueprintService.SeedSampleDataForBlueprintAsync(internalWorkspace, cancellationToken);
    }

    private static async Task SeedPermissionsAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var existingCodes = await context.Permissions
            .Select(permission => permission.Code)
            .ToListAsync(cancellationToken);

        context.Permissions.AddRange(ApplicationPermissions.All
            .Except(existingCodes)
            .Select(code => new Permission { Code = code }));

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedRolePermissionsAsync(
        AppDbContext context,
        int roleId,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken)
    {
        var permissionIds = await context.Permissions
            .Where(permission => permissions.Contains(permission.Code))
            .ToDictionaryAsync(permission => permission.Code, permission => permission.Id, cancellationToken);

        var existingPermissions = await context.RolePermissions
            .Where(permission => permission.RoleId == roleId)
            .Select(permission => permission.Permission.Code)
            .ToListAsync(cancellationToken);

        var newPermissions = permissions
            .Except(existingPermissions)
            .Select(permission => new RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionIds[permission]
            });

        context.RolePermissions.AddRange(newPermissions);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<ApplicationUser> SeedAdminUserAsync(
        UserManager<ApplicationUser> userManager,
        AppDbContext context,
        IConfiguration configuration,
        int organizationId,
        int workspaceId,
        CancellationToken cancellationToken)
    {
        var email = configuration["SeedData:Admin:Email"] ?? "admin@example.com";
        var password = configuration["SeedData:Admin:Password"] ?? "Admin123!";
        var firstName = configuration["SeedData:Admin:FirstName"] ?? "System";
        var lastName = configuration["SeedData:Admin:LastName"] ?? "Admin";

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                IsActive = true
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to seed admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }

        var orgMembership = await context.OrganizationUsers
            .FirstOrDefaultAsync(ou => ou.OrganizationId == organizationId && ou.UserId == user.Id, cancellationToken);
        if (orgMembership is null)
        {
            context.OrganizationUsers.Add(new OrganizationUser
            {
                OrganizationId = organizationId,
                UserId = user.Id,
                Role = OrganizationRole.Owner,
                Status = OrganizationUserStatus.Active
            });
            await context.SaveChangesAsync(cancellationToken);
        }

        await EnsureWorkspaceMembershipAsync(context, workspaceId, user.Id, "Owner/Admin", EmploymentType.FullTime, cancellationToken);
        return user;
    }

    private static async Task SeedDemoStaffAsync(
        UserManager<ApplicationUser> userManager,
        AppDbContext context,
        int workspaceId,
        CancellationToken cancellationToken)
    {
        var staffSpecs = new[]
        {
            ("tech.maya@servora.local", "Maya", "Putri", "Worker123!", "Technician", EmploymentType.FullTime),
            ("tech.rafi@servora.local", "Rafi", "Pratama", "Freelance123!", "Technician", EmploymentType.Freelance),
            ("desk.nadia@servora.local", "Nadia", "Kusuma", "DemoStaff123!", "Front Desk", EmploymentType.FullTime)
        };

        foreach (var (email, firstName, lastName, password, roleName, empType) in staffSpecs)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = firstName,
                    LastName = lastName,
                    IsActive = true
                };
                var create = await userManager.CreateAsync(user, password);
                if (!create.Succeeded) continue;
            }

            await EnsureWorkspaceMembershipAsync(context, workspaceId, user.Id, roleName, empType, cancellationToken);
        }
    }

    private static async Task EnsureWorkspaceMembershipAsync(
        AppDbContext context,
        int workspaceId,
        int userId,
        string roleName,
        EmploymentType employmentType,
        CancellationToken cancellationToken)
    {
        var shopUser = await context.ShopUsers
            .FirstOrDefaultAsync(su => su.ShopId == workspaceId && su.UserId == userId, cancellationToken);

        if (shopUser is null)
        {
            shopUser = new ShopUser
            {
                ShopId = workspaceId,
                UserId = userId,
                Status = ShopUserStatus.Active,
                EmploymentType = employmentType
            };
            context.ShopUsers.Add(shopUser);
            await context.SaveChangesAsync(cancellationToken);
        }

        var role = await context.Roles.FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
        if (role is not null)
        {
            var hasRole = await context.ShopUserRoles
                .AnyAsync(sur => sur.ShopUserId == shopUser.Id && sur.RoleId == role.Id, cancellationToken);
            if (!hasRole)
            {
                context.ShopUserRoles.Add(new ShopUserRole
                {
                    ShopUserId = shopUser.Id,
                    RoleId = role.Id
                });
                await context.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private static async Task SeedBonusTypesAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            new BonusType
            {
                Code = BonusTypeCodes.MaintenanceCompletion,
                Name = "Maintenance Completion Bonus",
                Description = "Flat bonus awarded per completed service work order.",
                IsSystem = true,
                IsActive = true
            },
            new BonusType
            {
                Code = BonusTypeCodes.CustomerCancellation,
                Name = "Inspection / Cancellation Bonus",
                Description = "Diagnostic effort bonus when a customer cancels after inspection.",
                IsSystem = true,
                IsActive = true
            },
            new BonusType
            {
                Code = BonusTypeCodes.TaskItemized,
                Name = "Itemized Task Bonus",
                Description = "Bonus calculated per completed checklist or itemized task.",
                IsSystem = true,
                IsActive = true
            }
        };

        var existingCodes = await context.BonusTypes.Select(b => b.Code).ToHashSetAsync(cancellationToken);
        foreach (var seed in seeds)
        {
            if (!existingCodes.Contains(seed.Code))
            {
                context.BonusTypes.Add(seed);
            }
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedAssetCategoriesAsync(AppDbContext context, int workspaceId, CancellationToken cancellationToken)
    {
        var hasCategories = await context.AssetCategories.AnyAsync(c => c.ShopId == workspaceId, cancellationToken);
        if (hasCategories) return;

        context.AssetCategories.AddRange(
            new AssetCategory { ShopId = workspaceId, Name = "Diagnostic & Test Tools", Description = "Meters, testers, and diagnostic kits", DefaultUsefulLifeMonths = 36 },
            new AssetCategory { ShopId = workspaceId, Name = "Workshop Machinery", Description = "Lifts, compressors, and bench stations", DefaultUsefulLifeMonths = 60 },
            new AssetCategory { ShopId = workspaceId, Name = "IT & Counter Equipment", Description = "POS terminals, label printers, and workstations", DefaultUsefulLifeMonths = 36 },
            new AssetCategory { ShopId = workspaceId, Name = "Field Service Vehicles", Description = "Dispatch vans and service motorbikes", DefaultUsefulLifeMonths = 60 }
        );

        await context.SaveChangesAsync(cancellationToken);
    }

    private sealed record RoleSeed(string Name, string Description, IReadOnlyCollection<string> Permissions);
}
