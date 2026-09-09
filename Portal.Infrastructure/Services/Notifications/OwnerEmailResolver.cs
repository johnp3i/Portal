using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;

namespace Portal.Infrastructure.Services.Notifications;

/// <summary>
/// Single source of truth for "the business owner's email." Resolves the active owner user's
/// email from the Membership DB (<c>UserBusiness.IsOwner &amp;&amp; IsActive</c>). Tenant-less
/// (explicit businessId) — safe for background services and the notification producer.
/// </summary>
public interface IOwnerEmailResolver
{
    /// <summary>Returns the owner's email for the business, or null when none is resolvable.</summary>
    Task<string?> ResolveAsync(int businessId);
}

public class OwnerEmailResolver : IOwnerEmailResolver
{
    private readonly MembershipDbContext _membershipDbContext;

    public OwnerEmailResolver(MembershipDbContext membershipDbContext)
    {
        _membershipDbContext = membershipDbContext;
    }

    public async Task<string?> ResolveAsync(int businessId)
    {
        return await _membershipDbContext.UserBusinesses
            .Include(ub => ub.User)
            .Where(ub => ub.BusinessId == businessId && ub.IsOwner && ub.IsActive)
            .Select(ub => ub.User.Email)
            .FirstOrDefaultAsync();
    }
}
