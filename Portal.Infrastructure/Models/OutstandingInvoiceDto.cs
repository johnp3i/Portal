namespace Portal.Infrastructure.Models;

/// <summary>
/// Represents an invoice with outstanding balance for the global payment allocation UI.
/// Used by the manual allocation form and FIFO allocation engine.
/// </summary>
public class OutstandingInvoiceDto
{
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public DateTime InvoiceDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal OutstandingBalance { get; set; }
}
