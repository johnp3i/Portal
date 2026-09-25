using Portal.Infrastructure.Entities.Sales;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;
using Portal.Infrastructure.Repositories.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Business logic for prospecting campaigns.
/// </summary>
public class ProspectCampaignService : IProspectCampaignService
{
    private readonly ProspectCampaignRepository _campaignRepository;
    private readonly ProspectActivityRepository _activityRepository;
    private readonly ISalesProductService _productService;
    private readonly ICurrentTenantService _tenantService;

    // How many entries the campaign dashboard's Recent Activity feed shows.
    private const int RecentActivityCount = 20;

    // Campaign status: 1 Draft, 2 Active, 3 Closed.
    private static readonly Dictionary<byte, string> CampaignStatusNames = new()
    {
        [1] = "Draft",
        [2] = "Active",
        [3] = "Closed"
    };

    // Activity type: 1 Research, 2 Call, 3 Email, 4 Meeting, 5 Demo, 6 Note, 7 Social, 8 Other.
    private static readonly Dictionary<byte, string> ActivityTypeNames = new()
    {
        [1] = "Research", [2] = "Call", [3] = "Email", [4] = "Meeting",
        [5] = "Demo", [6] = "Note", [7] = "Social", [8] = "Other"
    };

    // Sentiment: 1 Positive, 2 Neutral, 3 Negative.
    private static readonly Dictionary<byte, string> SentimentNames = new()
    {
        [1] = "Positive", [2] = "Neutral", [3] = "Negative"
    };

    public ProspectCampaignService(
        ProspectCampaignRepository campaignRepository,
        ProspectActivityRepository activityRepository,
        ISalesProductService productService,
        ICurrentTenantService tenantService)
    {
        _campaignRepository = campaignRepository;
        _activityRepository = activityRepository;
        _productService = productService;
        _tenantService = tenantService;
    }

    internal static string CampaignStatusName(byte status)
        => CampaignStatusNames.TryGetValue(status, out var name) ? name : "Unknown";

