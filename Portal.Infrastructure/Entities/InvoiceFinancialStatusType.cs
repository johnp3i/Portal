namespace Portal.Infrastructure.Entities;

/// <summary>
/// Reference table defining the financial states of an Invoice.
/// Schema: [invoice].InvoiceFinancialStatusType
/// Seed values: Unpaid (1), PartiallyPaid (2), Paid (3), Overdue (4), WrittenOff (5)
/// </summary>
public class InvoiceFinancialStatusType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    // Navigation properties
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
