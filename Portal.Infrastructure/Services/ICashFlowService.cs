using Portal.Infrastructure.Models.CashFlow;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Computes cash flow projections on-demand from live Invoice, Payment, Purchase, and Settings data.
/// All queries are scoped to the specified businessId for tenant isolation.
/// </summary>
public interface ICashFlowService
{
    /// <summary>
    /// Computes the full projection for the given horizon, optionally excluding specific invoices.
    /// </summary>
    Task<CashFlowProjectionDto> GetProjectionAsync(int businessId, int daysAhead = 30, int[]? excludedInvoiceIds = null);

    /// <summary>
    /// Returns the current settings for the business, or null if not configured.
    /// </summary>
    Task<CashFlowSettingsDto?> GetSettingsAsync(int businessId);

    /// <summary>
    /// Persists the starting balance and alert threshold for the business.
    /// </summary>
    Task SaveSettingsAsync(int businessId, decimal startingBalance, decimal alertThreshold);

    /// <summary>
    /// Returns compact widget data for the Home Dashboard (30-day projection summary).
    /// </summary>
    Task<CashFlowWidgetDto> GetWidgetDataAsync(int businessId);
}
