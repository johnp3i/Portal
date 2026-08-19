using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for product price tier management including CRUD, default tier switching,
/// deactivation/reactivation, and multi-tenant scoped queries.
/// </summary>
public class ProductPriceTierService : IProductPriceTierService
{
    private readonly ProductPriceTierRepository _tierRepository;
    private readonly ProductRepository _productRepository;
    private readonly ProductPriceHistoryRepository _priceHistoryRepository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly PortalDbContext _dbContext;
    private readonly ILogger<ProductPriceTierService> _logger;

    public ProductPriceTierService(
        ProductPriceTierRepository tierRepository,
        ProductRepository productRepository,
        ProductPriceHistoryRepository priceHistoryRepository,
        ICurrentTenantService currentTenantService,
        PortalDbContext dbContext,
        ILogger<ProductPriceTierService> logger)
    {
        _tierRepository = tierRepository;
        _productRepository = productRepository;
        _priceHistoryRepository = priceHistoryRepository;
        _currentTenantService = currentTenantService;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CreateTierAsync(CreateTierRequest request, int businessId, string userId)
    {
        try
        {
            // Validate TierName
            if (string.IsNullOrWhiteSpace(request.TierName))
                return ServiceResult.Fail("Tier name is required.");

            var trimmedName = request.TierName.Trim();
            if (trimmedName.Length > 100)
                return ServiceResult.Fail("Tier name must be 100 characters or fewer.");

            // Validate prices
            if (request.SellingPrice < 0 || request.CostPrice < 0)
                return ServiceResult.Fail("Price values must be zero or greater.");

            // Verify ProductId belongs to authenticated user's BusinessId
            var product = await _productRepository.GetByIdAndBusinessIdAsync(request.ProductId, businessId);
            if (product == null)
                return ServiceResult.Fail("Product not found.");

            // Enforce max 20 active tiers per product
            var activeCount = await _tierRepository.GetActiveCountAsync(request.ProductId);
            if (activeCount >= 20)
                return ServiceResult.Fail("Maximum of 20 price tiers per product reached.");

            // Enforce tier name uniqueness among active tiers for the product
            var activeTiers = await _tierRepository.GetActiveByProductIdAsync(request.ProductId);
            if (activeTiers.Any(t => string.Equals(t.TierName, trimmedName, StringComparison.OrdinalIgnoreCase)))
                return ServiceResult.Fail("A tier with this name already exists for this product.");

            // Determine if this is the first tier for the product (auto-set IsDefault)
            var isFirstTier = activeCount == 0;
            var isDefault = isFirstTier || request.IsDefault;

            var now = DateTime.UtcNow;
            var tier = new ProductPriceTier
            {
                ProductId = request.ProductId,
                TierName = trimmedName,
                SellingPrice = request.SellingPrice,
                CostPrice = request.CostPrice,
                IsDefault = isDefault,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            // Use a transaction to ensure atomicity
            using var transaction = await _dbContext.Database.BeginTransactionAsync();

            // If IsDefault = true and there's an existing default, clear it
            if (isDefault && !isFirstTier)
            {
                var currentDefault = activeTiers.FirstOrDefault(t => t.IsDefault);
                if (currentDefault != null)
                {
                    await _tierRepository.SetDefaultFlagAsync(currentDefault.Id, false);
                }
            }

            // Insert the tier
            var insertedId = await _tierRepository.InsertAsync(tier);

            // Insert initial price history record
            await _priceHistoryRepository.InsertTierPriceHistoryAsync(
                request.ProductId, insertedId, request.SellingPrice, request.CostPrice, userId);

            // If new tier is default: sync Product.DefaultSellingPrice and DefaultCostPrice
            if (isDefault)
            {
                await _productRepository.UpdateDefaultPricesAsync(request.ProductId, request.SellingPrice, request.CostPrice);
            }

            // Commit transaction
            await transaction.CommitAsync();

            return ServiceResult.Ok(insertedId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateTierAsync(UpdateTierRequest request, int businessId, string userId)
    {
        try
        {
            // Validate TierName (trim before length check for consistency)
            if (string.IsNullOrWhiteSpace(request.TierName))
                return ServiceResult.Fail("Tier name is required.");

            var trimmedName = request.TierName.Trim();
            if (trimmedName.Length > 100)
                return ServiceResult.Fail("Tier name must be 100 characters or fewer.");

            // Validate prices
            if (request.SellingPrice < 0 || request.CostPrice < 0)
                return ServiceResult.Fail("Price values must be zero or greater.");

            // Load the tier and verify it exists
            var tier = await _tierRepository.GetByIdAsync(request.TierId);
            if (tier == null)
                return ServiceResult.Fail("Price tier not found.");

            // Verify request.ProductId matches the tier's actual ProductId
            if (tier.ProductId != request.ProductId)
                return ServiceResult.Fail("Price tier not found.");

            // Verify the product belongs to the business
            var product = await _productRepository.GetByIdAndBusinessIdAsync(tier.ProductId, businessId);
            if (product == null)
                return ServiceResult.Fail("Product not found.");

            // Enforce tier name uniqueness among active tiers (excluding current tier)
            var activeTiers = await _tierRepository.GetActiveByProductIdAsync(tier.ProductId);
            var duplicateName = activeTiers.Any(t => t.Id != request.TierId
                && string.Equals(t.TierName, trimmedName, StringComparison.OrdinalIgnoreCase));
            if (duplicateName)
                return ServiceResult.Fail("A tier with this name already exists for this product.");

            // Update tier record
            tier.TierName = trimmedName;
            tier.SellingPrice = request.SellingPrice;
            tier.CostPrice = request.CostPrice;
            tier.UpdatedAtUtc = DateTime.UtcNow;

            // Use a transaction to ensure atomicity (update + history + default sync)
            using var transaction = await _dbContext.Database.BeginTransactionAsync();

            await _tierRepository.UpdateAsync(tier);

            // Insert price history record
            await _priceHistoryRepository.InsertTierPriceHistoryAsync(
                tier.ProductId,
                tier.Id,
                tier.SellingPrice,
                tier.CostPrice,
                userId);

            // If tier is default, sync Product.DefaultSellingPrice and DefaultCostPrice
            if (tier.IsDefault)
            {
                await _productRepository.UpdateDefaultPricesAsync(tier.ProductId, tier.SellingPrice, tier.CostPrice);
            }

            await transaction.CommitAsync();

            return ServiceResult.Ok(tier.Id);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> SetDefaultTierAsync(int tierId, int productId, int businessId)
    {
        try
        {
            // Load the tier by tierId
            var tier = await _tierRepository.GetByIdAsync(tierId);
            if (tier == null)
                return ServiceResult.Fail("Price tier not found.");

            // Verify productId matches the tier's ProductId
            if (tier.ProductId != productId)
                return ServiceResult.Fail("Price tier not found.");

            // Verify the product belongs to the authenticated user's BusinessId
            var product = await _productRepository.GetByIdAndBusinessIdAsync(productId, businessId);
            if (product == null)
                return ServiceResult.Fail("Product not found.");

            // Verify the tier is active
            if (!tier.IsActive)
                return ServiceResult.Fail("Cannot set an inactive tier as the default.");

            // If tier is already the default, no-op — return success
            if (tier.IsDefault)
                return ServiceResult.Ok();

            // All within a single transaction
            using var transaction = await _dbContext.Database.BeginTransactionAsync();

            // Find the current default tier and clear its IsDefault flag
            var activeTiers = await _tierRepository.GetActiveByProductIdAsync(productId);
            var currentDefault = activeTiers.FirstOrDefault(t => t.IsDefault);
            if (currentDefault != null)
            {
                await _tierRepository.SetDefaultFlagAsync(currentDefault.Id, false);
            }

            // Set IsDefault on the new tier
            await _tierRepository.SetDefaultFlagAsync(tierId, true);

            // Sync Product.DefaultSellingPrice and DefaultCostPrice to new default tier's values
            await _productRepository.UpdateDefaultPricesAsync(productId, tier.SellingPrice, tier.CostPrice);

            // Commit transaction
            await transaction.CommitAsync();

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeactivateTierAsync(int tierId, int productId, int businessId)
    {
        try
        {
            // Load tier by tierId and verify it exists
            var tier = await _tierRepository.GetByIdAsync(tierId);
            if (tier == null || tier.ProductId != productId)
                return ServiceResult.Fail("Price tier not found.");

            // Verify product belongs to authenticated user's BusinessId
            var product = await _productRepository.GetByIdAndBusinessIdAsync(productId, businessId);
            if (product == null)
                return ServiceResult.Fail("Product not found.");

            // Reject if tier is the default tier
            if (tier.IsDefault)
                return ServiceResult.Fail("Cannot deactivate the default tier. Set another tier as default first.");

            // Set IsActive = false, update UpdatedAtUtc
            await _tierRepository.DeactivateAsync(tierId);

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ReactivateTierAsync(int tierId, int productId, int businessId)
    {
        try
        {
            // Load tier and verify it exists
            var tier = await _tierRepository.GetByIdAsync(tierId);
            if (tier == null || tier.ProductId != productId)
                return ServiceResult.Fail("Price tier not found.");

            // Verify product belongs to the authenticated user's BusinessId
            var product = await _productRepository.GetByIdAndBusinessIdAsync(productId, businessId);
            if (product == null)
                return ServiceResult.Fail("Product not found.");

            // Reject if active tier count would exceed 20
            var activeCount = await _tierRepository.GetActiveCountAsync(productId);
            if (activeCount >= 20)
                return ServiceResult.Fail("Maximum of 20 price tiers per product reached. Deactivate another tier first.");

            // Reject if tier name conflicts with existing active tier
            var activeTiers = await _tierRepository.GetActiveByProductIdAsync(productId);
            var nameConflict = activeTiers.Any(t => t.TierName.Equals(tier.TierName, StringComparison.OrdinalIgnoreCase));
            if (nameConflict)
                return ServiceResult.Fail("An active tier with this name already exists. Rename the conflicting tier first.");

            // Reactivate the tier
            await _tierRepository.ReactivateAsync(tierId);

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ProductPriceTier>> GetTiersForProductAsync(int productId, int businessId)
    {
        try
        {
            // Verify product belongs to the authenticated user's BusinessId
            var product = await _productRepository.GetByIdAndBusinessIdAsync(productId, businessId);
            if (product == null)
                return new List<ProductPriceTier>();

            return await _tierRepository.GetByProductIdAsync(productId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ProductPriceTier>> GetActiveTiersForProductAsync(int productId, int businessId)
    {
        try
        {
            // Verify product belongs to the authenticated user's BusinessId
            var product = await _productRepository.GetByIdAndBusinessIdAsync(productId, businessId);
            if (product == null)
                return new List<ProductPriceTier>();

            return await _tierRepository.GetActiveByProductIdAsync(productId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ProductPriceTier?> GetTierByIdAsync(int tierId, int productId, int businessId)
    {
        try
        {
            // Verify product belongs to the authenticated user's BusinessId
            var product = await _productRepository.GetByIdAndBusinessIdAsync(productId, businessId);
            if (product == null)
                return null;

            var tier = await _tierRepository.GetByIdAsync(tierId);
            if (tier == null || tier.ProductId != productId)
                return null;

            return tier;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Dictionary<int, int>> GetActiveTierCountsAsync(IEnumerable<int> productIds)
    {
        try
        {
            return await _tierRepository.GetActiveCountsByProductIdsAsync(productIds);
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
