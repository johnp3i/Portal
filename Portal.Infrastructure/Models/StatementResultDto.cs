namespace Portal.Infrastructure.Models;

/// <summary>
/// Contains the complete result of a customer statement generation including balances, totals, and line items.
/// </summary>
public class StatementResultDto
{
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal TotalInvoiced { get; set; }
    public decimal TotalPaid { get; set; }
    /// <summary>Total credit-note amount applied within the period (reduces the balance like a payment).</summary>
    public decimal TotalCredited { get; set; }
    public int InvoiceCount { get; set; }
    public int PaymentCount { get; set; }
    public int CreditNoteCount { get; set; }
    public List<StatementLineDto> Lines { get; set; } = new();
}
