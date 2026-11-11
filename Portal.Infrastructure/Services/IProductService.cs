using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Service interface for product catalog management operations.
/// </summary>
public interface IProductService
{
    // CRUD
    Task<ServiceResult> CreateProductAsync(Product product, string userId);
    Task<ServiceResult> UpdateProductAsync(Product product, string userId);
    Task<ServiceResult> DeactivateProductAsync(int productId);
    Task<Product?> GetProductByIdAsync(int productId);

    // Listing & Search
    Task<PagedResult<Product>> GetProductsPagedAsync(ProductListFilter filter, int page, int pageSize = 15);

    // KPIs & Analytics
    Task<ProductKpiDto> GetKpisAsync();
    Task<List<ProductUsageDto>> GetTopProductsByUsageAsync(int top = 10);

    /// <summary>
    /// Top products by revenue and by units sold over the given period, from issued, non-deleted
    /// invoices. <paramref name="period"/> is whitelisted to "year" (current calendar year, default),
    /// "last12", "quarter" (last 3 months), or "all".
    /// </summary>
    Task<ProductPerformanceResult> GetProductPerformanceAsync(string period, int top = 10);

    // Auto-population (called after line item persistence)
    Task AutoPopulateFromLineItemAsync(string? productCode, string description, decimal unitPrice, decimal vatRate, string userId);

    // Price History
    Task<List<ProductPriceHistory>> GetPriceHistoryAsync(int productId);
}