    public async Task<ServiceResult> CreateCampaignAsync(CreateProspectCampaignRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            if (string.IsNullOrWhiteSpace(request.Name))
                return ServiceResult.Fail("Campaign name is required.");

            if (request.WeeklyCallTarget < 0)
                return ServiceResult.Fail("Weekly call target cannot be negative.");

            if (request.StartDate.HasValue && request.EndDate.HasValue && request.EndDate < request.StartDate)
                return ServiceResult.Fail("End date cannot be before the start date.");

            var entity = new ProspectCampaign
            {
                BusinessId = businessId,
                Name = request.Name.Trim(),
                SalesProductId = request.SalesProductId,
                Description = request.Description,
                Market = request.Market,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                WeeklyCallTarget = request.WeeklyCallTarget,
                Status = 2, // new campaigns start Active
                Notes = request.Notes
            };

            var id = await _campaignRepository.InsertAsync(entity);
            return ServiceResult.Ok(id);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> UpdateCampaignAsync(UpdateProspectCampaignRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            var existing = await _campaignRepository.GetByIdAsync(request.Id, businessId);
            if (existing == null)
                return ServiceResult.Fail("Campaign not found.");

            if (string.IsNullOrWhiteSpace(request.Name))
                return ServiceResult.Fail("Campaign name is required.");

            if (request.WeeklyCallTarget < 0)
                return ServiceResult.Fail("Weekly call target cannot be negative.");

            if (request.StartDate.HasValue && request.EndDate.HasValue && request.EndDate < request.StartDate)
                return ServiceResult.Fail("End date cannot be before the start date.");

            if (!CampaignStatusNames.ContainsKey(request.Status))
                return ServiceResult.Fail("Invalid campaign status.");

            existing.Name = request.Name.Trim();
            existing.SalesProductId = request.SalesProductId;
            existing.Description = request.Description;
            existing.Market = request.Market;
            existing.StartDate = request.StartDate;
            existing.EndDate = request.EndDate;
            existing.WeeklyCallTarget = request.WeeklyCallTarget;
            existing.Status = request.Status;
            existing.Notes = request.Notes;

            await _campaignRepository.UpdateAsync(existing);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<ProspectCampaignListDto>> GetCampaignsAsync()
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var campaigns = await _campaignRepository.GetAllByBusinessAsync(businessId);
            if (campaigns.Count == 0)
                return new List<ProspectCampaignListDto>();

            var products = await _productService.GetActiveProductsAsync();
            var productNames = products.ToDictionary(p => p.Id, p => p.Name);

            var result = new List<ProspectCampaignListDto>(campaigns.Count);
            foreach (var c in campaigns)
            {
                var funnel = await _campaignRepository.GetFunnelCountsAsync(c.Id, businessId);
                var total = funnel.Values.Sum();
                var converted = funnel.TryGetValue(5, out var conv) ? conv : 0;

                result.Add(new ProspectCampaignListDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    SalesProductId = c.SalesProductId,
                    ProductName = c.SalesProductId.HasValue && productNames.TryGetValue(c.SalesProductId.Value, out var pn) ? pn : null,
                    Market = c.Market,
                    StartDate = c.StartDate,
                    EndDate = c.EndDate,
                    WeeklyCallTarget = c.WeeklyCallTarget,
                    Status = c.Status,
                    StatusName = CampaignStatusName(c.Status),
                    ProspectCount = total,
                    ConvertedCount = converted,
                    CreatedAtUtc = c.CreatedAtUtc
                });
            }
            return result;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ProspectCampaignDashboardDto?> GetDashboardAsync(int campaignId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var campaign = await _campaignRepository.GetByIdAsync(campaignId, businessId);
            if (campaign == null) return null;

            string? productName = null;
            if (campaign.SalesProductId.HasValue)
            {
                var product = await _productService.GetByIdAsync(campaign.SalesProductId.Value);
                productName = product?.Name;
            }

            var funnel = await BuildFunnelAsync(campaignId, businessId);
            var objective = await BuildWeeklyObjectiveAsync(campaign, businessId);
            var recentActivity = await BuildRecentActivityAsync(campaignId, businessId);

            return new ProspectCampaignDashboardDto
            {
                Id = campaign.Id,
                Name = campaign.Name,
                ProductName = productName,
                SalesProductId = campaign.SalesProductId,
                Market = campaign.Market,
                Description = campaign.Description,
                StartDate = campaign.StartDate,
                EndDate = campaign.EndDate,
                WeeklyCallTarget = campaign.WeeklyCallTarget,
                Status = campaign.Status,
                StatusName = CampaignStatusName(campaign.Status),
                Notes = campaign.Notes,
                Funnel = funnel,
                WeeklyObjective = objective,
                RecentActivity = recentActivity
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ProspectFunnelDto> GetFunnelAsync(int campaignId)
    {
        try
        {
            return await BuildFunnelAsync(campaignId, _tenantService.CurrentBusinessId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<WeeklyObjectiveDto> GetWeeklyObjectiveAsync(int campaignId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var campaign = await _campaignRepository.GetByIdAsync(campaignId, businessId);
            if (campaign == null)
                return new WeeklyObjectiveDto { Message = "Campaign not found." };

            return await BuildWeeklyObjectiveAsync(campaign, businessId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private async Task<ProspectFunnelDto> BuildFunnelAsync(int campaignId, int businessId)
    {
        var counts = await _campaignRepository.GetFunnelCountsAsync(campaignId, businessId);
        int C(byte status) => counts.TryGetValue(status, out var n) ? n : 0;

        return new ProspectFunnelDto
        {
            Research = C(1),
            Ready = C(2),
            Contacting = C(3),
            Engaged = C(4),
            Converted = C(5),
            Disqualified = C(6),
            Total = counts.Values.Sum()
        };
    }

    /// <summary>Recent activity across all prospects in the campaign (newest first), for the dashboard feed.</summary>
    private async Task<List<CampaignActivityFeedItemDto>> BuildRecentActivityAsync(int campaignId, int businessId)
    {
        var rows = await _activityRepository.GetRecentByCampaignAsync(campaignId, businessId, RecentActivityCount);
        return rows.Select(r => new CampaignActivityFeedItemDto
        {
            ProspectId = r.ProspectId,
            ProspectName = r.ProspectName,
            ActivityType = r.ActivityType,
            ActivityTypeName = ActivityTypeNames.TryGetValue(r.ActivityType, out var tn) ? tn : "Unknown",
            Outcome = r.Outcome,
            OccurredAtUtc = r.OccurredAtUtc,
            Sentiment = r.Sentiment,
            SentimentName = r.Sentiment.HasValue && SentimentNames.TryGetValue(r.Sentiment.Value, out var sn) ? sn : null,
            IsFollowUp = r.IsFollowUp
        }).ToList();
    }

    /// <summary>
    /// V1 static objective. The follow-ups-due count is real (drives the working list);
    /// the message is a fixed placeholder until the Prospecting Assistant is built.
    /// </summary>
    private async Task<WeeklyObjectiveDto> BuildWeeklyObjectiveAsync(ProspectCampaign campaign, int businessId)
    {
        var followUpsDue = await _campaignRepository.GetFollowUpsDueCountAsync(campaign.Id, businessId, DateTime.UtcNow.Date);

        var message = campaign.WeeklyCallTarget > 0
            ? $"This week's objective: make {campaign.WeeklyCallTarget} cold calls. {followUpsDue} follow-up(s) are due."
            : $"{followUpsDue} follow-up(s) are due. Set a weekly call target to track outreach pace.";

        return new WeeklyObjectiveDto
        {
            WeeklyCallTarget = campaign.WeeklyCallTarget,
            FollowUpsDue = followUpsDue,
            Message = message,
            IsAssistantActive = false // V1 placeholder — assistant is backlogged
        };
    }
}
