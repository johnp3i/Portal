namespace Portal.Infrastructure.Services;

/// <summary>
/// Renders an invoice into a self-contained HTML snapshot for sharing.
/// </summary>
public interface IInvoiceRenderer
{
    Task<string> RenderAsync(int invoiceId);
}
