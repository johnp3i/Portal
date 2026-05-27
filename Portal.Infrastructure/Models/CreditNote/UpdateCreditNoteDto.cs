namespace Portal.Infrastructure.Models;

/// <summary>
/// Input data transfer object for updating an existing credit note (Draft status only).
/// </summary>
public class UpdateCreditNoteDto
{
    public DateOnly IssueDate { get; set; }
    public string Reason { get; set; } = null!;
    public int VatSubmissionPeriodId { get; set; }
    public List<CreateCreditNoteLineDto> Lines { get; set; } = new();
}
