namespace Portal.Infrastructure.Models;

/// <summary>
/// A recent invoice row for the dashboard table.
/// </summary>
public class RecentInvoiceDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public int InvoiceFinancialStatusTypeId { get; set; }
    public string FinancialStatusName { get; set; } = null!;
    public decimal TotalAmount { get; set; }
}
