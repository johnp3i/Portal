namespace Portal.Infrastructure.Entities;

/// <summary>
/// A record tracking when and how a credit note amount was applied against the source invoice's outstanding balance.
/// Schema: [credit].CreditNoteApplication
/// </summary>
public class CreditNoteApplication
{
    public int Id { get; set; }

    public int CreditNoteId { get; set; }

    public int InvoiceId { get; set; }

    public decimal AmountApplied { get; set; }

    public DateTime AppliedAtUtc { get; set; }

    public string? AppliedByUserId { get; set; }

    public bool IsVoided { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public CreditNote CreditNote { get; set; } = null!;

    public Invoice Invoice { get; set; } = null!;
}
