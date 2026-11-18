using Microsoft.AspNetCore.Identity;
using Servora.Api.Features.Identity.Permissions;

namespace Servora.Api.Features.Identity.Roles;

public class ApplicationRole : IdentityRole<int>
{
    public string? Description { get; set; }
    
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
