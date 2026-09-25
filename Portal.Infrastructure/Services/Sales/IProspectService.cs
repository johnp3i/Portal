using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Business logic for prospects — researched targets inside a campaign. A prospect is NOT a
/// Sales Contact; it only enters the lead pipeline through Convert-to-Lead.
/// </summary>
public interface IProspectService
{
    Task<ServiceResult> CreateProspectAsync(CreateProspectRequest request);
    Task<ServiceResult> UpdateProspectAsync(UpdateProspectRequest request);
    Task<ServiceResult> UpdateStatusAsync(int prospectId, byte status);

    Task<PagedResult<ProspectListDto>> GetProspectsPagedAsync(
        int campaignId, string? priority, byte? status, string? segment, string? nextAction, int page, int pageSize);

    Task<ProspectDetailDto?> GetProspectDetailAsync(int prospectId);

    /// <summary>Adds a timeline activity. Never creates a Lead or Contact. Optionally advances status.</summary>
    Task<ServiceResult> AddActivityAsync(AddProspectActivityRequest request);

    /// <summary>Edits an existing timeline activity's fields.</summary>
    Task<ServiceResult> UpdateActivityAsync(UpdateProspectActivityRequest request);

    /// <summary>
    /// Converts a prospect into a Sales Contact + Lead (Source = Cold Call), keeping a
    /// back-reference on the prospect for later scoring-performance measurement.
    /// </summary>
    Task<ServiceResult> ConvertToLeadAsync(ConvertProspectRequest request);
}
