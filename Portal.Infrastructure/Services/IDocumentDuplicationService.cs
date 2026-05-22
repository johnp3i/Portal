using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Handles duplication of invoices and quotations into new Draft documents.
/// </summary>
public interface IDocumentDuplicationService
{
    /// <summary>
    /// Duplicates an existing invoice, creating a new Draft invoice with fresh dates,
    /// a new sequential number, and all sections/lines copied.
    /// </summary>
    Task<Invoice> DuplicateInvoiceAsync(int sourceInvoiceId, string userId);

    /// <summary>
    /// Duplicates an existing quotation, creating a new Draft quotation with a fresh
    /// validity period, a new sequential reference, and all sections/lines copied.
    /// </summary>
    Task<Quotation> DuplicateQuotationAsync(int sourceQuotationId, string userId);
}
