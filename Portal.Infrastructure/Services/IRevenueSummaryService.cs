using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for Z-Report manual entry and management.
/// </summary>
public interface IRevenueSummaryService
{
    Task<PagedResult<RevenueSummary>> GetPagedAsync(
        int? revenueSourceId = null,
        DateOnly? dateFrom = null,
        DateOnly? dateTo = null,
        string? zReportNumber = null,
        int page = 1,
        int pageSize = 15,
        string dateMode = "period");

    Task<RevenueSummary?> GetByIdAsync(int id);
    Task<List<RevenueSummaryLine>> GetLinesAsync(int revenueSummaryId);
    Task<ServiceResult> CreateAsync(RevenueSummary summary, List<RevenueSummaryLine> lines);
    Task<ServiceResult> UpdateAsync(RevenueSummary summary, List<RevenueSummaryLine> lines);
    Task<ServiceResult> DeleteAsync(int id);
    Task<ServiceResult> RestoreAsync(int id);
    Task<bool> IsLockedAsync(int id);
}
