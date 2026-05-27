namespace Portal.Infrastructure.Models;

/// <summary>
/// Input data transfer object for a single line item when creating or updating a credit note.
/// </summary>
public class CreateCreditNoteLineDto
{
    public string Description { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; }
}
