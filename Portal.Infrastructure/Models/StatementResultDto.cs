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
    public int InvoiceCount { get; set; }
    public int PaymentCount { get; set; }
    public List<StatementLineDto> Lines { get; set; } = new();
}
