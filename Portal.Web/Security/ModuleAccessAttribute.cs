using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Services;
using Portal.Web.Services.Stripe;

namespace Portal.Web.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class ModuleAccessAttribute : Attribute, IAsyncAuthorizationFilter
{
    public string Module { get; }
    public string RequiredLevel { get; }

    public ModuleAccessAttribute(string module, string requiredLevel = AccessLevels.ReadOnly)
    {
        Module = module;
        RequiredLevel = requiredLevel;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILogger<ModuleAccessAttribute>>();

        // (1) SuperAdmin bypasses all checks
        if (user.IsInRole("SuperAdmin"))
            return;

        // (2) Resolve BusinessId from claims
        var businessIdClaim = user.FindFirst("BusinessId");
        if (businessIdClaim is null || !int.TryParse(businessIdClaim.Value, out var businessId) || businessId == 0)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            logger.LogWarning(
                "Module access denied: no business association. UserId={UserId}, Module={Module}, BusinessId=none, Reason={Reason}",
                userId, Module, "no_business_association");

            context.Result = new ViewResult
            {
                ViewName = "~/Views/Shared/NoBusinessAssociation.cshtml",
                StatusCode = 403
            };
            return;
        }

        // Validate module identifier against PortalModules.All
        if (!PortalModules.IsValid(Module))
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            logger.LogWarning(
                "Module access denied: invalid module identifier. UserId={UserId}, Module={Module}, BusinessId={BusinessId}, Reason={Reason}",
                userId, Module, businessId, "invalid_module");

            context.Result = new ForbidResult();
            return;
        }

        // (3) Check Subscription status via SubscriptionPlanService.GetAccessAsync
        var subscriptionPlanService = context.HttpContext.RequestServices
            .GetRequiredService<ISubscriptionPlanService>();

        var accessResult = await subscriptionPlanService.GetAccessAsync(businessId);

        if (!accessResult.HasActiveSubscription)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var status = accessResult.SubscriptionStatus;

            // cancelled/incomplete/unpaid → redirect to "subscription required" page
            logger.LogWarning(
                "Module access denied: subscription inactive. UserId={UserId}, Module={Module}, BusinessId={BusinessId}, Reason={Reason}",
                userId, Module, businessId, "subscription_inactive");

            context.Result = new ViewResult
            {
                ViewName = "~/Views/Shared/SubscriptionRequired.cshtml",
                StatusCode = 403
            };
            return;
        }

        // past_due → allow with warning banner
        if (accessResult.SubscriptionStatus == "past_due")
        {
            context.HttpContext.Items["SubscriptionWarning"] = "Your payment is overdue. Please update your billing information to avoid service interruption.";
        }

        // (4) Check PlanFeature includes requested module
        if (!accessResult.IncludedModules.Contains(Module))
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            logger.LogWarning(
                "Module access denied: module not in plan. UserId={UserId}, Module={Module}, BusinessId={BusinessId}, Reason={Reason}",
                userId, Module, businessId, "module_not_in_plan");

            context.Result = new ViewResult
            {
                ViewName = "~/Views/Shared/UpgradeRequired.cshtml",
                StatusCode = 403
            };
            return;
        }

        // (5) Check user-level permission (existing IPermissionService)
        var userIdForPermission = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdForPermission))
        {
            context.Result = new ForbidResult();
            return;
        }

        var permissionService = context.HttpContext.RequestServices
            .GetRequiredService<IPermissionService>();

        var accessLevel = await permissionService.GetAccessLevelAsync(userIdForPermission, Module, businessId);

        if (!AccessLevels.MeetsRequirement(accessLevel, RequiredLevel))
        {
            context.Result = new ForbidResult();
        }
    }
}
