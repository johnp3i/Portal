namespace Portal.Infrastructure.Models;

/// <summary>
/// Model for rendering a credit note PDF document containing business, customer, and credit note details.
/// </summary>
public class CreditNotePdfModel
{
    public string BusinessName { get; set; } = null!;
    public string? BusinessAddress { get; set; }
    public string? BusinessVatNumber { get; set; }
    public string? BusinessLogoUrl { get; set; }
    public string CustomerName { get; set; } = null!;
    public string? CustomerAddress { get; set; }
    public string CreditNoteNumber { get; set; } = null!;
    public DateOnly IssueDate { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public string CurrencySymbol { get; set; } = "€";
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public List<CreditNoteLineDto> Lines { get; set; } = new();
}
