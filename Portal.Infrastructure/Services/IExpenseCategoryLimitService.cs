using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for expense category spending limit management and evaluation.
/// </summary>
public interface IExpenseCategoryLimitService
{
    /// <summary>
    /// Returns all configured limits for the current business, joined with category names.
    /// </summary>
    Task<List<ExpenseCategoryLimitViewModel>> GetLimitsForBusinessAsync();

    /// <summary>
    /// Evaluates annual and period limits for a given category/amount/date combination.
    /// </summary>
    Task<LimitCheckResult> EvaluateLimitsAsync(CheckLimitsRequest request);

    /// <summary>
    /// Creates or updates the limit configuration for a business + category.
    /// </summary>
    Task<ServiceResult> SaveLimitAsync(int expenseCategoryId, decimal? annualLimitEur, decimal? periodLimitEur);

    /// <summary>
    /// Clears a specific limit field (annual or period) for a category.
    /// </summary>
    Task<ServiceResult> ClearLimitAsync(int expenseCategoryId, string limitType);
}
