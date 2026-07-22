using Portal.Infrastructure.Entities.Sales;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;
using Portal.Infrastructure.Repositories.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Business logic for sales product management.
/// </summary>
public class SalesProductService : ISalesProductService
{
    private readonly SalesProductRepository _productRepository;
    private readonly ICurrentTenantService _tenantService;

    public SalesProductService(SalesProductRepository productRepository, ICurrentTenantService tenantService)
    {
        _productRepository = productRepository;
        _tenantService = tenantService;
    }

    public async Task<ServiceResult> CreateProductAsync(CreateSalesProductRequest request)
    {
        try
        {
            var entity = new SalesProduct
            {
                BusinessId = _tenantService.CurrentBusinessId,
                Name = request.Name,
                Description = request.Description,
                IsActive = true
            };

            var id = await _productRepository.InsertAsync(entity);
            return ServiceResult.Ok(id);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> UpdateProductAsync(UpdateSalesProductRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var existing = await _productRepository.GetByIdAsync(request.Id, businessId);
            if (existing == null)
                return ServiceResult.Fail("Product not found.");

            existing.Name = request.Name;
            existing.Description = request.Description;

            await _productRepository.UpdateAsync(existing);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> DeactivateProductAsync(int id)
    {
        try
        {
            await _productRepository.DeactivateAsync(id, _tenantService.CurrentBusinessId);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<SalesProduct?> GetByIdAsync(int id)
    {
        try
        {
            return await _productRepository.GetByIdAsync(id, _tenantService.CurrentBusinessId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<PagedResult<SalesProduct>> GetProductsPagedAsync(string? searchTerm, int page, int pageSize)
    {
        try
        {
            return await _productRepository.GetPagedAsync(searchTerm, page, pageSize, _tenantService.CurrentBusinessId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<SalesProduct>> GetActiveProductsAsync()
    {
        try
        {
            return await _productRepository.GetAllActiveAsync(_tenantService.CurrentBusinessId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
