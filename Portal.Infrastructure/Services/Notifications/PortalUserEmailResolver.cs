using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;

namespace Portal.Infrastructure.Services.Notifications;

/// <summary>
/// Resolves a portal user's email by their identity UserId (Membership DB). Used by the Task &amp;
/// Meeting Reminder to reach a team member who has no own <c>TeamMember.Email</c> but is linked to a
/// portal user (<c>TeamMember.UserId</c>). Tenant-less; sibling to <see cref="IOwnerEmailResolver"/>.
/// </summary>
public interface IPortalUserEmailResolver
{
    /// <summary>Returns the portal user's email for the given identity UserId, or null.</summary>
    Task<string?> ResolveByUserIdAsync(string userId);
}

public class PortalUserEmailResolver : IPortalUserEmailResolver
{
    private readonly MembershipDbContext _membershipDbContext;

    public PortalUserEmailResolver(MembershipDbContext membershipDbContext)
    {
        _membershipDbContext = membershipDbContext;
    }

    public async Task<string?> ResolveByUserIdAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        // A linked team member's UserId corresponds to a portal user who has a UserBusiness row;
        // read the email via that navigation (no direct Users DbSet is exposed).
        return await _membershipDbContext.UserBusinesses
            .Include(ub => ub.User)
            .Where(ub => ub.UserId == userId)
            .Select(ub => ub.User.Email)
            .FirstOrDefaultAsync();
    }
}
