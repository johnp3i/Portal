using Microsoft.Extensions.Logging;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for product catalog management including CRUD, search, KPIs, price history, and auto-population.
/// </summary>
public class ProductService : IProductService
{
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ProductRepository _productRepository;
    private readonly ProductPriceHistoryRepository _priceHistoryRepository;
    private readonly SupplierRepository _supplierRepository;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        ICurrentTenantService currentTenantService,
        ProductRepository productRepository,
        ProductPriceHistoryRepository priceHistoryRepository,
        SupplierRepository supplierRepository,
        ILogger<ProductService> logger)
    {
        _currentTenantService = currentTenantService;
        _productRepository = productRepository;
        _priceHistoryRepository = priceHistoryRepository;
        _supplierRepository = supplierRepository;
        _logger = logger;
    }

    public async Task<ServiceResult> CreateProductAsync(Product product, string userId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;
        if (businessId == 0)
            return ServiceResult.Fail("Business context could not be resolved.");

        // Validate required fields
        if (string.IsNullOrWhiteSpace(product.ProductCode) || string.IsNullOrWhiteSpace(product.Description))
            return ServiceResult.Fail("ProductCode and Description are required.");

        // Validate ProductTypeId is required for new products
        if (!product.ProductTypeId.HasValue)
            throw new ArgumentException("Product Type is required for new products");

        // Validate ProductTypeId is a valid value (Services=1, Goods=2)
        if (product.ProductTypeId != 1 && product.ProductTypeId != 2)
            throw new ArgumentException("Product Type must be Services (1) or Goods (2)");

        // Check duplicate ProductCode for this business
        var existing = await _productRepository.GetByProductCodeAndBusinessIdAsync(product.ProductCode.Trim(), businessId);
        if (existing != null)
            return ServiceResult.Fail("ProductCode already exists for this business.");

        // Validate SupplierId belongs to same business (if provided)
        if (product.SupplierId.HasValue)
        {
            var supplier = await _supplierRepository.GetByIdAndBusinessIdAsync(product.SupplierId.Value, businessId);
            if (supplier == null)
                return ServiceResult.Fail("Supplier not found or does not belong to this business.");
        }

        // Stamp business and defaults
        product.BusinessId = businessId;
        product.IsActive = true;
        product.CreatedAtUtc = DateTime.UtcNow;
        product.ProductCode = product.ProductCode.Trim();
        product.Description = product.Description.Trim();

        var newId = await _productRepository.InsertAsync(product);

        // Insert initial price history record
        await _priceHistoryRepository.InsertAsync(new ProductPriceHistory
        {
            ProductId = newId,
            SellingPrice = product.DefaultSellingPrice,
            CostPrice = product.DefaultCostPrice,
            EffectiveFromUtc = DateTime.UtcNow,
            ChangedByUserId = userId
        });

        return ServiceResult.Ok(newId);
    }

    public async Task<ServiceResult> UpdateProductAsync(Product product, string userId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;
        if (businessId == 0)
            return ServiceResult.Fail("Business context could not be resolved.");

        // Validate required fields
        if (string.IsNullOrWhiteSpace(product.ProductCode) || string.IsNullOrWhiteSpace(product.Description))
            return ServiceResult.Fail("ProductCode and Description are required.");

        // Validate ProductTypeId if provided (allow NULL for legacy products)
        if (product.ProductTypeId.HasValue && product.ProductTypeId != 1 && product.ProductTypeId != 2)
            throw new ArgumentException("Product Type must be Services (1) or Goods (2)");

        // Check product exists and belongs to this business
        var existing = await _productRepository.GetByIdAndBusinessIdAsync(product.Id, businessId);
        if (existing == null)
            return ServiceResult.Fail("Product not found.");

        // Validate SupplierId belongs to same business (if provided)
        if (product.SupplierId.HasValue)
        {
            var supplier = await _supplierRepository.GetByIdAndBusinessIdAsync(product.SupplierId.Value, businessId);
            if (supplier == null)
                return ServiceResult.Fail("Supplier not found or does not belong to this business.");
        }

        // Preserve business ownership
        product.BusinessId = businessId;
        product.ProductCode = product.ProductCode.Trim();
        product.Description = product.Description.Trim();

        // Detect price changes before update
        bool priceChanged = existing.DefaultSellingPrice != product.DefaultSellingPrice
                         || existing.DefaultCostPrice != product.DefaultCostPrice;

        await _productRepository.UpdateAsync(product);

        // Insert price history if prices changed
        if (priceChanged)
        {
            await _priceHistoryRepository.InsertAsync(new ProductPriceHistory
            {
                ProductId = product.Id,
                SellingPrice = product.DefaultSellingPrice,
                CostPrice = product.DefaultCostPrice,
                EffectiveFromUtc = DateTime.UtcNow,
                ChangedByUserId = userId
            });
        }

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeactivateProductAsync(int productId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;
        if (businessId == 0)
            return ServiceResult.Fail("Business context could not be resolved.");

        var existing = await _productRepository.GetByIdAndBusinessIdAsync(productId, businessId);
        if (existing == null)
            return ServiceResult.Fail("Product not found.");

        await _productRepository.DeactivateAsync(productId, businessId);

        return ServiceResult.Ok();
    }

    public async Task<Product?> GetProductByIdAsync(int productId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;
        if (businessId == 0)
            return null;

        return await _productRepository.GetByIdAndBusinessIdAsync(productId, businessId);
    }

    public async Task<PagedResult<Product>> GetProductsPagedAsync(string? searchTerm, int page, int pageSize = 15)
    {
        var businessId = _currentTenantService.CurrentBusinessId;
        if (businessId == 0)
        {
            return new PagedResult<Product>
            {
                Items = new List<Product>(),
                CurrentPage = 1,
                PageSize = pageSize,
                TotalCount = 0
            };
        }

        // Clamp page to minimum 1
        if (page < 1) page = 1;

        // Clamp pageSize to range [1, 100], default 15
        if (pageSize < 1 || pageSize > 100) pageSize = 15;

        int offset = (page - 1) * pageSize;

        var (items, totalCount) = await _productRepository.GetPagedByBusinessIdAsync(
            businessId,
            searchTerm,
            offset,
            pageSize);

        var result = new PagedResult<Product>
        {
            Items = items,
            CurrentPage = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        // If requested page exceeds total pages, clamp to page 1
        if (page > result.TotalPages && result.TotalCount > 0)
        {
            var (clampedItems, _) = await _productRepository.GetPagedByBusinessIdAsync(
                businessId,
                searchTerm,
                0,
                pageSize);

            result.Items = clampedItems;
            result.CurrentPage = 1;
        }

        return result;
    }

    public async Task<ProductKpiDto> GetKpisAsync()
    {
        var businessId = _currentTenantService.CurrentBusinessId;
        if (businessId == 0)
        {
            return new ProductKpiDto
            {
                TotalProducts = 0,
                ActiveProducts = 0,
                AverageSellingPrice = 0,
                BestSellerDescription = null,
                BestSellerUsageCount = 0
            };
        }

        return await _productRepository.GetKpiDataAsync(businessId);
    }

    public async Task<List<ProductUsageDto>> GetTopProductsByUsageAsync(int top = 10)
    {
        var businessId = _currentTenantService.CurrentBusinessId;
        if (businessId == 0)
            return new List<ProductUsageDto>();

        return await _productRepository.GetTopByUsageAsync(businessId, top);
    }

    public async Task AutoPopulateFromLineItemAsync(string? productCode, string description, decimal unitPrice, decimal vatRate, string userId)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            if (businessId == 0)
                return;

            Product? matchedProduct = null;

            // Priority 1: Match by ProductCode (case-insensitive)
            if (!string.IsNullOrWhiteSpace(productCode))
            {
                matchedProduct = await _productRepository.GetByProductCodeAndBusinessIdAsync(productCode.Trim(), businessId);
            }

            // Priority 2: Match by Description (case-insensitive exact match)
            if (matchedProduct == null && !string.IsNullOrWhiteSpace(description))
            {
                matchedProduct = await _productRepository.GetByDescriptionAndBusinessIdAsync(description.Trim(), businessId);
            }

            if (matchedProduct != null)
            {
                // Update LastUsedDate only — do NOT update prices
                matchedProduct.LastUsedDate = DateTime.UtcNow;
                await _productRepository.UpdateAsync(matchedProduct);
                return;
            }

            // No match found — create new product only if ProductCode is present
            if (!string.IsNullOrWhiteSpace(productCode))
            {
                var newProduct = new Product
                {
                    BusinessId = businessId,
                    ProductCode = productCode.Trim(),
                    Description = !string.IsNullOrWhiteSpace(description) ? description.Trim() : productCode.Trim(),
                    DefaultSellingPrice = unitPrice,
                    DefaultCostPrice = 0.00m,
                    DefaultVatRate = vatRate,
                    IsActive = true,
                    LastUsedDate = DateTime.UtcNow,
                    CreatedAtUtc = DateTime.UtcNow
                };

                var newId = await _productRepository.InsertAsync(newProduct);

                // Insert initial price history record
                await _priceHistoryRepository.InsertAsync(new ProductPriceHistory
                {
                    ProductId = newId,
                    SellingPrice = unitPrice,
                    CostPrice = 0.00m,
                    EffectiveFromUtc = DateTime.UtcNow,
                    ChangedByUserId = userId
                });
            }

            // No ProductCode and no match — take no action
        }
        catch (Exception ex)
        {
            // Fire-and-forget: log failure, never throw
            _logger.LogError(ex, "Auto-population failed for ProductCode={ProductCode}, Description={Description}", productCode, description);
        }
    }

    public async Task<List<ProductPriceHistory>> GetPriceHistoryAsync(int productId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;
        if (businessId == 0)
            return new List<ProductPriceHistory>();

        // Verify product belongs to this business before returning history
        var product = await _productRepository.GetByIdAndBusinessIdAsync(productId, businessId);
        if (product == null)
            return new List<ProductPriceHistory>();

        return await _priceHistoryRepository.GetByProductIdAsync(productId);
    }
}
