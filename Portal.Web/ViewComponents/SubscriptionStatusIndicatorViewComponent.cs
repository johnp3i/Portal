using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Portal.Web.Models.Stripe;
using Portal.Web.Models.ViewComponents;
using Portal.Web.Services;
using Portal.Web.Services.Stripe;

namespace Portal.Web.ViewComponents;

public class SubscriptionStatusIndicatorViewComponent : ViewComponent
{
    private readonly ISubscriptionPlanService _subscriptionPlanService;
    private readonly IPlatformConfigService _platformConfigService;
    private readonly ILogger<SubscriptionStatusIndicatorViewComponent> _logger;

    public SubscriptionStatusIndicatorViewComponent(
        ISubscriptionPlanService subscriptionPlanService,
        IPlatformConfigService platformConfigService,
        ILogger<SubscriptionStatusIndicatorViewComponent> logger)
    {
        _subscriptionPlanService = subscriptionPlanService;
        _platformConfigService = platformConfigService;
        _logger = logger;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        try
        {
            if (UserClaimsPrincipal?.Identity?.IsAuthenticated != true)
            {
                return Content(string.Empty);
            }

            var businessIdClaim = UserClaimsPrincipal.FindFirstValue("BusinessId");
            var isSuperAdmin = UserClaimsPrincipal.IsInRole("SuperAdmin");

            if (!int.TryParse(businessIdClaim, out var businessId) || businessId <= 0)
            {
                return Content(string.Empty);
            }

            var accessResult = await _subscriptionPlanService.GetAccessAsync(businessId);

            // SuperAdmin with no subscription record returns empty
            if (isSuperAdmin &&
                string.IsNullOrEmpty(accessResult.SubscriptionStatus) &&
                string.IsNullOrEmpty(accessResult.PlanName))
            {
                return Content(string.Empty);
            }

            var (badgeText, badgeBackgroundColor) = await MapStatusToBadgeAsync(accessResult);

            var planName = string.IsNullOrEmpty(accessResult.PlanName)
                ? "No Plan"
                : accessResult.PlanName;

            var isOwner = string.Equals(
                UserClaimsPrincipal.FindFirstValue("IsOwner"),
                "true",
                StringComparison.OrdinalIgnoreCase);

            var viewModel = new SubscriptionStatusIndicatorViewModel
            {
                PlanName = planName,
                BadgeText = badgeText,
                BadgeBackgroundColor = badgeBackgroundColor,
                BadgeTextColor = "#FFFFFF",
                IsOwner = isOwner,
                HasActiveSubscription = accessResult.HasActiveSubscription,
                IsGraceAccess = accessResult.IsGraceAccess
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to render subscription status indicator");
            return Content(string.Empty);
        }
    }

    private async Task<(string BadgeText, string BackgroundColor)> MapStatusToBadgeAsync(SubscriptionAccessResult accessResult)
    {
        if (string.IsNullOrEmpty(accessResult.SubscriptionStatus))
        {
            return ("No Subscription", "#C24A4A");
        }

        // Detect promo trial: trialing status with no Stripe subscription
        if (string.Equals(accessResult.SubscriptionStatus, "trialing", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrEmpty(accessResult.StripeSubscriptionId))
        {
            var trialBadgeText = await _platformConfigService.GetValueAsync("TrialBadgeText") ?? "Trial";
            return (trialBadgeText, "#0D5EA6");
        }

        return accessResult.SubscriptionStatus.ToLowerInvariant() switch
        {
            "active" => ("Active", "#129867"),
            "trialing" => ("Trial", "#0D5EA6"),
            "past_due" => ("Past Due", "#C8912E"),
            "cancelled" => ("Cancelled", "#C24A4A"),
            _ => ("Unknown", "#C24A4A")
        };
    }
}
