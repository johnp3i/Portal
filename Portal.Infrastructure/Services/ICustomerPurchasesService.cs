using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Customer Purchases Dashboard: read-only purchase analytics for one customer plus
/// "coming later" feature interest registration.
/// </summary>
public interface ICustomerPurchasesService
{
    /// <summary>Null when the customer is not found for the current tenant (or there is no tenant).</summary>
    Task<PurchasesDashboardDto?> GetDashboardAsync(int customerId, PurchasesQuery query);

    /// <summary>Null when the customer is not found for the current tenant. An invalid product key yields an empty result.</summary>
    Task<ProductPurchaseLinesDto?> GetProductPurchaseLinesAsync(int customerId, PurchasesQuery query, string productKey);

    Task<List<string>> GetRegisteredFeatureKeysAsync(string userId);

    Task<ServiceResult> RegisterFeatureInterestAsync(string userId, string? featureKey);
}
