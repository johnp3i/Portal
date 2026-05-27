using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Provides filtered, paginated access to system log entries from the Portal.Logging database.
/// No tenant scoping — SuperAdmins see all platform logs for cross-tenant debugging.
/// PageSize is clamped to [1, 200]; PageNumber is clamped to minimum 1.
/// </summary>
public class SystemLogQueryService : ISystemLogQueryService
{
    private readonly SystemLogQueryRepository _repository;

    public SystemLogQueryService(SystemLogQueryRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<LogEntry>> GetLogsAsync(SystemLogFilter filter)
    {
        try
        {
            var pageSize = Math.Clamp(filter.PageSize, 1, 200);
            var pageNumber = Math.Max(filter.PageNumber, 1);
            var skip = (pageNumber - 1) * pageSize;

            var (items, totalCount) = await _repository.GetPagedAsync(
                filter.Level,
                filter.DateFrom,
                filter.DateTo,
                filter.UserId,
                filter.CorrelationId,
                filter.SourceContext,
                filter.RequestPath,
                skip,
                pageSize);

            var totalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pageSize);

            if (pageNumber > totalPages && totalCount > 0)
            {
                return new PagedResult<LogEntry>
                {
                    Items = new List<LogEntry>(),
                    CurrentPage = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
            }

            return new PagedResult<LogEntry>
            {
                Items = items,
                CurrentPage = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<SystemLogKpiCounts> GetKpiCountsAsync()
    {
        try
        {
            var (errorCount24h, warningCount24h, totalToday) = await _repository.GetKpiCountsAsync();

            return new SystemLogKpiCounts
            {
                ErrorCount24h = errorCount24h,
                WarningCount24h = warningCount24h,
                TotalToday = totalToday
            };
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<ExportResult<LogEntry>> GetExportLogsAsync(SystemLogFilter filter, int maxRows = 10000)
    {
        try
        {
            var (items, totalCount) = await _repository.GetAllMatchingAsync(
                filter.Level,
                filter.DateFrom,
                filter.DateTo,
                filter.UserId,
                filter.CorrelationId,
                filter.SourceContext,
                filter.RequestPath,
                maxRows);

            return new ExportResult<LogEntry>
            {
                Items = items,
                TotalCount = totalCount,
                IsTruncated = totalCount > maxRows
            };
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<List<string>> GetDistinctLevelsAsync()
    {
        try
        {
            return await _repository.GetDistinctLevelsAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<List<string>> GetDistinctSourceContextsAsync()
    {
        try
        {
            return await _repository.GetDistinctSourceContextsAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }
}
