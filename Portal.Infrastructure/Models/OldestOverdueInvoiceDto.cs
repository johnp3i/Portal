namespace Portal.Infrastructure.Models;

/// <summary>
/// The single oldest overdue invoice for a business (by due date), used in the weekly
/// Financial Snapshot's "needs your attention" section. Null when nothing is overdue.
/// </summary>
public class OldestOverdueInvoiceDto
{
    public string InvoiceNumber { get; set; } = null!;
    public DateOnly DueDate { get; set; }
    public decimal Outstanding { get; set; }
    public int DaysOverdue { get; set; }
}
