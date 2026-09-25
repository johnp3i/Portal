using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Business logic for prospecting campaigns — the container for prospects being researched
/// and approached before they enter the lead pipeline.
/// </summary>
public interface IProspectCampaignService
{
    Task<ServiceResult> CreateCampaignAsync(CreateProspectCampaignRequest request);
    Task<ServiceResult> UpdateCampaignAsync(UpdateProspectCampaignRequest request);
    Task<List<ProspectCampaignListDto>> GetCampaignsAsync();
    Task<ProspectCampaignDashboardDto?> GetDashboardAsync(int campaignId);

    /// <summary>Funnel counts for a campaign (used by the dashboard and, later, the assistant).</summary>
    Task<ProspectFunnelDto> GetFunnelAsync(int campaignId);

    /// <summary>
    /// V1 weekly objective. Static in V1 (target + follow-ups due). The future Prospecting
    /// Assistant will replace the message with a real recommendation.
    /// </summary>
    Task<WeeklyObjectiveDto> GetWeeklyObjectiveAsync(int campaignId);
}
