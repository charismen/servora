using Microsoft.AspNetCore.Identity;

namespace Servora.Api.Features.Identity.Users;

public class ApplicationUserLogin : IdentityUserLogin<int>
{
    public int Id { get; set; }
}
