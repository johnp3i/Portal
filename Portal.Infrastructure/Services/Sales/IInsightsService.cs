using Portal.Infrastructure.Models.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Computes operational sales metrics for the Insights dashboard.
/// </summary>
public interface IInsightsService
{
    Task<InsightsMetricsDto> GetMetricsAsync(DateTime startDate, DateTime endDate);
    Task<int> GetNewLeadsCountAsync(DateTime startDate, DateTime endDate);
    Task<decimal?> GetResponseSlaPercentageAsync(DateTime startDate, DateTime endDate);
    Task<ConversionRatesDto> GetConversionRatesAsync(DateTime startDate, DateTime endDate);
    Task<List<RevenueBreakdownDto>> GetRevenueByProductAsync(DateTime startDate, DateTime endDate);
    Task<List<RevenueBreakdownDto>> GetRevenueBySourceAsync(DateTime startDate, DateTime endDate);
    Task<double?> GetAverageSalesCycleDaysAsync(DateTime startDate, DateTime endDate);
}
