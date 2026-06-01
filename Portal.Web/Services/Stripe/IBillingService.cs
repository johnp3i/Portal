using Portal.Infrastructure.Models;
using Portal.Web.Models.Stripe;

namespace Portal.Web.Services.Stripe;

/// <summary>
/// Provides billing history, invoice retrieval, and PDF invoice generation
/// for business owners viewing their payment records.
/// </summary>
public interface IBillingService
{
    /// <summary>
    /// Gets the billing overview for a business including current subscription status,
    /// plan name, period dates, and summary statistics.
    /// </summary>
    Task<BillingOverviewModel> GetBillingOverviewAsync(int businessId);

    /// <summary>
    /// Gets a paginated list of billing invoices for a business,
    /// ordered by PaidAtUtc descending (most recent first).
    /// </summary>
    Task<PagedResult<BillingInvoiceModel>> GetInvoicesAsync(int businessId, int page, int pageSize);

    /// <summary>
    /// Generates a PDF document for the specified invoice.
    /// Returns the PDF as a byte array for download.
    /// </summary>
    Task<byte[]> GenerateInvoicePdfAsync(int invoiceId, int businessId);
}
