using Microsoft.AspNetCore.Identity;
using Servora.Api.Features.Notifications;

namespace Servora.Api.Features.Identity.Users;

public class ApplicationUser : IdentityUser<int>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public TwoFactorMethod PreferredTwoFactorMethod { get; set; } = TwoFactorMethod.None;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
