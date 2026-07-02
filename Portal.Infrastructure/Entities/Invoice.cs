namespace Portal.Infrastructure.Entities;

/// <summary>
/// A financial document generated from a Quotation or created independently, representing an obligation to pay.
/// Schema: [invoice].Invoice
/// </summary>
public class Invoice
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int CustomerId { get; set; }

    public int? QuotationId { get; set; }

    public int InvoiceStatusTypeId { get; set; }

    public int InvoiceFinancialStatusTypeId { get; set; }

    public string InvoiceNumber { get; set; } = null!;

    public DateOnly InvoiceDate { get; set; }

    public DateOnly DueDate { get; set; }

    public decimal Subtotal { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public string? Notes { get; set; }

    public bool IsGrandTotalShown { get; set; } = true;

    public bool IsQuotationReferenceShown { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public int? VatSubmissionPeriodId { get; set; }

    public bool IsDisputed { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public Customer Customer { get; set; } = null!;

    public Quotation? Quotation { get; set; }

    public InvoiceStatusType InvoiceStatusType { get; set; } = null!;

    public InvoiceFinancialStatusType InvoiceFinancialStatusType { get; set; } = null!;

    public ICollection<InvoiceLine> InvoiceLines { get; set; } = new List<InvoiceLine>();

    public ICollection<InvoiceSection> InvoiceSections { get; set; } = new List<InvoiceSection>();

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public VatSubmissionPeriod? VatSubmissionPeriod { get; set; }
}
