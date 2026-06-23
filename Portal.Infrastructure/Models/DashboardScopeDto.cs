using Portal.Infrastructure.Constants;

namespace Portal.Infrastructure.Models;

/// <summary>
/// Determines which dashboard sections should be fetched and displayed
/// based on the authenticated user's module permissions.
/// </summary>
public class DashboardScopeDto
{
    public bool ShowRevenue { get; set; }
    public bool ShowInvoice { get; set; }
    public bool ShowQuotation { get; set; }
    public bool ShowPurchase { get; set; }
    public bool ShowVat { get; set; }
    public bool ShowCustomer { get; set; }
    public bool ShowPnlTeaser { get; set; }

    /// <summary>
    /// Returns true if at least one KPI-bearing module is visible.
    /// </summary>
    public bool HasAnyKpiSection =>
        ShowRevenue || ShowInvoice || ShowQuotation || ShowPurchase || ShowVat;

    /// <summary>
    /// Creates a scope where all sections are visible (for privileged users).
    /// </summary>
    public static DashboardScopeDto FullAccess() => new()
    {
        ShowRevenue = true,
        ShowInvoice = true,
        ShowQuotation = true,
        ShowPurchase = true,
        ShowVat = true,
        ShowCustomer = true,
        ShowPnlTeaser = false
    };

    /// <summary>
    /// Creates a scope from a module permissions dictionary.
    /// A module is visible if its access level is not "none".
    /// </summary>
    public static DashboardScopeDto FromPermissions(Dictionary<string, string> permissions)
    {
        bool isVisible(string module) =>
            permissions.TryGetValue(module, out var level)
            && level != AccessLevels.None;

        return new DashboardScopeDto
        {
            ShowRevenue = isVisible(PortalModules.Revenue),
            ShowInvoice = isVisible(PortalModules.Invoice),
            ShowQuotation = isVisible(PortalModules.Quotation),
            ShowPurchase = isVisible(PortalModules.Purchase),
            ShowVat = isVisible(PortalModules.Vat),
            ShowCustomer = isVisible(PortalModules.Customer),
            ShowPnlTeaser = !isVisible(PortalModules.Pnl)
        };
    }
}
