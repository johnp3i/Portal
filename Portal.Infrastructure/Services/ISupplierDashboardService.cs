using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Analytics service for the Supplier Dashboard page.
/// Computes spend metrics, chart data, and paginated purchase history for a single supplier.
/// </summary>
public interface ISupplierDashboardService
{
    /// <summary>
    /// Computes all dashboard metrics for a supplier, optionally scoped to a VAT period.
    /// </summary>
    /// <param name="supplierId">The ID of the supplier to compute metrics for.</param>
    /// <param name="periodId">Optional VAT submission period ID to scope the data. Pass <c>null</c> for all-time data.</param>
    /// <param name="page">The 1-based page number for the purchases table.</param>
    /// <param name="description">Optional description substring filter for the purchases table.</param>
    /// <param name="categoryId">Optional expense category ID filter for the purchases table.</param>
    /// <param name="dateFrom">Optional start date (inclusive) filter for the purchases table.</param>
    /// <param name="dateTo">Optional end date (inclusive) filter for the purchases table.</param>
    /// <returns>A fully populated <see cref="SupplierDashboardViewModel"/> ready for the view.</returns>
    Task<SupplierDashboardViewModel> GetDashboardAsync(
        int supplierId,
        int? periodId,
        int page,
        string? description = null,
        int? categoryId = null,
        DateOnly? dateFrom = null,
        DateOnly? dateTo = null);
}
