namespace Portal.Infrastructure.Models;

/// <summary>
/// Input data transfer object for creating a new credit note.
/// </summary>
public class CreateCreditNoteDto
{
    /// <summary>
    /// The source invoice. NULL for an account-level credit note issued directly to a customer.
    /// When null, <see cref="CustomerId"/> must be supplied instead.
    /// </summary>
    public int? InvoiceId { get; set; }

    /// <summary>
    /// The target customer for an account-level credit note (used only when <see cref="InvoiceId"/>
    /// is null). In invoice mode the customer is derived from the invoice and this is ignored.
    /// </summary>
    public int? CustomerId { get; set; }

    public DateOnly IssueDate { get; set; }
    public string Reason { get; set; } = null!;
    public int VatSubmissionPeriodId { get; set; }
    public List<CreateCreditNoteLineDto> Lines { get; set; } = new();
}
