using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Handles soft-deletion of invoices and quotations.
/// Only Draft status documents are eligible for soft-delete.
/// </summary>
public interface IDocumentSoftDeleteService
{
    /// <summary>
    /// Soft-deletes a Draft invoice by setting IsDeleted = 1.
    /// Returns a result indicating success or failure with an error message.
    /// </summary>
    Task<ServiceResult> SoftDeleteInvoiceAsync(int invoiceId);

    /// <summary>
    /// Soft-deletes a Draft quotation by setting IsDeleted = 1.
    /// Returns a result indicating success or failure with an error message.
    /// </summary>
    Task<ServiceResult> SoftDeleteQuotationAsync(int quotationId);
}
