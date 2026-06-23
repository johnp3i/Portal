namespace Portal.Infrastructure.Models;

/// <summary>
/// Model for rendering the P&amp;L PDF export view.
/// </summary>
public class PnlPdfModel
{
    public string BusinessName { get; set; } = null!;
    public string CurrencySymbol { get; set; } = "€";
    public PnlSummaryDto Summary { get; set; } = null!;
}
