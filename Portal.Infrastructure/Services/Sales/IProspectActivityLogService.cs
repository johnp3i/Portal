using Portal.Infrastructure.Models.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Read-only, paged, filterable prospect activity log — the "what did we do" view across prospecting,
/// overall or per-campaign. Tenant-scoped. Never exposes the next-action contact name/phone (presence
/// flags only).
/// </summary>
public interface IProspectActivityLogService
{
    /// <summary>Returns the filtered page of activity rows plus the period summary and pagination.</summary>
    Task<ProspectActivityLogResultDto> GetAsync(ProspectActivityFilter filter, int page, int pageSize);

    /// <summary>
    /// Returns ALL rows matching the filter (not paged), shaped as item DTOs, for CSV export.
    /// Still privacy-safe: no raw next-action contact name/phone, only presence flags.
    /// </summary>
    Task<List<ProspectActivityLogItemDto>> GetAllForExportAsync(ProspectActivityFilter filter);

    /// <summary>Distinct prospect cities for the City filter, within an optional campaign scope.</summary>
    Task<List<string>> GetCitiesAsync(int? campaignId);
}
