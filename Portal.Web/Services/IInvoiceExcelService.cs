namespace Portal.Web.Services;

/// <summary>
/// Generates an .xlsx workbook for a single invoice. Unlike the PDF, the Excel export is
/// pagination-free: all line items and totals live in one worksheet that the recipient can
/// re-sort, re-total, or import into their own systems. This sidesteps the page-splitting
/// problems some customers hit when printing the PDF.
/// </summary>
public interface IInvoiceExcelService
{
    /// <summary>
    /// Builds the invoice workbook for the current tenant and returns the raw .xlsx bytes.
    /// </summary>
    Task<byte[]> GenerateAsync(int invoiceId, CancellationToken cancellationToken = default);
}
