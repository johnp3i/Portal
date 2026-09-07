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

    /// <summary>
    /// Computes the core P&amp;L figures (revenue collected, COGS, operating expenses, net) for an
    /// explicit business and date range. Unlike <see cref="GetSummaryAsync"/>, this takes the
    /// business id explicitly and does NOT read the current tenant — safe to call from a
    /// background service (e.g. the scheduled Financial Snapshot digest) that has no HTTP context.
    /// </summary>
    Task<PnlSnapshotDto> ComputeSnapshotAsync(int businessId, DateOnly startDate, DateOnly endDate);
}
