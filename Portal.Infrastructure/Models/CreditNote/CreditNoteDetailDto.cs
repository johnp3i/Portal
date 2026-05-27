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
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
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
}
