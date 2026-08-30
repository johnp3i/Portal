namespace Portal.Infrastructure.Models;

/// <summary>
/// Result of the VAT period pre-submission checklist — an advisory, non-blocking set of
/// automated checks shown on the VAT period review page before the period is filed.
/// </summary>
public class VatPreSubmissionChecklistDto
{
    /// <summary>True when the underlying VAT submission is already marked as filed.</summary>
    public bool IsSubmitted { get; set; }

    /// <summary>Number of items with a Warning status. Info items do not count.</summary>
    public int WarningCount { get; set; }

    /// <summary>True when there are no Warning items to review.</summary>
    public bool AllClear => WarningCount == 0;

    public string CurrencySymbol { get; set; } = "€";

    public List<VatChecklistItemDto> Items { get; set; } = new();
}

/// <summary>
/// A single checklist line: a stable key, a status discriminator, and human-readable text.
/// </summary>
public class VatChecklistItemDto
{
    /// <summary>Stable identifier, e.g. "unassigned_purchases".</summary>
    public string Key { get; set; } = null!;

    /// <summary>Status discriminator: "pass", "warning", or "info".</summary>
    public string Status { get; set; } = null!;

    public string Title { get; set; } = null!;

    /// <summary>Pre-formatted, human-readable detail message.</summary>
    public string Detail { get; set; } = null!;
}
