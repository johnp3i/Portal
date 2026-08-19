using Portal.Infrastructure.Entities.Sales;
using Portal.Infrastructure.Models.Sales;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Repositories.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Computes operational sales metrics for the Insights dashboard.
/// All metrics are scoped to the authenticated BusinessId via ICurrentTenantService.
/// </summary>
public class InsightsService : IInsightsService
{
    private readonly ICurrentTenantService _tenantService;
    private readonly LeadRequestRepository _leadRequestRepository;
    private readonly LeadResponseRepository _leadResponseRepository;
    private readonly ActivityFeedRepository _activityFeedRepository;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly LeadStatusTypeRepository _leadStatusTypeRepository;
    private readonly LeadResponseTemplateRepository _leadResponseTemplateRepository;

    // Lead status type IDs
    private const int NewStatusId = 1;
    private const int ContactedStatusId = 2;
    private const int MeetingScheduledStatusId = 4;
    private const int ProposalSentStatusId = 5;
    private const int WonStatusId = 6;
    private const int LostStatusId = 7;
    private const int InactiveStatusId = 8;
    private const int DefaultResponseTimeHours = 24;

    public InsightsService(
        ICurrentTenantService tenantService,
        LeadRequestRepository leadRequestRepository,
        LeadResponseRepository leadResponseRepository,
        ActivityFeedRepository activityFeedRepository,
        InvoiceRepository invoiceRepository,
        LeadStatusTypeRepository leadStatusTypeRepository,
        LeadResponseTemplateRepository leadResponseTemplateRepository)
    {
        _tenantService = tenantService;
        _leadRequestRepository = leadRequestRepository;
        _leadResponseRepository = leadResponseRepository;
        _activityFeedRepository = activityFeedRepository;
        _invoiceRepository = invoiceRepository;
        _leadStatusTypeRepository = leadStatusTypeRepository;
        _leadResponseTemplateRepository = leadResponseTemplateRepository;
    }

    public async Task<InsightsMetricsDto> GetMetricsAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var allLeads = await _leadRequestRepository.GetByBusinessIdAsync(businessId);

            var metrics = new InsightsMetricsDto
            {
                NewLeadsCount = GetNewLeadsCountInternal(allLeads, startDate, endDate),
                ResponseSlaPercentage = await GetResponseSlaPercentageInternal(allLeads, startDate, endDate, businessId),
                RevenueByProduct = await GetRevenueByProductAsync(startDate, endDate),
                RevenueBySource = await GetRevenueBySourceAsync(startDate, endDate),
                AverageSalesCycleDays = GetAverageSalesCycleDaysInternal(allLeads, startDate, endDate)
            };

            var conversionRates = await GetConversionRatesInternal(allLeads, startDate, endDate, businessId);
            metrics.DemoConversionRate = conversionRates.DemoConversionRate;
            metrics.ProposalConversionRate = conversionRates.ProposalConversionRate;
            metrics.WinRate = conversionRates.WinRate;

            return metrics;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<int> GetNewLeadsCountAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var allLeads = await _leadRequestRepository.GetByBusinessIdAsync(businessId);
            return GetNewLeadsCountInternal(allLeads, startDate, endDate);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private int GetNewLeadsCountInternal(List<LeadRequest> allLeads, DateTime startDate, DateTime endDate)
    {
        return allLeads.Count(lead =>
            lead.IsActive &&
            lead.CreatedAtUtc >= startDate &&
            lead.CreatedAtUtc < endDate);
    }

