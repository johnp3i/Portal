using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Computes KPI aggregates, chart data, and summary tables for the revenue dashboard.
/// All queries are scoped to the specified businessId for tenant isolation.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Computes all four KPI card values for the dashboard:
    /// Outstanding Receivables, Overdue Amount, Paid This Month, Partially Paid.
    /// </summary>
    Task<DashboardKpiDto> GetKpiDataAsync(int businessId);

    /// <summary>
    /// Returns monthly revenue collected totals for the last 12 months.
    /// </summary>
    Task<List<MonthlyRevenueDto>> GetRevenueCollectedAsync(int businessId);

    /// <summary>
    /// Returns paired monthly totals of invoiced vs collected for the last 12 months.
    /// </summary>
    Task<List<InvoicedVsCollectedDto>> GetInvoicedVsCollectedAsync(int businessId);

    /// <summary>
    /// Computes the collection rate percentage: amount collected within 30 days of invoice date
    /// divided by total invoiced amount, for invoices issued in the last 12 months.
    /// </summary>
    Task<decimal> GetCollectionRateAsync(int businessId);

    /// <summary>
    /// Returns overdue invoices sorted by days overdue descending, with search and pagination support.
    /// </summary>
    Task<PagedResult<OverdueInvoiceDto>> GetOverdueInvoicesAsync(
        int businessId, string? searchTerm, int page, int pageSize);

    /// <summary>
    /// Returns recent non-voided payments sorted by PaymentDateUtc descending, with search and pagination support.
    /// </summary>
    Task<PagedResult<RecentPaymentDto>> GetRecentPaymentsAsync(
        int businessId, string? searchTerm, int page, int pageSize);

    /// <summary>
    /// Returns expenses total and count for the current calendar month.
    /// </summary>
    Task<ExpensesKpiDto> GetExpensesThisMonthAsync(int businessId);

    /// <summary>
    /// Returns up to 5 upcoming supplier payments (effective due date within the next 14 days,
    /// including overdue), ordered by effective due date ascending.
    /// </summary>
    Task<List<UpcomingSupplierPaymentDto>> GetUpcomingSupplierPaymentsAsync(int businessId);

    /// <summary>
    /// Returns monthly revenue and expense totals for the last 6 months (including current).
    /// </summary>
    Task<List<RevenueVsExpensesDto>> GetRevenueVsExpensesAsync(int businessId);

    /// <summary>
    /// Returns the count of issued invoices grouped by financial status.
    /// </summary>
    Task<InvoiceStatusBreakdownDto> GetInvoiceStatusBreakdownAsync(int businessId);

    /// <summary>
    /// Returns the 5 most recently issued invoices.
    /// </summary>
    Task<List<RecentInvoiceDto>> GetRecentInvoicesAsync(int businessId);

    /// <summary>
    /// Returns VAT summary for the current open period (or most recent period).
    /// </summary>
    Task<VatSummaryDto> GetVatSummaryAsync(int businessId);

    /// <summary>
    /// Returns top 5 customers ranked by total invoiced amount.
    /// </summary>
    Task<List<TopCustomerDto>> GetTopCustomersAsync(int businessId);
}
