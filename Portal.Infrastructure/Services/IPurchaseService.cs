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

    /// <summary>
    /// One page of filtered/sorted purchases for the list view, with Supplier/ExpenseCategory names
    /// resolved for the page (including inactive ones). All filtering, search, sort and paging run
    /// in SQL, so only the requested page is loaded.
    /// </summary>
    Task<PagedResult<Purchase>> GetPagedListAsync(PurchaseListFilter filter, int page, int pageSize);

    /// <summary>Aggregate KPIs over the FULL filtered set (independent of the current page).</summary>
    Task<PurchaseListAggregates> GetListAggregatesAsync(PurchaseListFilter filter);

    /// <summary>
    /// Expense-trend time series over the same filtered set as the list/KPIs, grouped by the chosen
    /// dimension ("category" | "supplier"). Buckets (daily/monthly/yearly) are derived from the
    /// effective date range; empty buckets are zero-filled; only the top series by spend get their
    /// own line and the rest are folded into "Other". The summed result reconciles with
    /// <see cref="GetListAggregatesAsync"/>.TotalSpend for the same filter.
    /// </summary>
    Task<ExpenseTrendResult> GetExpenseTrendAsync(PurchaseListFilter filter, string dimension);

    /// <summary>All purchases matching the filter (no paging) — used by CSV/PDF export.</summary>
    Task<List<Purchase>> GetFilteredForExportAsync(PurchaseListFilter filter);

    /// <summary>Supplier names (incl. inactive) for the given ids — so every list row shows a name.</summary>
    Task<Dictionary<int, string>> GetSupplierNamesAsync(IEnumerable<int> supplierIds);

    /// <summary>Expense-category names (incl. inactive) for the given ids.</summary>
    Task<Dictionary<int, string>> GetExpenseCategoryNamesAsync(IEnumerable<int> categoryIds);
    Task<Purchase?> GetPurchaseByIdAsync(int id);
    Task<Purchase?> GetMostRecentBySupplierAsync(int supplierId);
    Task<ServiceResult> CreatePurchaseAsync(Purchase purchase);
    Task<ServiceResult> UpdatePurchaseAsync(Purchase purchase);
    Task<ServiceResult> CancelPurchaseAsync(int id);
    Task<ServiceResult> SetPurchasePaidStateAsync(int id, bool isPaid);
    Task<ServiceResult> BulkCreatePurchasesAsync(List<Purchase> purchases);
    Task<ServiceResult> AssignPurchasesToPeriodAsync(int businessId, int periodId, List<int> purchaseIds);
    Task<ServiceResult> UnassignPurchasesFromPeriodAsync(int businessId, List<int> purchaseIds);
    Task<List<Purchase>> GetUnassignedForPeriodAsync(int businessId, int periodId);
    Task<int> CountUnassignedForPeriodAsync(int businessId, int periodId);
}
