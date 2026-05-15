namespace Portal.Infrastructure.Entities;

/// <summary>
/// Reference table defining the document lifecycle states of an Invoice.
/// Schema: [invoice].InvoiceStatusType
/// Seed values: Draft (1), Issued (2), Cancelled (3)
/// </summary>
public class InvoiceStatusType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    // Navigation properties
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
