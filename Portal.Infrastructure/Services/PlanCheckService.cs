using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Provides plan-level and user-level permission checks for the subscription gating system.
/// Uses per-request caching via HttpContext.Items to avoid redundant queries within a single request.
/// </summary>
public class PlanCheckService : IPlanCheckService
{
    private const string PlanModulesCacheKey = "PlanCheckService_PlanModules";
    private const string PlanAccessLevelsCacheKey = "PlanCheckService_PlanAccessLevels";
    private const string UserPermissionsCacheKey = "PlanCheckService_UserPermissions";
    private const string IsOwnerCacheKeyPrefix = "PlanCheckService_IsOwner_";
    private const string HasActiveSubCacheKey = "PlanCheckService_HasActiveSub";

    private readonly ICurrentTenantService _currentTenantService;
    private readonly PortalDbContext _portalDbContext;
    private readonly MembershipDbContext _membershipDbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PlanCheckService(
        ICurrentTenantService currentTenantService,
        PortalDbContext portalDbContext,
        MembershipDbContext membershipDbContext,
        IHttpContextAccessor httpContextAccessor)
    {
        _currentTenantService = currentTenantService;
        _portalDbContext = portalDbContext;
        _membershipDbContext = membershipDbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public async Task<bool> IsModuleInPlanAsync(string module)
    {
        var modules = await GetPlanModulesAsync();
        return modules.Contains(module);
    }

    /// <inheritdoc />
    public async Task<string> GetEffectiveAccessLevelAsync(string userId, string module)
    {
        // Get the plan-level access for this module
        var planAccessLevels = await GetPlanAccessLevelsAsync();

        if (!planAccessLevels.TryGetValue(module, out var planLevel))
        {
            return AccessLevels.None;
        }

        // Get the user-level access for this module
        var userPermissions = await GetUserPermissionsAsync(userId);

        if (!userPermissions.TryGetValue(module, out var userLevel))
        {
            return AccessLevels.None;
        }

        // Return the more restrictive of the two
        return ResolveEffectiveAccessLevel(planLevel, userLevel);
    }

    /// <inheritdoc />
    public async Task<List<string>> GetPlanModulesAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Items.TryGetValue(PlanModulesCacheKey, out var cached) == true && cached is List<string> cachedModules)
        {
            return cachedModules;
        }

        var businessId = _currentTenantService.CurrentBusinessId;

        var modules = await _portalDbContext.BusinessPlans
            .Where(bp => bp.BusinessId == businessId && bp.IsActive)
            .SelectMany(bp => bp.Plan.PlanFeatures)
            .Where(pf => pf.IsIncluded)
            .Select(pf => pf.ModuleName)
            .ToListAsync();

        if (httpContext != null)
        {
            httpContext.Items[PlanModulesCacheKey] = modules;
        }

