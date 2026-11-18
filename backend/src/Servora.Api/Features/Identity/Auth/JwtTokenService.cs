using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Servora.Api.Features.Identity.Users;
using Servora.Api.Features.Shops;
using Servora.Api.Infrastructure.Data;
using Servora.Api.Infrastructure.ShopContext;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Servora.Api.Features.Identity.Auth;

public class JwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _context;

    public JwtTokenService(
        IConfiguration configuration,
        UserManager<ApplicationUser> userManager,
        AppDbContext context)
    {
        _configuration = configuration;
        _userManager = userManager;
        _context = context;
    }

    public async Task<TokenResult> CreateTokenAsync(ApplicationUser user, int? workspaceId = null)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var expiresAt = DateTimeOffset.UtcNow.AddHours(8);

        var membershipQuery = _context.ShopUsers
            .Where(shopUser => shopUser.UserId == user.Id
                && shopUser.Status == ShopUserStatus.Active
                && shopUser.Shop.IsActive);

        if (workspaceId.HasValue)
        {
            membershipQuery = membershipQuery.Where(shopUser => shopUser.ShopId == workspaceId.Value);
        }

        var membership = await membershipQuery
            .OrderBy(shopUser => shopUser.ShopId)
            .Select(shopUser => new { shopUser.Id, shopUser.ShopId })
            .FirstOrDefaultAsync();

        var roles = membership is null
            ? new List<string>()
            : await _context.ShopUserRoles
                .Where(userRole => userRole.ShopUserId == membership.Id)
                .Join(_context.Roles, userRole => userRole.RoleId, role => role.Id, (_, role) => role.Name!)
                .Where(roleName => roleName != null)
                .ToListAsync();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new("full_name", $"{user.FirstName} {user.LastName}".Trim())
        };

        if (membership is not null)
        {
            claims.Add(new Claim(ShopContext.WorkspaceClaimType, membership.ShopId.ToString()));
        }

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            jwtSettings["Key"] ?? "super-secret-key-that-should-be-very-long-in-production"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new TokenResult(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public sealed record TokenResult(string AccessToken, DateTimeOffset ExpiresAt);
}
