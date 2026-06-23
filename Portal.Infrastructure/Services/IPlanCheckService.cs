namespace Portal.Infrastructure.Services;

/// <summary>
/// Provides plan-level and user-level permission checks for the subscription gating system.
/// </summary>
public interface IPlanCheckService
{
    /// <summary>
    /// Returns true if the module is included in the current business's active subscription plan.
    /// </summary>
    Task<bool> IsModuleInPlanAsync(string module);

    /// <summary>
    /// Returns the effective access level for the current user and module.
    /// Combines plan-level and user-level permissions, returning the more restrictive.
    /// Returns "none" if module is not in plan or user has no permission.
    /// </summary>
    Task<string> GetEffectiveAccessLevelAsync(string userId, string module);

    /// <summary>
    /// Returns all modules included in the current business's subscription plan.
    /// </summary>
    Task<List<string>> GetPlanModulesAsync();

    /// <summary>
    /// Returns the plan name that includes the specified module (for soft-gate display).
    /// Returns the lowest tier plan that includes it.
    /// </summary>
    Task<string?> GetRequiredPlanForModuleAsync(string module);

    /// <summary>
    /// Returns true if the current business has an active (non-expired, non-cancelled) subscription.
    /// </summary>
    Task<bool> HasActiveSubscriptionAsync();

    /// <summary>
    /// Returns true if the current user is the business owner.
    /// </summary>
    Task<bool> IsOwnerAsync(string userId);
}
