using Microsoft.AspNetCore.Identity;

namespace Servora.Api.Features.Identity.Users;

public class ApplicationUserToken : IdentityUserToken<int>
{
    public int Id { get; set; }
}
