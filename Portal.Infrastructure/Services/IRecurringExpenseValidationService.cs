using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for recurring expense rule management and validation against purchase history.
/// </summary>
public interface IRecurringExpenseValidationService
{
    /// <summary>
    /// Validates all active rules for the business against the given date range.
    /// </summary>
    Task<RecurringExpenseValidationResult> ValidateAsync(int businessId, DateOnly startDate, DateOnly endDate);

    /// <summary>
    /// Returns all rules for the business (active and inactive, non-deleted) for the management UI.
    /// </summary>
    Task<List<RecurringRuleViewModel>> GetRulesForBusinessAsync(int businessId);

    /// <summary>
    /// Creates or updates a recurring rule. Returns ServiceResult with success/fail.
    /// </summary>
    Task<ServiceResult> SaveRuleAsync(int businessId, SaveRecurringRuleRequest request);

    /// <summary>
    /// Soft-deletes a rule (sets IsDeleted = 1). Returns ServiceResult.
    /// </summary>
    Task<ServiceResult> DeleteRuleAsync(int businessId, int ruleId);

    /// <summary>
    /// Toggles the IsActive flag on a rule. Returns ServiceResult.
    /// </summary>
    Task<ServiceResult> ToggleRuleAsync(int businessId, int ruleId);
}
