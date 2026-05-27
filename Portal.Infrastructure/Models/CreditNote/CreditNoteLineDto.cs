namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for a credit note line item.
/// </summary>
public class CreditNoteLineDto
{
    public int Id { get; set; }
    public string Description { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; }
    public decimal LineTotal { get; set; }
}
