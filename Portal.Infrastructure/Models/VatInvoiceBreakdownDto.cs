namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for a single invoice row in the VAT submission detail breakdown.
/// </summary>
public class VatInvoiceBreakdownDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public DateOnly InvoiceDate { get; set; }

    /// <summary>
    /// True when the invoice was explicitly assigned to this period via VatSubmissionPeriodId.
    /// False when it was included via date-range fallback.
    /// </summary>
    public bool IsExplicitAssignment { get; set; }

    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
}
