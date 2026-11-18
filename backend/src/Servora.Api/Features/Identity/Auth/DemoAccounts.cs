using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Identity.Users;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data;

namespace Servora.Api.Features.Identity.Auth;

public static class DemoAccounts
{
    public sealed record DemoAccountDto(
        int UserId,
        string Name,
        string Email,
        string Role,
        string EmploymentType,
        string DefaultPassword);

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/auth/demo-accounts", async (
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            CancellationToken cancellationToken) =>
        {
            var shop = await context.Shops
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderBy(s => s.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (shop is null)
            {
                return Results.Ok(Array.Empty<DemoAccountDto>());
            }

            var shopUsers = await context.ShopUsers
                .AsNoTracking()
                .Where(su => su.ShopId == shop.Id && su.Status == ShopUserStatus.Active && su.User.IsActive)
                .Include(su => su.User)
                .Include(su => su.Roles)
                    .ThenInclude(sur => sur.Role)
                .OrderBy(su => su.UserId)
                .ToListAsync(cancellationToken);

            var list = new List<DemoAccountDto>();
            foreach (var su in shopUsers)
            {
                var fullName = $"{su.User.FirstName} {su.User.LastName}".Trim();
                var displayName = string.IsNullOrWhiteSpace(fullName) ? (su.User.Email ?? "User") : fullName;
                var roles = su.Roles.Select(r => r.Role.Name ?? string.Empty).Where(n => !string.IsNullOrEmpty(n)).ToList();
                var roleLabel = roles.Count > 0 ? string.Join(", ", roles) : su.EmploymentType.ToString();

                var defaultPassword = "";
                if (su.User.Email?.Equals("admin@example.com", StringComparison.OrdinalIgnoreCase) == true)
                {
                    defaultPassword = "Admin123!";
                }
                else if (await userManager.CheckPasswordAsync(su.User, "DemoStaff123!"))
                {
                    defaultPassword = "DemoStaff123!";
                }
                else if (await userManager.CheckPasswordAsync(su.User, "Worker123!"))
                {
                    defaultPassword = "Worker123!";
                }
                else if (await userManager.CheckPasswordAsync(su.User, "Freelance123!"))
                {
                    defaultPassword = "Freelance123!";
                }

                list.Add(new DemoAccountDto(
                    su.UserId,
                    displayName,
                    su.User.Email ?? string.Empty,
                    roleLabel,
                    su.EmploymentType.ToString(),
                    defaultPassword));
            }

            return Results.Ok(list);
        })
        .WithName("GetDemoAccounts")
        .WithOpenApi()
        .AllowAnonymous();
    }
}

