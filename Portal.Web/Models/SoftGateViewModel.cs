namespace Portal.Web.Models;

/// <summary>
/// View model for soft-gate and access-denied views displayed when a user lacks the required
/// plan or permission to access a module.
/// </summary>
public class SoftGateViewModel
{
    /// <summary>
    /// The module key (e.g., "cashflow", "pnl").
    /// </summary>
    public string ModuleName { get; set; } = null!;

    /// <summary>
    /// Human-readable display name for the module (e.g., "Cash Flow", "Profit & Loss").
    /// </summary>
    public string ModuleDisplayName { get; set; } = null!;

    /// <summary>
    /// A brief description of what the module does.
    /// </summary>
    public string ModuleDescription { get; set; } = null!;

    /// <summary>
    /// The name of the lowest-tier plan that includes this module (e.g., "Professional").
    /// </summary>
    public string RequiredPlanName { get; set; } = null!;

    /// <summary>
    /// The name of the current business's plan (e.g., "Starter").
    /// </summary>
    public string CurrentPlanName { get; set; } = null!;
}
