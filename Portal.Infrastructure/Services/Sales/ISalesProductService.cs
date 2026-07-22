using Portal.Infrastructure.Entities.Sales;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Business logic for sales product management.
/// </summary>
public interface ISalesProductService
{
    Task<ServiceResult> CreateProductAsync(CreateSalesProductRequest request);
    Task<ServiceResult> UpdateProductAsync(UpdateSalesProductRequest request);
    Task<ServiceResult> DeactivateProductAsync(int id);
    Task<SalesProduct?> GetByIdAsync(int id);
    Task<PagedResult<SalesProduct>> GetProductsPagedAsync(string? searchTerm, int page, int pageSize);
    Task<List<SalesProduct>> GetActiveProductsAsync();
}
