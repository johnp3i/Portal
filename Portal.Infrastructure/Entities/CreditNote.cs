namespace Portal.Infrastructure.Entities;

/// <summary>
/// A financial document issued against a source invoice that formally reduces the amount owed by the customer.
/// Schema: [credit].CreditNote
/// </summary>
public class CreditNote
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    /// <summary>
    /// The source invoice this credit note corrects. NULL for account-level credit notes issued
    /// directly to a customer with no originating invoice (the credit sits on the customer account
    /// and is applied to invoices later via CreditNoteApplication rows).
    /// </summary>
    public int? InvoiceId { get; set; }

    public int CustomerId { get; set; }

    public int CreditNoteStatusTypeId { get; set; }

    public int VatSubmissionPeriodId { get; set; }

    public string CreditNoteNumber { get; set; } = null!;

    public DateOnly IssueDate { get; set; }

    public string Reason { get; set; } = null!;

    public decimal Subtotal { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public DateTime? IssuedAtUtc { get; set; }

    public DateTime? VoidedAtUtc { get; set; }

    public string? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public Invoice? Invoice { get; set; }

    public Customer Customer { get; set; } = null!;

    public CreditNoteStatusType CreditNoteStatusType { get; set; } = null!;

    public VatSubmissionPeriod VatSubmissionPeriod { get; set; } = null!;

    public ICollection<CreditNoteLine> CreditNoteLines { get; set; } = new List<CreditNoteLine>();

    public ICollection<CreditNoteApplication> CreditNoteApplications { get; set; } = new List<CreditNoteApplication>();
}
