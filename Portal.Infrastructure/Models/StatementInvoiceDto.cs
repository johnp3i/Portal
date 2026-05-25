namespace Portal.Infrastructure.Models;

/// <summary>
/// Internal query result representing an invoice within the statement period.
/// </summary>
public class StatementInvoiceDto
{
    public int Id { get; set; }
    public DateOnly InvoiceDate { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public decimal TotalAmount { get; set; }
}
