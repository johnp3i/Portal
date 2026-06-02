namespace Portal.Web.Models.ViewComponents;

/// <summary>
/// View model for the SubscriptionStatusIndicator ViewComponent.
/// Contains all display data needed to render the subscription badge in the sidebar.
/// </summary>
public class SubscriptionStatusIndicatorViewModel
{
    /// <summary>
    /// Display name for the plan. Truncated to 20 chars in the view.
    /// Defaults to "No Plan" when no subscription exists.
    /// </summary>
    public string PlanName { get; set; } = "No Plan";

    /// <summary>
    /// Text displayed inside the badge pill (e.g., "Active", "Trial", "Past Due", "Cancelled", "No Subscription", "Unknown").
    /// </summary>
    public string BadgeText { get; set; } = "No Subscription";

    /// <summary>
    /// CSS hex color for badge background. Determined by status mapping.
    /// </summary>
    public string BadgeBackgroundColor { get; set; } = "#C24A4A";

    /// <summary>
    /// CSS hex color for badge text. Always #FFFFFF per design system.
    /// </summary>
    public string BadgeTextColor { get; set; } = "#FFFFFF";

    /// <summary>
    /// Whether the current user is a business owner (controls link rendering).
    /// </summary>
    public bool IsOwner { get; set; }

    /// <summary>
    /// Passthrough from SubscriptionAccessResult for potential future use.
    /// </summary>
    public bool HasActiveSubscription { get; set; }

    /// <summary>
    /// Passthrough from SubscriptionAccessResult for potential future use.
    /// </summary>
    public bool IsGraceAccess { get; set; }
}
