using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Batch-resolves UserIds to display names from MembershipDbContext.
/// Format: "{FirstName} {LastInitial}." — e.g., "John P."
/// Null → "System", not found → "Unknown User".
/// </summary>
public class UserNameResolver
{
    private readonly MembershipDbContext _membershipDbContext;
    private readonly ICurrentTenantService _tenantService;

    public UserNameResolver(MembershipDbContext membershipDbContext, ICurrentTenantService tenantService)
    {
        _membershipDbContext = membershipDbContext;
        _tenantService = tenantService;
    }

    /// <summary>
    /// Resolves a collection of UserIds to display names in a single database query.
    /// </summary>
    public async Task<Dictionary<string, string>> ResolveNamesAsync(IEnumerable<string?> userIds)
    {
        try
        {
            var result = new Dictionary<string, string>();
            var distinctIds = userIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();

            if (distinctIds.Count == 0)
                return result;

            var businessId = _tenantService.CurrentBusinessId;

            var users = await _membershipDbContext.UserBusinesses
                .Include(ub => ub.User)
                .Where(ub => ub.BusinessId == businessId && distinctIds.Contains(ub.UserId))
                .Select(ub => new { ub.UserId, ub.User.FirstName, ub.User.LastName })
                .ToListAsync();

            foreach (var user in users)
            {
                var displayName = !string.IsNullOrEmpty(user.FirstName) && !string.IsNullOrEmpty(user.LastName)
                    ? $"{user.FirstName} {user.LastName[0]}."
                    : user.FirstName ?? "User";
                result[user.UserId] = displayName;
            }

            // Add "Unknown User" for any IDs not found
            foreach (var id in distinctIds)
            {
                if (!result.ContainsKey(id!))
                    result[id!] = "Unknown User";
            }

            return result;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Resolves a single userId to a display name. Returns "System" for null.
    /// </summary>
    public string GetDisplayName(Dictionary<string, string> resolvedNames, string? userId)
    {
        if (string.IsNullOrEmpty(userId)) return "System";
        return resolvedNames.TryGetValue(userId, out var name) ? name : "Unknown User";
    }
}
