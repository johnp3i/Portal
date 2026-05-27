using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for querying AuditLog records with dynamic filter support.
/// Uses EF Core LINQ against [audit].[AuditLog] — read-only, no inserts or updates.
/// </summary>
public class AuditLogQueryRepository : GenericStoredProcedureRepository<AuditLog>
{
    public AuditLogQueryRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Returns a paged, filtered, and ordered slice of AuditLog records for a given business.
    /// All non-null filter parameters are applied with AND logic. Results are ordered by Timestamp DESC.
    /// </summary>
    public async Task<(List<AuditLog> Items, int TotalCount)> GetPagedAsync(
        int businessId,
        string? tableName,
        string? action,
        string? userId,
        DateTime? dateFrom,
        DateTime? dateTo,
        int skip,
        int take)
    {
        try
        {
            var query = _context.Set<AuditLog>()
                .IgnoreQueryFilters()
                .Where(a => a.BusinessId == businessId);

            if (!string.IsNullOrWhiteSpace(tableName))
                query = query.Where(a => a.TableName == tableName);

            if (!string.IsNullOrWhiteSpace(action))
                query = query.Where(a => a.Action == action);

            if (!string.IsNullOrWhiteSpace(userId))
                query = query.Where(a => a.UserId == userId);

            if (dateFrom.HasValue)
                query = query.Where(a => a.Timestamp >= dateFrom.Value);

            if (dateTo.HasValue)
                query = query.Where(a => a.Timestamp <= dateTo.Value);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            return (items, totalCount);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns the distinct TableName values present in AuditLog for the given business,
    /// sorted alphabetically.
    /// </summary>
    public async Task<List<string>> GetDistinctTableNamesAsync(int businessId)
    {
        try
        {
            return await _context.Set<AuditLog>()
                .IgnoreQueryFilters()
                .Where(a => a.BusinessId == businessId)
                .Select(a => a.TableName)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }
}
