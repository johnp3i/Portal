using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Identity;

namespace Portal.Web.Security;

/// <summary>
/// Custom claims principal factory that injects the BusinessId claim on sign-in.
/// Resolves from UserBusiness (new model) first, falls back to ApplicationUser.BusinessId (legacy).
/// </summary>
public class BusinessClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    private readonly MembershipDbContext _membershipDbContext;

    public BusinessClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options,
        MembershipDbContext membershipDbContext)
        : base(userManager, roleManager, options)
    {
        _membershipDbContext = membershipDbContext;
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        // Try new model first: resolve from UserBusiness
        var defaultBusiness = await _membershipDbContext.UserBusinesses
            .Where(ub => ub.UserId == user.Id && ub.IsDefault && ub.IsActive)
            .Select(ub => ub.BusinessId)
            .FirstOrDefaultAsync();

        if (defaultBusiness > 0)
        {
            identity.AddClaim(new Claim("BusinessId", defaultBusiness.ToString()));
        }
        else if (user.BusinessId.HasValue)
        {
            // Fallback: legacy single-business field (backward compatibility during transition)
            identity.AddClaim(new Claim("BusinessId", user.BusinessId.Value.ToString()));
        }

        return identity;
    }
}
