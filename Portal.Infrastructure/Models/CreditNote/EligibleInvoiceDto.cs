namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object representing an invoice eligible for credit note creation.
/// Only invoices in Issued status with outstanding balance greater than zero are eligible.
/// </summary>
public class EligibleInvoiceDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public int CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal OutstandingBalance { get; set; }
}
