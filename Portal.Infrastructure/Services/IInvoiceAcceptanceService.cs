using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

public interface IInvoiceAcceptanceService
{
    /// <summary>
    /// Records an acceptance for the given share token.
    /// Validates share is active and non-expired.
    /// Returns the acceptance result indicating success, failure, or already-accepted.
    /// </summary>
    Task<InvoiceAcceptanceResult> AcceptAsync(string shareToken, string ipAddress, string userAgent);

    /// <summary>
    /// Gets the acceptance record for a given InvoiceShare ID, or null if not yet accepted.
    /// </summary>
    Task<InvoiceAcceptance?> GetByInvoiceShareIdAsync(int invoiceShareId);

    /// <summary>
    /// Returns the set of InvoiceShareIds (from the provided list) that have been accepted.
    /// Used for batch-loading acceptance status on list pages.
    /// </summary>
    Task<HashSet<int>> GetAcceptedShareIdsAsync(IEnumerable<int> shareIds);
}
