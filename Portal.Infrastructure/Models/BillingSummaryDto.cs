namespace Portal.Infrastructure.Models;

/// <summary>
/// Summary aggregation of billing invoices for a business.
/// Used by the billing history page to display total paid, invoice count, and last payment date.
/// </summary>
public class BillingSummaryDto
{
    public decimal TotalPaid { get; set; }

    public int InvoiceCount { get; set; }

    public DateTime? LastPaymentDate { get; set; }
}
