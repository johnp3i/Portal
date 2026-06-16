namespace Portal.Infrastructure.Services;

/// <summary>
/// Generates a PDF byte array for a given invoice using the Snapshot view.
/// </summary>
public interface IInvoicePdfService
{
    /// <summary>
    /// Renders the invoice snapshot to HTML and converts it to a PDF document.
    /// </summary>
    /// <param name="invoiceId">The invoice identifier.</param>
    /// <param name="cancellationToken">Optional cancellation token (30-second timeout applied by caller).</param>
    /// <returns>PDF file as a byte array.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the invoice is not found.</exception>
    Task<byte[]> GenerateAsync(int invoiceId, CancellationToken cancellationToken = default);
}
