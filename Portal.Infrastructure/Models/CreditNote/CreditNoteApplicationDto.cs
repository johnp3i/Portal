namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for a credit note application record.
/// </summary>
public class CreditNoteApplicationDto
{
    public int Id { get; set; }
    public DateTime AppliedAtUtc { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public int InvoiceId { get; set; }
    public decimal AmountApplied { get; set; }
    public string AppliedByUserId { get; set; } = null!;
    public bool IsVoided { get; set; }
}
