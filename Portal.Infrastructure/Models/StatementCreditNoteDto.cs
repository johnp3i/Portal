namespace Portal.Infrastructure.Models;

/// <summary>
/// Internal query result representing a credit-note application within the statement period.
/// Each row is a single non-voided application of a credit note against one of the customer's
/// invoices. The applied amount reduces the amount the customer owes, so it appears on the
/// statement as a credit (same side as a payment).
/// </summary>
public class StatementCreditNoteDto
{
    /// <summary>The CreditNoteApplication Id.</summary>
    public int Id { get; set; }

    /// <summary>When the credit was applied (drives its chronological position on the statement).</summary>
    public DateOnly AppliedDate { get; set; }

    /// <summary>The credit note's human-readable number (e.g. CN-1002-00004).</summary>
    public string CreditNoteNumber { get; set; } = string.Empty;

    /// <summary>The invoice number the credit was applied against.</summary>
    public string? InvoiceNumber { get; set; }

    /// <summary>The amount applied — the credit shown on the statement.</summary>
    public decimal AmountApplied { get; set; }
}
