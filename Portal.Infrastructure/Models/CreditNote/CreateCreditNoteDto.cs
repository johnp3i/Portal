namespace Portal.Infrastructure.Models;

/// <summary>
/// Input data transfer object for creating a new credit note.
/// </summary>
public class CreateCreditNoteDto
{
    public int InvoiceId { get; set; }
    public DateOnly IssueDate { get; set; }
    public string Reason { get; set; } = null!;
    public int VatSubmissionPeriodId { get; set; }
    public List<CreateCreditNoteLineDto> Lines { get; set; } = new();
}
