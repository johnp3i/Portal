using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Provides filtered, paginated access to system log entries from the Portal.Logging database.
/// No tenant scoping — SuperAdmins see all platform logs for cross-tenant debugging.
/// </summary>
public interface ISystemLogQueryService
{
    /// <summary>
    /// Returns a paginated, filtered list of log entries.
    /// PageSize is clamped to [1, 200]; PageNumber is clamped to minimum 1.
    /// When PageNumber exceeds TotalPages, Items is empty but TotalCount and TotalPages remain accurate.
    /// </summary>
    Task<PagedResult<LogEntry>> GetLogsAsync(SystemLogFilter filter);

    /// <summary>
    /// Returns KPI counts (error 24h, warning 24h, total today) in a single round-trip.
    /// </summary>
    Task<SystemLogKpiCounts> GetKpiCountsAsync();

    /// <summary>
    /// Returns all matching log entries up to maxRows for CSV export.
    /// Applies the same filter validation/clamping as GetLogsAsync.
    /// IsTruncated is true when TotalCount exceeds maxRows.
    /// </summary>
    Task<ExportResult<LogEntry>> GetExportLogsAsync(SystemLogFilter filter, int maxRows = 10000);

    /// <summary>
    /// Returns distinct log levels present in the Logs table, sorted alphabetically.
    /// </summary>
    Task<List<string>> GetDistinctLevelsAsync();

    /// <summary>
    /// Returns distinct source contexts present in the Logs table, sorted alphabetically.
    /// </summary>
    Task<List<string>> GetDistinctSourceContextsAsync();
}
