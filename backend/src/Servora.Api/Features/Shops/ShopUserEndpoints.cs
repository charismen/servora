using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Servora.Api.Features.Identity.Permissions;
using Servora.Api.Features.Identity.Roles;
using Servora.Api.Features.Identity.Users;
using Servora.Api.Features.Payroll;
using Servora.Api.Infrastructure.Activity;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;

namespace Servora.Api.Features.Shops;

public static class ShopUserEndpoints
{
    private const string OwnerRoleName = "Owner/Admin";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public sealed record StaffUserDto(int Id, string FullName, string Email, IReadOnlyCollection<string> Roles, string EmploymentType = "FullTime");
    public sealed record RoleDto(int Id, string Name, string? Description, IReadOnlyCollection<string>? Permissions = null);
    public sealed record BonusTypeDto(int Id, string Code, string Name, string? Description, bool IsSystem);
    public sealed record WorkerBonusConfigDto(int BonusTypeId, string BonusTypeCode, string BonusTypeName, bool IsActive, JsonElement Parameters);
    public sealed record FreelanceProfileDto(
        int Id,
        int ShopUserId,
        string BasePayMode,
        decimal BaseHourlyRate,
        IReadOnlyCollection<WageTierBracket> TieredRates,
        IReadOnlyCollection<WorkerBonusConfigDto> BonusConfigs);
    public sealed record ShopUserDto(
        int Id,
        int UserId,
        string FullName,
        string Email,
        string Status,
        IReadOnlyCollection<RoleDto> Roles,
        string EmploymentType = "FullTime",
        FreelanceProfileDto? FreelanceProfile = null);
    public sealed record ShopUserPageDto(IReadOnlyCollection<ShopUserDto> Items, int Page, int PageSize, int TotalItems, int TotalPages);
    public sealed record PermissionDto(int Id, string Code, string? Description);
    public sealed record CreateShopUserRequest(string Email, string Password, string FirstName, string LastName, IReadOnlyCollection<int> RoleIds, string? EmploymentType = null);
    public sealed record UpdateShopUserRequest(string FirstName, string LastName, string? Email = null, string? Password = null, string? EmploymentType = null);
    public sealed record UpdateEmploymentTypeRequest(string EmploymentType);
    public sealed record UpdateUserRolesRequest(IReadOnlyCollection<int> RoleIds);
    public sealed record UpsertRoleRequest(string Name, string? Description, IReadOnlyCollection<string> Permissions);
    public sealed record UpsertWorkerBonusConfigItem(string BonusTypeCode, bool IsActive, JsonElement Parameters);
    public sealed record UpsertFreelanceProfileRequest(
        string BasePayMode,
        decimal BaseHourlyRate,
        IReadOnlyCollection<WageTierBracket> TieredRates,
        IReadOnlyCollection<UpsertWorkerBonusConfigItem> BonusConfigs);

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/shop-users/staff", ListStaffAsync)
            .WithName("GetShopStaff")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.ServiceTicketsRead);

        app.MapGet("/shop-users", ListShopUsersAsync)
            .WithName("GetShopUsers")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsRead);

        app.MapGet("/shop-users/roles", ListRolesAsync)
            .WithName("GetShopUserRoles")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsRead);

        app.MapGet("/shop-users/permissions", ListPermissionsAsync)
            .WithName("GetShopUserPermissions")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsRead);

        app.MapGet("/shop-users/bonus-types", ListBonusTypesAsync)
            .WithName("GetShopUserBonusTypes")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsRead);

        app.MapPost("/shop-users", CreateShopUserAsync)
            .WithName("CreateShopUser")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPut("/shop-users/{userId:int}", UpdateShopUserAsync)
            .WithName("UpdateShopUser")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPut("/shop-users/{userId:int}/employment-type", UpdateEmploymentTypeAsync)
            .WithName("UpdateShopUserEmploymentType")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapGet("/shop-users/{userId:int}/freelance-profile", GetFreelanceProfileAsync)
            .WithName("GetShopUserFreelanceProfile")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsRead);

        app.MapPut("/shop-users/{userId:int}/freelance-profile", UpsertFreelanceProfileAsync)
            .WithName("UpsertShopUserFreelanceProfile")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPut("/shop-users/{userId:int}/roles", UpdateUserRolesAsync)
            .WithName("UpdateShopUserRoles")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPost("/shop-users/{userId:int}/deactivate", SetShopUserStatusAsync)
            .WithName("DeactivateShopUser")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPost("/shop-users/{userId:int}/reactivate", SetShopUserStatusAsync)
            .WithName("ReactivateShopUser")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPost("/shop-users/roles", CreateRoleAsync)
            .WithName("CreateShopRole")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapPut("/shop-users/roles/{roleId:int}", UpdateRoleAsync)
            .WithName("UpdateShopRole")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);

        app.MapDelete("/shop-users/roles/{roleId:int}", DeleteRoleAsync)
            .WithName("DeleteShopRole")
            .WithOpenApi()
            .RequireAuthorization(ApplicationPermissions.SettingsWrite);
    }

    private static async Task<IResult> ListStaffAsync(AppDbContext context, IShopContext shopContext, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var staff = await context.ShopUsers
            .AsNoTracking()
            .Where(shopUser => shopUser.ShopId == shopId && shopUser.Status == ShopUserStatus.Active && shopUser.User.IsActive)
            .OrderBy(shopUser => shopUser.User.FirstName)
            .ThenBy(shopUser => shopUser.User.LastName)
            .Select(shopUser => new
            {
                shopUser.UserId,
                shopUser.User.FirstName,
                shopUser.User.LastName,
                Email = shopUser.User.Email ?? string.Empty,
                shopUser.EmploymentType,
                Roles = shopUser.Roles
                    .OrderBy(role => role.Role.Name)
                    .Select(role => role.Role.Name ?? string.Empty)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(staff.Select(user =>
        {
            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            return new StaffUserDto(
                user.UserId,
                string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName,
                user.Email,
                user.Roles,
                user.EmploymentType.ToString());
        }));
    }

    private static async Task<IResult> ListShopUsersAsync(AppDbContext context, IShopContext shopContext, int? page, int? pageSize, string? search, string? sort, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var query = context.ShopUsers
            .AsNoTracking()
            .Where(shopUser => shopUser.ShopId == shopId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var matchesActive = nameof(ShopUserStatus.Active).Contains(term, StringComparison.OrdinalIgnoreCase);
            var matchesInactive = nameof(ShopUserStatus.Inactive).Contains(term, StringComparison.OrdinalIgnoreCase);
            var matchesFreelance = nameof(EmploymentType.Freelance).Contains(term, StringComparison.OrdinalIgnoreCase);

            query = query.Where(shopUser =>
                shopUser.User.FirstName.Contains(term) ||
                shopUser.User.LastName.Contains(term) ||
                (shopUser.User.Email != null && shopUser.User.Email.Contains(term)) ||
                (matchesActive && shopUser.Status == ShopUserStatus.Active) ||
                (matchesInactive && shopUser.Status == ShopUserStatus.Inactive) ||
                (matchesFreelance && shopUser.EmploymentType == EmploymentType.Freelance));
        }

        var shouldPage = page.HasValue || pageSize.HasValue || !string.IsNullOrWhiteSpace(search) || !string.IsNullOrWhiteSpace(sort);
        var currentPage = Math.Max(1, page ?? 1);
        var currentPageSize = Math.Clamp(pageSize ?? 10, 1, 100);
        var totalItems = shouldPage ? await query.CountAsync(cancellationToken) : 0;

        query = sort?.Trim().ToLowerInvariant() switch
        {
            "name-desc" => query.OrderByDescending(shopUser => shopUser.User.FirstName).ThenByDescending(shopUser => shopUser.User.LastName),
            "email-asc" => query.OrderBy(shopUser => shopUser.User.Email).ThenBy(shopUser => shopUser.User.FirstName),
            "status-asc" => query.OrderBy(shopUser => shopUser.Status).ThenBy(shopUser => shopUser.User.FirstName),
            _ => query.OrderBy(shopUser => shopUser.User.FirstName).ThenBy(shopUser => shopUser.User.LastName)
        };

        if (shouldPage)
        {
            query = query
                .Skip((currentPage - 1) * currentPageSize)
                .Take(currentPageSize);
        }

        var users = await query
            .Include(shopUser => shopUser.User)
            .Include(shopUser => shopUser.Roles)
                .ThenInclude(role => role.Role)
            .Include(shopUser => shopUser.FreelanceProfile)
                .ThenInclude(profile => profile!.BonusConfigs)
                    .ThenInclude(config => config.BonusType)
            .ToListAsync(cancellationToken);

        var items = users.Select(user =>
        {
            var fullName = $"{user.User.FirstName} {user.User.LastName}".Trim();
            var email = user.User.Email ?? string.Empty;
            var roles = user.Roles
                .OrderBy(role => role.Role.Name)
                .Select(role => new RoleDto(role.RoleId, role.Role.Name ?? string.Empty, role.Role.Description, null))
                .ToList();
            var profileDto = user.FreelanceProfile is null ? null : MapFreelanceProfileDto(user.FreelanceProfile);

            return new ShopUserDto(
                user.Id,
                user.UserId,
                string.IsNullOrWhiteSpace(fullName) ? email : fullName,
                email,
                user.Status.ToString(),
                roles,
                user.EmploymentType.ToString(),
                profileDto);
        }).ToList();

        if (!shouldPage) return Results.Ok(items);

        return Results.Ok(new ShopUserPageDto(items, currentPage, currentPageSize, totalItems, (int)Math.Ceiling(totalItems / (double)currentPageSize)));
    }

    private static async Task<IResult> ListRolesAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var roles = await context.Roles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .Select(role => new RoleDto(
                role.Id,
                role.Name ?? string.Empty,
                role.Description,
                role.RolePermissions.OrderBy(permission => permission.Permission.Code).Select(permission => permission.Permission.Code).ToList()))
            .ToListAsync(cancellationToken);

        return Results.Ok(roles);
    }

    private static async Task<IResult> ListPermissionsAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        return Results.Ok(await context.Permissions
            .AsNoTracking()
            .OrderBy(permission => permission.Code)
            .Select(permission => new PermissionDto(permission.Id, permission.Code, permission.Description))
            .ToListAsync(cancellationToken));
    }

    private static async Task<IResult> ListBonusTypesAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var bonusTypes = await context.BonusTypes
            .AsNoTracking()
            .Where(type => type.IsActive)
            .OrderBy(type => type.Id)
            .Select(type => new BonusTypeDto(type.Id, type.Code, type.Name, type.Description, type.IsSystem))
            .ToListAsync(cancellationToken);

        return Results.Ok(bonusTypes);
    }

    private static async Task<IResult> CreateShopUserAsync(
        CreateShopUserRequest request,
        UserManager<ApplicationUser> userManager,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email)) return Results.BadRequest("Email is required.");
        if (string.IsNullOrWhiteSpace(request.Password)) return Results.BadRequest("Password is required.");

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var roleIds = request.RoleIds.Distinct().ToArray();
        if (!await RolesExistAsync(context, roleIds, cancellationToken)) return Results.BadRequest("One or more roles were not found.");
        var employmentType = ParseEmploymentType(request.EmploymentType);

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = request.Email.Trim(),
                Email = request.Email.Trim(),
                EmailConfirmed = true,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded) return Results.BadRequest(string.Join(" ", result.Errors.Select(error => error.Description)));
        }

        var shopUser = await context.ShopUsers
            .Include(item => item.FreelanceProfile)
            .FirstOrDefaultAsync(item => item.ShopId == shopId && item.UserId == user.Id, cancellationToken);
        if (shopUser is null)
        {
            shopUser = new ShopUser
            {
                ShopId = shopId,
                UserId = user.Id,
                Status = ShopUserStatus.Active,
                EmploymentType = employmentType
            };
            context.ShopUsers.Add(shopUser);
            await context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            shopUser.EmploymentType = employmentType;
            await context.SaveChangesAsync(cancellationToken);
        }

        if (employmentType == EmploymentType.Freelance && shopUser.FreelanceProfile is null)
        {
            await EnsureDefaultFreelanceProfileAsync(context, shopId, shopUser.Id, cancellationToken);
        }

        await ReplaceUserRolesAsync(context, shopUser, roleIds, cancellationToken);
        await activityLogger.LogAsync("ShopUser", user.Id, "ShopUserCreated", $"Shop user saved: {user.Email} ({employmentType})", cancellationToken: cancellationToken);
        return Results.Created($"/api/v1/shop-users/{user.Id}", new { user.Id });
    }

    private static async Task<IResult> UpdateShopUserAsync(
        int userId,
        UpdateShopUserRequest request,
        UserManager<ApplicationUser> userManager,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var shopUser = await context.ShopUsers
            .Include(item => item.User)
            .Include(item => item.FreelanceProfile)
            .FirstOrDefaultAsync(item => item.ShopId == shopId && item.UserId == userId, cancellationToken);
        if (shopUser is null) return Results.NotFound();

        var user = shopUser.User;

        // 1. Update Name
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        // 2. Update Email if provided and changed
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var newEmail = request.Email.Trim();
            if (!string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
            {
                var existingWithEmail = await userManager.FindByEmailAsync(newEmail);
                if (existingWithEmail is not null && existingWithEmail.Id != user.Id)
                {
                    return Results.BadRequest("A user with this email address already exists.");
                }

                user.Email = newEmail;
                user.UserName = newEmail;
                user.NormalizedEmail = userManager.NormalizeEmail(newEmail);
                user.NormalizedUserName = userManager.NormalizeName(newEmail);
                user.EmailConfirmed = true;
            }
        }

        // 3. Update Temporary Password if provided
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            foreach (var validator in userManager.PasswordValidators)
            {
                var validationResult = await validator.ValidateAsync(userManager, user, request.Password);
                if (!validationResult.Succeeded)
                {
                    return Results.BadRequest(string.Join(" ", validationResult.Errors.Select(e => e.Description)));
                }
            }
            user.PasswordHash = userManager.PasswordHasher.HashPassword(user, request.Password);
            user.SecurityStamp = Guid.NewGuid().ToString();
        }

        // 4. Save User changes to database (Name, Email, PasswordHash, SecurityStamp, UpdatedAt)
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return Results.BadRequest(string.Join(" ", updateResult.Errors.Select(error => error.Description)));
        }

        // 5. Update Employment Type if provided
        if (!string.IsNullOrWhiteSpace(request.EmploymentType))
        {
            var employmentType = ParseEmploymentType(request.EmploymentType);
            shopUser.EmploymentType = employmentType;
            await context.SaveChangesAsync(cancellationToken);
            if (employmentType == EmploymentType.Freelance && shopUser.FreelanceProfile is null)
            {
                await EnsureDefaultFreelanceProfileAsync(context, shopId, shopUser.Id, cancellationToken);
            }
        }

        await activityLogger.LogAsync("ShopUser", user.Id, "ShopUserUpdated", $"Shop user profile updated: {user.Email}", cancellationToken: cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> UpdateEmploymentTypeAsync(
        int userId,
        UpdateEmploymentTypeRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var shopUser = await context.ShopUsers
            .Include(item => item.FreelanceProfile)
            .FirstOrDefaultAsync(item => item.ShopId == shopId && item.UserId == userId, cancellationToken);
        if (shopUser is null) return Results.NotFound();

        var employmentType = ParseEmploymentType(request.EmploymentType);
        shopUser.EmploymentType = employmentType;
        await context.SaveChangesAsync(cancellationToken);

        if (employmentType == EmploymentType.Freelance && shopUser.FreelanceProfile is null)
        {
            await EnsureDefaultFreelanceProfileAsync(context, shopId, shopUser.Id, cancellationToken);
        }

        await activityLogger.LogAsync(
            "ShopUser",
            userId,
            "ShopUserEmploymentTypeUpdated",
            $"Employment type updated to {employmentType}.",
            cancellationToken: cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetFreelanceProfileAsync(
        int userId,
        AppDbContext context,
        IShopContext shopContext,
        CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var shopUser = await context.ShopUsers
            .AsNoTracking()
            .Include(item => item.FreelanceProfile)
                .ThenInclude(profile => profile!.BonusConfigs)
                    .ThenInclude(config => config.BonusType)
            .FirstOrDefaultAsync(item => item.ShopId == shopId && item.UserId == userId, cancellationToken);

        if (shopUser is null) return Results.NotFound();

        if (shopUser.FreelanceProfile is null)
        {
            var created = await EnsureDefaultFreelanceProfileAsync(context, shopId, shopUser.Id, cancellationToken);
            return Results.Ok(MapFreelanceProfileDto(created));
        }

        return Results.Ok(MapFreelanceProfileDto(shopUser.FreelanceProfile));
    }

    private static async Task<IResult> UpsertFreelanceProfileAsync(
        int userId,
        UpsertFreelanceProfileRequest request,
        AppDbContext context,
        IShopContext shopContext,
        IActivityLogger activityLogger,
        CancellationToken cancellationToken)
    {
        if (request.BaseHourlyRate < 0)
        {
            return Results.BadRequest("Base hourly rate cannot be negative.");
        }

        var brackets = (request.TieredRates ?? [])
            .Where(bracket => bracket.MaxHours >= bracket.MinHours && bracket.MinHours >= 0)
            .OrderBy(bracket => bracket.MinHours)
            .ToList();

        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var shopUser = await context.ShopUsers
            .Include(item => item.FreelanceProfile)
                .ThenInclude(profile => profile!.BonusConfigs)
            .FirstOrDefaultAsync(item => item.ShopId == shopId && item.UserId == userId, cancellationToken);
        if (shopUser is null) return Results.NotFound();

        var basePayMode = Enum.TryParse<BasePayMode>(request.BasePayMode, true, out var parsedMode)
            ? parsedMode
            : BasePayMode.TieredDaily;

        var profile = shopUser.FreelanceProfile;
        if (profile is null)
        {
            profile = new FreelanceProfile
            {
                ShopId = shopId,
                ShopUserId = shopUser.Id,
                BasePayMode = basePayMode,
                BaseHourlyRate = request.BaseHourlyRate,
                TieredRatesJson = JsonSerializer.Serialize(brackets, JsonOptions)
            };
            context.FreelanceProfiles.Add(profile);
            await context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            profile.BasePayMode = basePayMode;
            profile.BaseHourlyRate = request.BaseHourlyRate;
            profile.TieredRatesJson = JsonSerializer.Serialize(brackets, JsonOptions);
        }

        var bonusTypesByCode = await context.BonusTypes
            .ToDictionaryAsync(item => item.Code, item => item, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var item in request.BonusConfigs ?? [])
        {
            if (!bonusTypesByCode.TryGetValue(item.BonusTypeCode, out var bonusType))
            {
                continue;
            }

            var rawJson = item.Parameters.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                ? "{}"
                : item.Parameters.GetRawText();

            var existingConfig = profile.BonusConfigs.FirstOrDefault(cfg => cfg.BonusTypeId == bonusType.Id);
            if (existingConfig is null)
            {
                context.WorkerBonusConfigs.Add(new WorkerBonusConfig
                {
                    ShopId = shopId,
                    FreelanceProfileId = profile.Id,
                    BonusTypeId = bonusType.Id,
                    ParametersJson = rawJson,
                    IsActive = item.IsActive
                });
            }
            else
            {
                existingConfig.ParametersJson = rawJson;
                existingConfig.IsActive = item.IsActive;
            }
        }

        shopUser.EmploymentType = EmploymentType.Freelance;
        await context.SaveChangesAsync(cancellationToken);

        await activityLogger.LogAsync(
            "FreelanceProfile",
            userId,
            "FreelanceProfileUpdated",
            $"Updated freelance compensation profile for user #{userId}.",
            cancellationToken: cancellationToken);

        var reloaded = await context.FreelanceProfiles
            .AsNoTracking()
            .Include(item => item.BonusConfigs)
                .ThenInclude(cfg => cfg.BonusType)
            .FirstAsync(item => item.Id == profile.Id, cancellationToken);

        return Results.Ok(MapFreelanceProfileDto(reloaded));
    }

    private static async Task<IResult> UpdateUserRolesAsync(int userId, UpdateUserRolesRequest request, AppDbContext context, IShopContext shopContext, IActivityLogger activityLogger, CancellationToken cancellationToken)
    {
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var shopUser = await context.ShopUsers
            .Include(user => user.User)
            .Include(user => user.Roles)
            .ThenInclude(role => role.Role)
            .FirstOrDefaultAsync(user => user.ShopId == shopId && user.UserId == userId, cancellationToken);
        if (shopUser is null) return Results.NotFound();

        var roleIds = request.RoleIds.Distinct().ToArray();
        if (roleIds.Length == 0) return Results.BadRequest("At least one role is required.");
        if (await WouldRemoveLastOwnerAsync(context, shopId, shopUser, roleIds, cancellationToken)) return Results.BadRequest("At least one active Owner/Admin is required.");

        if (!await RolesExistAsync(context, roleIds, cancellationToken)) return Results.BadRequest("One or more roles were not found.");
        await ReplaceUserRolesAsync(context, shopUser, roleIds, cancellationToken);
        await activityLogger.LogAsync("ShopUser", userId, "ShopUserRolesUpdated", "Shop user roles updated.", cancellationToken: cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> SetShopUserStatusAsync(int userId, HttpContext httpContext, AppDbContext context, IShopContext shopContext, IActivityLogger activityLogger, CancellationToken cancellationToken)
    {
        var active = httpContext.Request.Path.Value?.EndsWith("/reactivate", StringComparison.OrdinalIgnoreCase) == true;
        var shopId = await shopContext.GetCurrentShopIdAsync(cancellationToken);
        var shopUser = await context.ShopUsers
            .Include(user => user.User)
            .Include(user => user.Roles)
            .ThenInclude(role => role.Role)
            .FirstOrDefaultAsync(user => user.ShopId == shopId && user.UserId == userId, cancellationToken);
        if (shopUser is null) return Results.NotFound();
        if (!active && await WouldRemoveLastOwnerAsync(context, shopId, shopUser, [], cancellationToken)) return Results.BadRequest("At least one active Owner/Admin is required.");

        shopUser.Status = active ? ShopUserStatus.Active : ShopUserStatus.Inactive;
        shopUser.User.IsActive = active;
        await context.SaveChangesAsync(cancellationToken);
        await activityLogger.LogAsync("ShopUser", userId, active ? "ShopUserReactivated" : "ShopUserDeactivated", active ? "Shop user reactivated." : "Shop user deactivated.", cancellationToken: cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> CreateRoleAsync(UpsertRoleRequest request, RoleManager<ApplicationRole> roleManager, AppDbContext context, IActivityLogger activityLogger, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest("Role name is required.");
        var role = new ApplicationRole { Name = request.Name.Trim(), Description = request.Description?.Trim() };
        var result = await roleManager.CreateAsync(role);
        if (!result.Succeeded) return Results.BadRequest(string.Join(" ", result.Errors.Select(error => error.Description)));
        await ReplaceRolePermissionsAsync(context, role.Id, request.Permissions, cancellationToken);
        await activityLogger.LogAsync("Role", role.Id, "RoleCreated", $"Role created: {role.Name}", cancellationToken: cancellationToken);
        return Results.Created($"/api/v1/shop-users/roles/{role.Id}", new { role.Id });
    }

    private static async Task<IResult> UpdateRoleAsync(int roleId, UpsertRoleRequest request, RoleManager<ApplicationRole> roleManager, AppDbContext context, IActivityLogger activityLogger, CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(roleId.ToString());
        if (role is null) return Results.NotFound();
        if (role.Name == OwnerRoleName && request.Name.Trim() != OwnerRoleName) return Results.BadRequest("Owner/Admin role cannot be renamed.");
        if (role.Name == OwnerRoleName && !request.Permissions.Contains(ApplicationPermissions.SettingsWrite)) return Results.BadRequest("Owner/Admin must keep settings write permission.");

        role.Name = request.Name.Trim();
        role.Description = request.Description?.Trim();
        var result = await roleManager.UpdateAsync(role);
        if (!result.Succeeded) return Results.BadRequest(string.Join(" ", result.Errors.Select(error => error.Description)));
        await ReplaceRolePermissionsAsync(context, role.Id, request.Permissions, cancellationToken);
        await activityLogger.LogAsync("Role", role.Id, "RoleUpdated", $"Role updated: {role.Name}", cancellationToken: cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteRoleAsync(int roleId, RoleManager<ApplicationRole> roleManager, AppDbContext context, IActivityLogger activityLogger, CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(roleId.ToString());
        if (role is null) return Results.NotFound();
        if (role.Name == OwnerRoleName) return Results.BadRequest("Owner/Admin role cannot be deleted.");
        if (await context.ShopUserRoles.AnyAsync(item => item.RoleId == roleId, cancellationToken)) return Results.BadRequest("Role is assigned to users.");

        context.RolePermissions.RemoveRange(context.RolePermissions.Where(item => item.RoleId == roleId));
        var result = await roleManager.DeleteAsync(role);
        if (!result.Succeeded) return Results.BadRequest(string.Join(" ", result.Errors.Select(error => error.Description)));
        await activityLogger.LogAsync("Role", roleId, "RoleDeleted", $"Role deleted: {role.Name}", cancellationToken: cancellationToken);
        return Results.NoContent();
    }

    private static async Task ReplaceUserRolesAsync(AppDbContext context, ShopUser shopUser, IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken)
    {
        context.ShopUserRoles.RemoveRange(shopUser.Roles.Where(role => !roleIds.Contains(role.RoleId)));
        foreach (var roleId in roleIds.Except(shopUser.Roles.Select(role => role.RoleId)))
        {
            context.ShopUserRoles.Add(new ShopUserRole { ShopUserId = shopUser.Id, RoleId = roleId });
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task ReplaceRolePermissionsAsync(AppDbContext context, int roleId, IReadOnlyCollection<string> permissionCodes, CancellationToken cancellationToken)
    {
        var codes = permissionCodes.Distinct().ToArray();
        var permissions = await context.Permissions.Where(permission => codes.Contains(permission.Code)).ToListAsync(cancellationToken);
        if (permissions.Count != codes.Length) throw new InvalidOperationException("One or more permissions were not found.");

        var current = await context.RolePermissions.Where(permission => permission.RoleId == roleId).ToListAsync(cancellationToken);
        context.RolePermissions.RemoveRange(current.Where(permission => !permissions.Any(item => item.Id == permission.PermissionId)));
        foreach (var permission in permissions.Where(permission => current.All(item => item.PermissionId != permission.Id)))
        {
            context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permission.Id });
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<bool> RolesExistAsync(AppDbContext context, IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken)
        => roleIds.Count > 0 && await context.Roles.CountAsync(role => roleIds.Contains(role.Id), cancellationToken) == roleIds.Count;

    private static async Task<bool> WouldRemoveLastOwnerAsync(AppDbContext context, int shopId, ShopUser editedUser, IReadOnlyCollection<int> nextRoleIds, CancellationToken cancellationToken)
    {
        var ownerRoleId = await context.Roles.Where(role => role.Name == OwnerRoleName).Select(role => (int?)role.Id).FirstOrDefaultAsync(cancellationToken);
        if (ownerRoleId is null || !editedUser.Roles.Any(role => role.RoleId == ownerRoleId)) return false;
        if (nextRoleIds.Contains(ownerRoleId.Value) && editedUser.Status == ShopUserStatus.Active) return false;

        return !await context.ShopUserRoles.AnyAsync(role =>
            role.RoleId == ownerRoleId &&
            role.ShopUser.ShopId == shopId &&
            role.ShopUser.Id != editedUser.Id &&
            role.ShopUser.Status == ShopUserStatus.Active &&
            role.ShopUser.User.IsActive,
            cancellationToken);
    }

    private static EmploymentType ParseEmploymentType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return EmploymentType.FullTime;
        var normalized = value.Trim().Replace("_", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);
        return Enum.TryParse<EmploymentType>(normalized, true, out var parsed)
            ? parsed
            : EmploymentType.FullTime;
    }

    private static async Task<FreelanceProfile> EnsureDefaultFreelanceProfileAsync(
        AppDbContext context,
        int shopId,
        int shopUserId,
        CancellationToken cancellationToken)
    {
        var existing = await context.FreelanceProfiles
            .Include(item => item.BonusConfigs)
                .ThenInclude(cfg => cfg.BonusType)
            .FirstOrDefaultAsync(item => item.ShopId == shopId && item.ShopUserId == shopUserId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var profile = new FreelanceProfile
        {
            ShopId = shopId,
            ShopUserId = shopUserId,
            BasePayMode = BasePayMode.TieredDaily,
            BaseHourlyRate = 7500m,
            TieredRatesJson = FreelanceProfile.DefaultTieredRatesJson
        };
        context.FreelanceProfiles.Add(profile);
        await context.SaveChangesAsync(cancellationToken);

        var bonusTypes = await context.BonusTypes.Where(type => type.IsActive).ToListAsync(cancellationToken);
        foreach (var bonusType in bonusTypes)
        {
            var defaultParams = bonusType.Code switch
            {
                BonusTypeCodes.MaintenanceCompletion => """{"threshold":4,"rewardPerUnit":15000,"flatReward":0,"countAllUnitsOnceThresholdMet":false,"isRetroactive":true,"milestones":[{"minUnits":1,"maxUnits":3,"rewardPerUnit":150000},{"minUnits":4,"maxUnits":9999,"rewardPerUnit":250000}]}""",
                BonusTypeCodes.CustomerCancellation => """{"feePerIncident":25000,"requireDiagnosisOrInspectionHistory":true}""",
                BonusTypeCodes.TaskItemized => """{"rules":[{"taskKey":"OS_INSTALL","taskLabel":"OS / Software Reinstallation","matcherKeyword":"OS,Install OS,Clean Install,OS Reinstall,Reinstall OS,Windows","deviceCategory":"Any","rewardPerUnit":25000},{"taskKey":"KEYBOARD_BATTERY","taskLabel":"Keyboard / Battery Replacement","matcherKeyword":"Keyboard,Battery Replacement,Replace Battery,New Battery,Swap Battery,Ganti Baterai,Tukar Baterai,Pasang Baterai","deviceCategory":"Any","rewardPerUnit":25000},{"taskKey":"CLEANING_REPASTE_STD","taskLabel":"Cleaning & Thermal Repasting (Standard)","matcherKeyword":"Repaste,Repasting,Cleaning,Clean Dust,Thermal,Pasta","deviceCategory":"Standard","rewardPerUnit":25000},{"taskKey":"CLEANING_REPASTE_GAMING","taskLabel":"Cleaning & Thermal Repasting (Gaming)","matcherKeyword":"Repaste,Repasting,Cleaning,Clean Dust,Thermal,Pasta","deviceCategory":"Gaming","rewardPerUnit":50000}]}""",
                _ => "{}"
            };

            context.WorkerBonusConfigs.Add(new WorkerBonusConfig
            {
                ShopId = shopId,
                FreelanceProfileId = profile.Id,
                BonusTypeId = bonusType.Id,
                ParametersJson = defaultParams,
                IsActive = true
            });
        }

        await context.SaveChangesAsync(cancellationToken);

        return await context.FreelanceProfiles
            .AsNoTracking()
            .Include(item => item.BonusConfigs)
                .ThenInclude(cfg => cfg.BonusType)
            .FirstAsync(item => item.Id == profile.Id, cancellationToken);
    }

    private static FreelanceProfileDto MapFreelanceProfileDto(FreelanceProfile profile)
    {
        var brackets = TieredWageCalculator.ParseBrackets(profile.TieredRatesJson);
        var configs = (profile.BonusConfigs ?? [])
            .OrderBy(cfg => cfg.BonusTypeId)
            .Select(cfg =>
            {
                JsonElement element;
                try
                {
                    using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(cfg.ParametersJson) ? "{}" : cfg.ParametersJson);
                    element = doc.RootElement.Clone();
                }
                catch
                {
                    using var doc = JsonDocument.Parse("{}");
                    element = doc.RootElement.Clone();
                }

                return new WorkerBonusConfigDto(
                    cfg.BonusTypeId,
                    cfg.BonusType?.Code ?? string.Empty,
                    cfg.BonusType?.Name ?? string.Empty,
                    cfg.IsActive,
                    element);
            })
            .ToList();

        return new FreelanceProfileDto(
            profile.Id,
            profile.ShopUserId,
            profile.BasePayMode.ToString(),
            profile.BaseHourlyRate,
            brackets,
            configs);
    }
}
