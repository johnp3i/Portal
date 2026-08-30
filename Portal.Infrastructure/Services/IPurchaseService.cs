using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for purchase management including VAT calculation and origin type handling.
/// </summary>
public interface IPurchaseService
{
    Task<List<Purchase>> GetPurchasesAsync();
    Task<List<Purchase>> GetFilteredPurchasesAsync(int? supplierId, int? expenseCategoryId, DateOnly? dateFrom, DateOnly? dateTo);
    Task<Purchase?> GetPurchaseByIdAsync(int id);
    Task<Purchase?> GetMostRecentBySupplierAsync(int supplierId);
    Task<ServiceResult> CreatePurchaseAsync(Purchase purchase);
    Task<ServiceResult> UpdatePurchaseAsync(Purchase purchase);
    Task<ServiceResult> CancelPurchaseAsync(int id);
    Task<ServiceResult> BulkCreatePurchasesAsync(List<Purchase> purchases);
    Task<ServiceResult> AssignPurchasesToPeriodAsync(int businessId, int periodId, List<int> purchaseIds);
    Task<ServiceResult> UnassignPurchasesFromPeriodAsync(int businessId, List<int> purchaseIds);
    Task<List<Purchase>> GetUnassignedForPeriodAsync(int businessId, int periodId);
    Task<int> CountUnassignedForPeriodAsync(int businessId, int periodId);
}
