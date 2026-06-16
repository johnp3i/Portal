namespace Portal.Infrastructure.Services;

/// <summary>
/// Renders an invoice into a self-contained HTML snapshot for sharing.
/// </summary>
public interface IInvoiceRenderer
{
    Task<string> RenderAsync(int invoiceId);

    /// <summary>
    /// Renders an invoice using an explicit business ID (for anonymous PDF generation from shared links).
    /// </summary>
    Task<string> RenderAsync(int invoiceId, int businessId);
}
