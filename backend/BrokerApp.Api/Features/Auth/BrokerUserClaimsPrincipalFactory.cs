using BrokerApp.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace BrokerApp.Api.Features.Auth;

public sealed class BrokerUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<AppUser, IdentityRole<Guid>>
{
    public BrokerUserClaimsPrincipalFactory(
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, roleManager, optionsAccessor)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim(BrokerClaims.OrganizationId, user.OrganizationId.ToString()));
        identity.AddClaim(new Claim(BrokerClaims.DisplayName, user.DisplayName));
        identity.AddClaim(new Claim(ClaimTypes.Role, user.Role));

        if (user.Organization is not null)
        {
            identity.AddClaim(new Claim(BrokerClaims.OrganizationName, user.Organization.Name));
        }

        return identity;
    }
}
