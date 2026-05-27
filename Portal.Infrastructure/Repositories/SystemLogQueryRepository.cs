using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for querying LogEntry records from the Portal.Logging database.
/// Uses EF Core LINQ against [dbo].[Logs] — read-only, no inserts or updates.
/// </summary>
public class SystemLogQueryRepository : GenericStoredProcedureRepository<LogEntry>
{
    public SystemLogQueryRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Returns a paged, filtered, and ordered slice of LogEntry records.
    /// All non-null filter parameters are applied with AND logic.
    /// Results are ordered by TimeStamp DESC.
    /// </summary>
    public async Task<(List<LogEntry> Items, int TotalCount)> GetPagedAsync(
        string? level,
        DateTime? dateFrom,
        DateTime? dateTo,
        string? userId,
        string? correlationId,
        string? sourceContext,
        string? requestPath,
        int skip,
        int take)
    {
        try
        {
            var query = _context.Set<LogEntry>().AsQueryable();

            if (!string.IsNullOrWhiteSpace(level))
                query = query.Where(l => l.Level != null && l.Level.ToLower() == level.ToLower());

            if (dateFrom.HasValue)
                query = query.Where(l => l.TimeStamp >= dateFrom.Value);

            if (dateTo.HasValue)
                query = query.Where(l => l.TimeStamp <= dateTo.Value);

            if (!string.IsNullOrWhiteSpace(userId))
                query = query.Where(l => l.UserId == userId);

            if (!string.IsNullOrWhiteSpace(correlationId))
                query = query.Where(l => l.CorrelationId == correlationId);

            if (!string.IsNullOrWhiteSpace(sourceContext))
                query = query.Where(l => l.SourceContext == sourceContext);

            if (!string.IsNullOrWhiteSpace(requestPath))
                query = query.Where(l => l.RequestPath != null && l.RequestPath.Contains(requestPath));

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(l => l.TimeStamp)
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
    /// Returns all matching records (up to maxRows) without pagination, ordered by TimeStamp DESC.
    /// Reuses the same filter logic as GetPagedAsync but replaces Skip/Take with a single .Take(maxRows).
    /// Returns the unfiltered-by-take total count for truncation detection.
    /// </summary>
    public async Task<(List<LogEntry> Items, int TotalCount)> GetAllMatchingAsync(
        string? level,
        DateTime? dateFrom,
        DateTime? dateTo,
        string? userId,
        string? correlationId,
        string? sourceContext,
        string? requestPath,
        int maxRows)
    {
        try
        {
            var query = _context.Set<LogEntry>().AsQueryable();

            if (!string.IsNullOrWhiteSpace(level))
                query = query.Where(l => l.Level != null && l.Level.ToLower() == level.ToLower());

            if (dateFrom.HasValue)
                query = query.Where(l => l.TimeStamp >= dateFrom.Value);

            if (dateTo.HasValue)
                query = query.Where(l => l.TimeStamp <= dateTo.Value);

            if (!string.IsNullOrWhiteSpace(userId))
                query = query.Where(l => l.UserId == userId);

            if (!string.IsNullOrWhiteSpace(correlationId))
                query = query.Where(l => l.CorrelationId == correlationId);

            if (!string.IsNullOrWhiteSpace(sourceContext))
                query = query.Where(l => l.SourceContext == sourceContext);

            if (!string.IsNullOrWhiteSpace(requestPath))
                query = query.Where(l => l.RequestPath != null && l.RequestPath.Contains(requestPath));

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(l => l.TimeStamp)
                .Take(maxRows)
                .ToListAsync();

            return (items, totalCount);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns KPI counts in a single database round-trip using conditional aggregation.
    /// Errors: Level == "Error" AND TimeStamp within last 24 hours.
    /// Warnings: Level == "Warning" AND TimeStamp within last 24 hours.
    /// Total Today: TimeStamp on or after today's UTC midnight.
    /// </summary>
    public async Task<(int ErrorCount24h, int WarningCount24h, int TotalToday)> GetKpiCountsAsync()
    {
        try
        {
            var twentyFourHoursAgo = DateTime.UtcNow.AddHours(-24);
            var todayMidnightUtc = DateTime.UtcNow.Date;

            var counts = await _context.Set<LogEntry>()
                .Where(l => l.TimeStamp >= twentyFourHoursAgo || l.TimeStamp >= todayMidnightUtc)
                .GroupBy(l => 1)
                .Select(g => new
                {
                    ErrorCount24h = g.Count(l => l.Level == "Error" && l.TimeStamp >= twentyFourHoursAgo),
                    WarningCount24h = g.Count(l => l.Level == "Warning" && l.TimeStamp >= twentyFourHoursAgo),
                    TotalToday = g.Count(l => l.TimeStamp >= todayMidnightUtc)
                })
                .FirstOrDefaultAsync();

            if (counts == null)
                return (0, 0, 0);

            return (counts.ErrorCount24h, counts.WarningCount24h, counts.TotalToday);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns distinct Level values from the Logs table, sorted alphabetically.
    /// </summary>
    public async Task<List<string>> GetDistinctLevelsAsync()
    {
        try
        {
            return await _context.Set<LogEntry>()
                .Where(l => l.Level != null)
                .Select(l => l.Level!)
                .Distinct()
                .OrderBy(l => l)
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns distinct SourceContext values from the Logs table, sorted alphabetically.
    /// </summary>
    public async Task<List<string>> GetDistinctSourceContextsAsync()
    {
        try
        {
            return await _context.Set<LogEntry>()
                .Where(l => l.SourceContext != null)
                .Select(l => l.SourceContext!)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }
}
