using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Servora.Api.Features.Identity.Roles;
using Servora.Api.Infrastructure.Data;

namespace Servora.Api.Features.Identity.Users;

/// <summary>
/// Custom Identity UserStore that queries <see cref="ApplicationUserToken"/> and
/// <see cref="ApplicationUserLogin"/> by their unique business columns rather than EF FindAsync,
/// because Servora uses surrogate int auto-increment primary keys on all tables.
/// </summary>
public sealed class ServoraUserStore : UserStore<
    ApplicationUser,
    ApplicationRole,
    AppDbContext,
    int,
    ApplicationUserClaim,
    ApplicationUserRole,
    ApplicationUserLogin,
    ApplicationUserToken,
    ApplicationRoleClaim>
{
    public ServoraUserStore(AppDbContext context, IdentityErrorDescriber? describer = null)
        : base(context, describer)
    {
    }

    protected override Task<ApplicationUserToken?> FindTokenAsync(
        ApplicationUser user,
        string loginProvider,
        string name,
        CancellationToken cancellationToken)
        => Context.Set<ApplicationUserToken>()
            .FirstOrDefaultAsync(
                token => token.UserId == user.Id
                    && token.LoginProvider == loginProvider
                    && token.Name == name,
                cancellationToken);

    protected override Task<ApplicationUserLogin?> FindUserLoginAsync(
        int userId,
        string loginProvider,
        string providerKey,
        CancellationToken cancellationToken)
        => Context.Set<ApplicationUserLogin>()
            .FirstOrDefaultAsync(
                login => login.UserId == userId
                    && login.LoginProvider == loginProvider
                    && login.ProviderKey == providerKey,
                cancellationToken);

    protected override Task<ApplicationUserLogin?> FindUserLoginAsync(
        string loginProvider,
        string providerKey,
        CancellationToken cancellationToken)
        => Context.Set<ApplicationUserLogin>()
            .FirstOrDefaultAsync(
                login => login.LoginProvider == loginProvider
                    && login.ProviderKey == providerKey,
                cancellationToken);
}