    public async Task<decimal?> GetResponseSlaPercentageAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var allLeads = await _leadRequestRepository.GetByBusinessIdAsync(businessId);
            return await GetResponseSlaPercentageInternal(allLeads, startDate, endDate, businessId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private async Task<decimal?> GetResponseSlaPercentageInternal(List<LeadRequest> allLeads, DateTime startDate, DateTime endDate, int businessId)
    {
        // Filter leads created within the date range
        var leadsInRange = allLeads
            .Where(lead => lead.CreatedAtUtc >= startDate && lead.CreatedAtUtc < endDate)
            .ToList();

        if (leadsInRange.Count == 0)
            return null;

        var leadIds = leadsInRange.Select(l => l.Id).ToList();

        // Get earliest response dates for these leads
        var earliestResponses = await _leadResponseRepository.GetEarliestResponseDatesAsync(leadIds, businessId);

        if (earliestResponses.Count == 0)
            return null;

        // Get all active templates for SLA threshold lookup
        var templates = await _leadResponseTemplateRepository.GetAllActiveAsync(businessId);

        int leadsWithinThreshold = 0;
        int leadsWithResponse = 0;

        foreach (var lead in leadsInRange)
        {
            if (!earliestResponses.TryGetValue(lead.Id, out var earliestResponseDate))
                continue;

            leadsWithResponse++;

            // Compute elapsed hours
            var elapsedHours = (earliestResponseDate - lead.CreatedAtUtc).TotalHours;

            // Find matching template by ProductId for SLA threshold
            var matchingTemplate = templates.FirstOrDefault(t => t.ProductId == lead.ProductId && t.ProductId != null)
                ?? templates.FirstOrDefault(t => t.ProductId == null);

            var thresholdHours = matchingTemplate?.ResponseTimeInHours ?? DefaultResponseTimeHours;

            if (elapsedHours <= thresholdHours)
                leadsWithinThreshold++;
        }

        if (leadsWithResponse == 0)
            return null;

        return Math.Round((decimal)leadsWithinThreshold / leadsWithResponse * 100, 2);
    }

    public async Task<ConversionRatesDto> GetConversionRatesAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var allLeads = await _leadRequestRepository.GetByBusinessIdAsync(businessId);
            return await GetConversionRatesInternal(allLeads, startDate, endDate, businessId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private async Task<ConversionRatesDto> GetConversionRatesInternal(List<LeadRequest> allLeads, DateTime startDate, DateTime endDate, int businessId)
    {
        var result = new ConversionRatesDto();

        // Get all stage change activity entries in the date range
        var stageChanges = await _activityFeedRepository.GetStageChangesInRangeAsync(startDate, endDate, businessId);

        // Demo Conversion: leads that transitioned to "Meeting Scheduled" in range
        // divided by leads at New/Contacted at any point during range
        var leadsToMeetingScheduled = stageChanges
            .Where(sc => sc.Description.Contains("Meeting Scheduled", StringComparison.OrdinalIgnoreCase))
            .Select(sc => sc.LeadRequestId)
            .Distinct()
            .Count();

        // Denominator: leads that were at New or Contacted during the range
        // Approximation: leads created before end of range that were/are at New or Contacted
        var leadsAtNewOrContacted = allLeads
            .Where(lead => lead.CreatedAtUtc < endDate &&
                (lead.LeadStatusTypeId == NewStatusId || lead.LeadStatusTypeId == ContactedStatusId ||
                 stageChanges.Any(sc => sc.LeadRequestId == lead.Id)))
            .Select(lead => lead.Id)
            .Distinct()
            .Count();

        result.DemoConversionRate = leadsAtNewOrContacted > 0
            ? Math.Round((decimal)leadsToMeetingScheduled / leadsAtNewOrContacted * 100, 2)
            : null;

        // Proposal Conversion: leads that transitioned to "Proposal Sent" in range
        // divided by leads at non-terminal stages
        var leadsToProposalSent = stageChanges
            .Where(sc => sc.Description.Contains("Proposal Sent", StringComparison.OrdinalIgnoreCase))
            .Select(sc => sc.LeadRequestId)
            .Distinct()
            .Count();

        var leadsAtNonTerminal = allLeads
            .Where(lead => lead.CreatedAtUtc < endDate &&
                lead.LeadStatusTypeId != WonStatusId &&
                lead.LeadStatusTypeId != LostStatusId &&
                lead.LeadStatusTypeId != InactiveStatusId)
            .Count();

        result.ProposalConversionRate = leadsAtNonTerminal > 0
            ? Math.Round((decimal)leadsToProposalSent / leadsAtNonTerminal * 100, 2)
            : null;

        // Win Rate: Won leads with ClosedAtUtc in range / (Won + Lost) with ClosedAtUtc in range
        // Excluding Inactive
        var wonLeadsInRange = allLeads
            .Count(lead => lead.ClosedAtUtc.HasValue &&
                lead.ClosedAtUtc.Value >= startDate &&
                lead.ClosedAtUtc.Value < endDate &&
                lead.LeadStatusTypeId == WonStatusId);

        var lostLeadsInRange = allLeads
            .Count(lead => lead.ClosedAtUtc.HasValue &&
                lead.ClosedAtUtc.Value >= startDate &&
                lead.ClosedAtUtc.Value < endDate &&
                lead.LeadStatusTypeId == LostStatusId);

        var totalTerminalInRange = wonLeadsInRange + lostLeadsInRange;

        result.WinRate = totalTerminalInRange > 0
            ? Math.Round((decimal)wonLeadsInRange / totalTerminalInRange * 100, 2)
            : null;

        return result;
    }

    public async Task<List<RevenueBreakdownDto>> GetRevenueByProductAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            return await _invoiceRepository.GetRevenueByProductAsync(startDate, endDate, WonStatusId, businessId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<RevenueBreakdownDto>> GetRevenueBySourceAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            return await _invoiceRepository.GetRevenueBySourceAsync(startDate, endDate, WonStatusId, businessId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<double?> GetAverageSalesCycleDaysAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var allLeads = await _leadRequestRepository.GetByBusinessIdAsync(businessId);
            return GetAverageSalesCycleDaysInternal(allLeads, startDate, endDate);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private double? GetAverageSalesCycleDaysInternal(List<LeadRequest> allLeads, DateTime startDate, DateTime endDate)
    {
        // Filter leads with ClosedAtUtc in range, status Won or Lost, excluding Inactive
        var qualifyingLeads = allLeads
            .Where(lead => lead.ClosedAtUtc.HasValue &&
                lead.ClosedAtUtc.Value >= startDate &&
                lead.ClosedAtUtc.Value < endDate &&
                (lead.LeadStatusTypeId == WonStatusId || lead.LeadStatusTypeId == LostStatusId))
            .ToList();

        if (qualifyingLeads.Count == 0)
            return null;

        var totalDays = qualifyingLeads
            .Sum(lead => (lead.ClosedAtUtc!.Value - lead.CreatedAtUtc).TotalDays);

        return Math.Round(totalDays / qualifyingLeads.Count, 2);
    }
}
