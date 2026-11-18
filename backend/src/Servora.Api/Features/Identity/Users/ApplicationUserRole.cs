using Microsoft.AspNetCore.Identity;

namespace Servora.Api.Features.Identity.Users;

public class ApplicationUserRole : IdentityUserRole<int>
{
    public int Id { get; set; }
}
