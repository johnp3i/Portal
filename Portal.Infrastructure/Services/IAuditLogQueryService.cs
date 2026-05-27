using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Provides filtered, paginated access to the audit log for the current tenant.
/// All queries are automatically scoped to the current business via ICurrentTenantService.
/// </summary>
public interface IAuditLogQueryService
{
    /// <summary>
    /// Returns a paginated, filtered list of audit log entries for the current tenant.
    /// PageSize is clamped to [1, 100]; PageNumber is clamped to a minimum of 1.
    /// When PageNumber exceeds TotalPages, Items is empty but TotalCount and TotalPages remain accurate.
    /// </summary>
    Task<PagedResult<AuditLog>> GetAuditLogsAsync(AuditLogFilter filter);

    /// <summary>
    /// Returns distinct table names present in the audit log for the current tenant, sorted alphabetically.
    /// </summary>
    Task<List<string>> GetDistinctTableNamesAsync();
}
