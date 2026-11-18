using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Servora.Api.Infrastructure.Auth;

public class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : base(options) { }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var policy = await base.GetPolicyAsync(policyName);
        
        // If the policy doesn't exist but starts with "Permissions.", generate it dynamically
        if (policy == null && policyName.StartsWith("Permissions.", StringComparison.OrdinalIgnoreCase))
        {
            return new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();
        }

        return policy;
    }
}
