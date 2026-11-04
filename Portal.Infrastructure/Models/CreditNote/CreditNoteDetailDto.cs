namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for credit note detail view, including lines and applications.
/// </summary>
public class CreditNoteDetailDto
{
    public int Id { get; set; }
    public string CreditNoteNumber { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public int CustomerId { get; set; }
    /// <summary>Source invoice id, or null for an account-level credit note.</summary>
    public int? InvoiceId { get; set; }
    /// <summary>Source invoice number, or null for an account-level credit note.</summary>
    public string? InvoiceNumber { get; set; }
    public DateOnly IssueDate { get; set; }
    public string Reason { get; set; } = null!;
    public int CreditNoteStatusTypeId { get; set; }
    public string StatusName { get; set; } = null!;
    public string VatPeriodLabel { get; set; } = null!;
    public int VatSubmissionPeriodId { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime? IssuedAtUtc { get; set; }
    public DateTime? VoidedAtUtc { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public List<CreditNoteLineDto> Lines { get; set; } = new();
    public List<CreditNoteApplicationDto> Applications { get; set; } = new();

    /// <summary>True when the credit note has no source invoice (issued to the customer account).</summary>
    public bool IsAccountLevel => InvoiceId == null;

    /// <summary>
    /// For account-level notes: the still-unapplied portion (TotalAmount − sum of non-voided
    /// applications). Equals TotalAmount for an issued account note with no applications yet, and 0
    /// once fully drawn down. Not meaningful for invoice-linked notes (single-shot application).
    /// </summary>
    public decimal RemainingAmount { get; set; }
}
