namespace Portal.Infrastructure.Models;

/// <summary>
/// A single non-voided credit note applied against an invoice, for display on the
/// invoice detail view's Payment Status section. Lets the invoice reference the credit
/// note(s) that reduced its outstanding balance.
/// </summary>
public class AppliedCreditNoteDto
{
    /// <summary>The credit note's Id (for linking to its detail page).</summary>
    public int CreditNoteId { get; set; }

    /// <summary>The human-readable credit note number, e.g. CN-2026-0001.</summary>
    public string CreditNoteNumber { get; set; } = null!;

    /// <summary>The amount of this credit note applied to the invoice.</summary>
    public decimal AmountApplied { get; set; }

    /// <summary>When the credit was applied.</summary>
    public DateTime AppliedAtUtc { get; set; }
}
