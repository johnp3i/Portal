using Portal.Infrastructure.Models.Sales;
using Portal.Infrastructure.Repositories.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Builds the prospect activity log from the repository and shapes it for the UI. Resolves activity
/// type / sentiment display names and computes next-action presence flags. PRIVACY: the raw
/// next-action contact name and phone are never placed on the returned DTO — only the booleans — so
/// personal data is not surfaced in the scannable list (the real values live on the prospect detail).
/// </summary>
public class ProspectActivityLogService : IProspectActivityLogService
{
    private readonly ProspectActivityRepository _activityRepository;
    private readonly IActivityTypeLookupService _activityTypeLookup;
    private readonly ICurrentTenantService _tenantService;

    private static readonly Dictionary<byte, string> SentimentNames = new()
    {
        [1] = "Positive", [2] = "Neutral", [3] = "Negative"
    };

    public ProspectActivityLogService(
        ProspectActivityRepository activityRepository,
        IActivityTypeLookupService activityTypeLookup,
        ICurrentTenantService tenantService)
    {
        _activityRepository = activityRepository;
        _activityTypeLookup = activityTypeLookup;
        _tenantService = tenantService;
    }

    public async Task<ProspectActivityLogResultDto> GetAsync(ProspectActivityFilter filter, int page, int pageSize)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            var (rows, totalCount) = await _activityRepository.GetActivityLogPagedAsync(businessId, filter, page, pageSize);
            var summary = await _activityRepository.GetActivitySummaryAsync(businessId, filter);

            var typeNames = await _activityTypeLookup.GetNameMapAsync();
            var items = rows.Select(r => MapRow(r, typeNames)).ToList();

            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);
            if (page < 1) page = 1;
            if (totalPages > 0 && page > totalPages) page = totalPages;

            return new ProspectActivityLogResultDto
            {
                Items = items,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                Summary = summary
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    // Upper bound on export rows: a prospecting dataset is small; this guards against an unbounded
    // query while being far above any realistic single-period export.
    private const int ExportMaxRows = 10000;

    public async Task<List<ProspectActivityLogItemDto>> GetAllForExportAsync(ProspectActivityFilter filter)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            // Reuse the paged query with a single large page — returns the whole filtered set.
            var (rows, _) = await _activityRepository.GetActivityLogPagedAsync(businessId, filter, 1, ExportMaxRows);
            var typeNames = await _activityTypeLookup.GetNameMapAsync();
            return rows.Select(r => MapRow(r, typeNames)).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<string>> GetCitiesAsync(int? campaignId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            return await _activityRepository.GetDistinctCitiesAsync(businessId, campaignId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    // Shared row → DTO mapping. PRIVACY: carries only presence flags, never the raw contact
    // name/phone — so neither the on-screen list nor the CSV export can leak personal data.
    private static ProspectActivityLogItemDto MapRow(ProspectActivityLogRow r, IReadOnlyDictionary<byte, string> typeNames) => new()
    {
        ActivityId = r.ActivityId,
        ProspectId = r.ProspectId,
        ProspectName = r.ProspectName,
        City = r.City,
        Segment = r.Segment,
        ProspectCampaignId = r.ProspectCampaignId,
        CampaignName = r.CampaignName,
        ActivityType = r.ActivityType,
        ActivityTypeName = typeNames.TryGetValue(r.ActivityType, out var tn) ? tn : "Unknown",
        OccurredAtUtc = r.OccurredAtUtc,
        Sentiment = r.Sentiment,
        SentimentName = r.Sentiment.HasValue && SentimentNames.TryGetValue(r.Sentiment.Value, out var sn) ? sn : null,
        IsFollowUp = r.IsFollowUp,
        Outcome = r.Outcome,
        NextAction = r.NextAction,
        NextActionDate = r.NextActionDate,
        NextActionTime = r.NextActionTime,
        HasNextActionContact = !string.IsNullOrWhiteSpace(r.NextActionContactName),
        HasNextActionPhone = !string.IsNullOrWhiteSpace(r.NextActionPhone),
        HasNextActionEmail = !string.IsNullOrWhiteSpace(r.NextActionEmail),
        PerformedByName = string.IsNullOrWhiteSpace(r.PerformedByUserId) ? null : "You"
    };
}
