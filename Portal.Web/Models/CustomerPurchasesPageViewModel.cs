using Portal.Infrastructure.Models;

namespace Portal.Web.Models;

/// <summary>View model for the Customer Purchases dashboard page.</summary>
public class CustomerPurchasesPageViewModel
{
    public PurchasesDashboardDto Dashboard { get; set; } = null!;

    /// <summary>KPI ids collapsed according to the cp_kpi_collapsed cookie.</summary>
    public HashSet<string> CollapsedKpiIds { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Feature keys the current user has already registered interest in.</summary>
    public List<string> RegisteredFeatureKeys { get; set; } = new();

    /// <summary>True when an invalid period was requested and the default was used instead.</summary>
    public bool ShowPeriodFallbackWarning { get; set; }
}
