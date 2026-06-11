namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for invoice list display.
/// </summary>
public class InvoiceListDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public DateOnly InvoiceDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string StatusName { get; set; } = null!;
    public string FinancialStatusName { get; set; } = null!;
    public int InvoiceStatusTypeId { get; set; }
    public int InvoiceFinancialStatusTypeId { get; set; }

    /// <summary>
    /// Acceptance status for the invoice's shared link.
    /// Null = no active share, "awaiting" = shared but not accepted, "accepted" = accepted by customer.
    /// </summary>
    public string? AcceptanceStatus { get; set; }
}
