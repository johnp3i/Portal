using Portal.Infrastructure.Models.ProductInsights;

namespace Portal.Infrastructure.Services;

public interface IProductInsightsService
{
    Task<ProductKpiDto> GetSalesKpisAsync(string productCode, int businessId, decimal costPrice);
    Task<List<ProductCustomerDto>> GetTopCustomersAsync(string productCode, int businessId, int top = 5);
    Task<ProductCustomerSummaryDto> GetCustomerSummaryAsync(string productCode, int businessId);
    Task<List<MonthlyProductRevenueDto>> GetMonthlyTrendAsync(string productCode, int businessId, int months = 12);
    Task<ProductForecastDto> GetForecastAsync(string productCode, int businessId, decimal sellingPrice);
    Task<ProductPipelineDto?> GetPipelineActivityAsync(int productId, int businessId);
}
