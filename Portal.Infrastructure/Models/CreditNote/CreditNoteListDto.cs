namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for credit note list display.
/// </summary>
public class CreditNoteListDto
{
    public int Id { get; set; }
    public string CreditNoteNumber { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    /// <summary>Source invoice number, or null for account-level credit notes.</summary>
    public string? InvoiceNumber { get; set; }
    public DateOnly IssueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public int CreditNoteStatusTypeId { get; set; }
    public string StatusName { get; set; } = null!;
    public string Reason { get; set; } = null!;
    /// <summary>True when the credit note has no source invoice (issued to the customer account).</summary>
    public bool IsAccountLevel => string.IsNullOrEmpty(InvoiceNumber);
}
