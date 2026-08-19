using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Service interface for product price tier management operations.
/// Handles creation, updating, default tier switching, deactivation/reactivation, and queries.
/// </summary>
public interface IProductPriceTierService
{
    // Tier CRUD
    Task<ServiceResult> CreateTierAsync(CreateTierRequest request, int businessId, string userId);
    Task<ServiceResult> UpdateTierAsync(UpdateTierRequest request, int businessId, string userId);
    Task<ServiceResult> SetDefaultTierAsync(int tierId, int productId, int businessId);
    Task<ServiceResult> DeactivateTierAsync(int tierId, int productId, int businessId);
    Task<ServiceResult> ReactivateTierAsync(int tierId, int productId, int businessId);

    // Queries
    Task<List<ProductPriceTier>> GetTiersForProductAsync(int productId, int businessId);
    Task<List<ProductPriceTier>> GetActiveTiersForProductAsync(int productId, int businessId);
    Task<ProductPriceTier?> GetTierByIdAsync(int tierId, int productId, int businessId);

    /// <summary>
    /// Returns a map of ProductId -> active tier count for the given products.
    /// Products with no active tiers are omitted. Used for list/table indicators.
    /// </summary>
    Task<Dictionary<int, int>> GetActiveTierCountsAsync(IEnumerable<int> productIds);
}
