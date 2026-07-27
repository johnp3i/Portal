using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Service interface for the Business Insights admin page.
/// </summary>
public interface IBusinessInsightsService
{
    /// <summary>
    /// Returns filtered and paginated business insights with summary statistics.
    /// </summary>
    Task<(List<BusinessInsightDto> Items, BusinessInsightSummaryDto Summary, int TotalCount)> GetBusinessInsightsAsync(BusinessInsightFilter filter);
}
