namespace Portal.Infrastructure.Entities;

/// <summary>
/// A commercial proposal document containing priced line items sent to a Customer.
/// Schema: [quotation].Quotation
/// </summary>
public class Quotation
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int CustomerId { get; set; }

    public int QuotationStatusTypeId { get; set; }

    public string Reference { get; set; } = null!;

    public DateOnly? ValidUntil { get; set; }

    public decimal Subtotal { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public int? QuotationContactId { get; set; }

    public bool IsGrandTotalShown { get; set; } = true;

    // Navigation properties
    public Business Business { get; set; } = null!;

    public Customer Customer { get; set; } = null!;

    public QuotationStatusType QuotationStatusType { get; set; } = null!;

    public QuotationContact? QuotationContact { get; set; }

    public ICollection<QuotationLine> QuotationLines { get; set; } = new List<QuotationLine>();

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
