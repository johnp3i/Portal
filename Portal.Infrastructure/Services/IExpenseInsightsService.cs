using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.ExpenseInsights;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Provides expense analytics computations including category breakdown, trend analysis,
/// budget management, and CSV export. All queries are scoped to the current tenant
/// via ICurrentTenantService and global query filters.
/// </summary>
public interface IExpenseInsightsService
{
    /// <summary>
    /// Computes the full expense insights dataset for the given period.
    /// Includes category breakdown, summary KPIs, budget status, and top suppliers.
    /// </summary>
    Task<ExpenseInsightsDto> GetInsightsDataAsync(ExpenseInsightsPeriodRequest request);

    /// <summary>
    /// Computes monthly totals per category for the last 12 months (trend data).
    /// </summary>
    Task<ExpenseInsightsTrendDto> GetTrendDataAsync();

    /// <summary>
    /// Creates or updates a budget limit for a category.
    /// Pass null PeriodLimitEur to clear the limit.
    /// </summary>
    Task<ServiceResult> UpsertBudgetLimitAsync(int expenseCategoryId, decimal? periodLimitEur);

    /// <summary>
    /// Resolves a period type to concrete start/end dates.
    /// Reuses the same logic as PnlService.
    /// </summary>
    ExpenseInsightsDateRange ResolvePeriod(PnlPeriodType periodType, DateTime referenceDate);

    /// <summary>
    /// Validates a custom date range.
    /// </summary>
    ExpenseInsightsValidationResult ValidateCustomRange(DateOnly startDate, DateOnly endDate);

    /// <summary>
    /// Generates CSV content for the current breakdown.
    /// </summary>
    Task<ExportResult> ExportCsvAsync(ExpenseInsightsPeriodRequest request);
}