        return modules;
    }

    /// <inheritdoc />
    public async Task<string?> GetRequiredPlanForModuleAsync(string module)
    {
        // Query all plans ordered by DisplayOrder (ascending = cheapest first)
        // Find the first plan that includes the specified module
        var planName = await _portalDbContext.Plans
            .Where(p => p.IsActive)
            .OrderBy(p => p.DisplayOrder)
            .Where(p => p.PlanFeatures.Any(pf => pf.ModuleName == module && pf.IsIncluded))
            .Select(p => p.Name)
            .FirstOrDefaultAsync();

        return planName;
    }

    /// <inheritdoc />
    public async Task<bool> HasActiveSubscriptionAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Items.TryGetValue(HasActiveSubCacheKey, out var cached) == true && cached is bool cachedResult)
        {
            return cachedResult;
        }

        var businessId = _currentTenantService.CurrentBusinessId;
        var now = DateTime.UtcNow;

        var hasActive = await _portalDbContext.BusinessPlans
            .AnyAsync(bp => bp.BusinessId == businessId
                         && bp.IsActive
                         && (bp.Status == "active"
                             || (bp.Status == "trial" && bp.TrialEndsAtUtc != null && bp.TrialEndsAtUtc > now)));

        if (httpContext != null)
        {
            httpContext.Items[HasActiveSubCacheKey] = hasActive;
        }

        return hasActive;
    }

    /// <inheritdoc />
    public async Task<bool> IsOwnerAsync(string userId)
    {
        var cacheKey = IsOwnerCacheKeyPrefix + userId;
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Items.TryGetValue(cacheKey, out var cached) == true && cached is bool cachedResult)
        {
            return cachedResult;
        }

        var businessId = _currentTenantService.CurrentBusinessId;

        var isOwner = await _membershipDbContext.UserBusinesses
            .AnyAsync(ub => ub.UserId == userId
                         && ub.BusinessId == businessId
                         && ub.IsOwner
                         && ub.IsActive);

        if (httpContext != null)
        {
            httpContext.Items[cacheKey] = isOwner;
        }

        return isOwner;
    }

    /// <inheritdoc />
    public async Task<string?> GetCurrentPlanNameAsync()
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        var planName = await _portalDbContext.Subscriptions
            .Where(s => s.BusinessId == businessId
                && (s.Status == "active" || s.Status == "trialing" || s.Status == "past_due"))
            .Join(_portalDbContext.Plans,
                s => s.PlanId,
                p => p.Id,
                (s, p) => p.Name)
            .FirstOrDefaultAsync();

        return planName;
    }

    /// <summary>
    /// Retrieves the plan-level access levels for all included modules (cached per-request).
    /// </summary>
    private async Task<Dictionary<string, string>> GetPlanAccessLevelsAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Items.TryGetValue(PlanAccessLevelsCacheKey, out var cached) == true && cached is Dictionary<string, string> cachedLevels)
        {
            return cachedLevels;
        }

        var businessId = _currentTenantService.CurrentBusinessId;

        var accessLevels = await _portalDbContext.BusinessPlans
            .Where(bp => bp.BusinessId == businessId && bp.IsActive)
            .SelectMany(bp => bp.Plan.PlanFeatures)
            .Where(pf => pf.IsIncluded)
            .ToDictionaryAsync(pf => pf.ModuleName, pf => pf.AccessLevel);

        if (httpContext != null)
        {
            httpContext.Items[PlanAccessLevelsCacheKey] = accessLevels;
        }

        return accessLevels;
    }

    /// <summary>
    /// Retrieves the user-level permissions for all modules (cached per-request).
    /// </summary>
    private async Task<Dictionary<string, string>> GetUserPermissionsAsync(string userId)
    {
        var cacheKey = UserPermissionsCacheKey + "_" + userId;
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Items.TryGetValue(cacheKey, out var cached) == true && cached is Dictionary<string, string> cachedPermissions)
        {
            return cachedPermissions;
        }

        var businessId = _currentTenantService.CurrentBusinessId;

        var permissions = await _membershipDbContext.UserBusinessPermissions
            .Where(ubp => ubp.UserBusiness.UserId == userId
                       && ubp.UserBusiness.BusinessId == businessId
                       && ubp.UserBusiness.IsActive
                       && ubp.IsActive)
            .ToDictionaryAsync(ubp => ubp.Module, ubp => ubp.AccessLevel);

        if (httpContext != null)
        {
            httpContext.Items[cacheKey] = permissions;
        }

        return permissions;
    }

    /// <summary>
    /// Resolves the effective access level as the more restrictive of plan and user levels.
    /// Ordering: none &lt; readonly &lt; full
    /// </summary>
    internal static string ResolveEffectiveAccessLevel(string planLevel, string userLevel)
    {
        if (planLevel == AccessLevels.None || userLevel == AccessLevels.None)
            return AccessLevels.None;

        if (planLevel == AccessLevels.ReadOnly || userLevel == AccessLevels.ReadOnly)
            return AccessLevels.ReadOnly;

        return AccessLevels.Full;
    }
}
