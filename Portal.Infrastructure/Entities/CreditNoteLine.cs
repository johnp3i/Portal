namespace Portal.Infrastructure.Entities;

/// <summary>
/// An individual line item within a credit note specifying the description, quantity, unit price, VAT rate, and line total.
/// Schema: [credit].CreditNoteLine
/// </summary>
public class CreditNoteLine
{
    public int Id { get; set; }

    public int CreditNoteId { get; set; }

    public string Description { get; set; } = null!;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal VatRate { get; set; }

    public decimal LineTotal { get; set; }

    public int SortOrder { get; set; }

    // Navigation properties
    public CreditNote CreditNote { get; set; } = null!;
}
