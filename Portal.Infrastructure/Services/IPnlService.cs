using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Computes Profit &amp; Loss figures from existing Payment and Purchase data.
/// All queries are scoped to the current tenant via ICurrentTenantService and global query filters.
/// </summary>
public interface IPnlService
{
    /// <summary>
    /// Computes the full P&amp;L summary for the given period, including trend comparison.
    /// </summary>
    Task<PnlSummaryDto> GetSummaryAsync(PnlPeriodRequest request);

    /// <summary>
    /// Resolves a predefined period label to concrete start/end dates based on the reference date.
    /// </summary>
    PnlDateRange ResolvePeriod(PnlPeriodType periodType, DateTime referenceDate);

    /// <summary>
    /// Validates a custom date range (start must be &lt;= end).
    /// </summary>
    PnlValidationResult ValidateCustomRange(DateOnly startDate, DateOnly endDate);
}
