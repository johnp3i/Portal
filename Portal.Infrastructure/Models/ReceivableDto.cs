namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for a receivable invoice row in the receivables list view.
/// </summary>
public class ReceivableDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public DateOnly InvoiceDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal OutstandingBalance { get; set; }
    public int InvoiceFinancialStatusTypeId { get; set; }
    public string FinancialStatusName { get; set; } = null!;
    public bool HasOutstandingBalance { get; set; }
}
