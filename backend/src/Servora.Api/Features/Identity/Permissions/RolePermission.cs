using Servora.Api.Features.Identity.Roles;

namespace Servora.Api.Features.Identity.Permissions;

public class RolePermission
{
    public int Id { get; set; }
    
    public int RoleId { get; set; }
    public ApplicationRole Role { get; set; } = null!;

    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
