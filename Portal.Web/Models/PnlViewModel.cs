using Portal.Infrastructure.Models;

namespace Portal.Web.Models;

/// <summary>
/// View model for the Profit &amp; Loss Index page.
/// </summary>
public class PnlViewModel
{
    public PnlSummaryDto Summary { get; set; } = null!;
    public PnlPeriodType SelectedPeriod { get; set; }
    public string? CustomStartDate { get; set; }
    public string? CustomEndDate { get; set; }
    public string CurrencySymbol { get; set; } = "€";
}
