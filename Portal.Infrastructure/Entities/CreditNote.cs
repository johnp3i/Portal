namespace Portal.Infrastructure.Entities;

/// <summary>
/// A financial document issued against a source invoice that formally reduces the amount owed by the customer.
/// Schema: [credit].CreditNote
/// </summary>
public class CreditNote
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int InvoiceId { get; set; }

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

    public Invoice Invoice { get; set; } = null!;

    public Customer Customer { get; set; } = null!;

    public CreditNoteStatusType CreditNoteStatusType { get; set; } = null!;

    public VatSubmissionPeriod VatSubmissionPeriod { get; set; } = null!;

    public ICollection<CreditNoteLine> CreditNoteLines { get; set; } = new List<CreditNoteLine>();

    public ICollection<CreditNoteApplication> CreditNoteApplications { get; set; } = new List<CreditNoteApplication>();
}
