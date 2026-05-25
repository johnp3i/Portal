namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for an overdue invoice row displayed on the revenue dashboard.
/// </summary>
public class OverdueInvoiceDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public DateOnly DueDate { get; set; }
    public int DaysOverdue { get; set; }
    public decimal OutstandingBalance { get; set; }
}
