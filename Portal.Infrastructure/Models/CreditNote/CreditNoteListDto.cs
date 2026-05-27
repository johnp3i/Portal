namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for credit note list display.
/// </summary>
public class CreditNoteListDto
{
    public int Id { get; set; }
    public string CreditNoteNumber { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public string InvoiceNumber { get; set; } = null!;
    public DateOnly IssueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public int CreditNoteStatusTypeId { get; set; }
    public string StatusName { get; set; } = null!;
    public string Reason { get; set; } = null!;
}
