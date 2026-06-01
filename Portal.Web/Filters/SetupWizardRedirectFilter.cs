using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Portal.Infrastructure.Services;
using Portal.Web.Services.Stripe;

namespace Portal.Web.Filters;

/// <summary>
/// Global action filter that redirects authenticated business owners to the setup wizard
/// when their business has no BusinessProfile record (setup not completed).
/// Excludes the SetupWizard controller itself, API controllers, webhook endpoints,
/// Account controller, and Checkout controller to avoid infinite redirect loops
/// and allow essential flows to proceed.
/// </summary>
public class SetupWizardRedirectFilter : IAsyncActionFilter
{
    private readonly ISetupWizardService _setupWizardService;
    private readonly ILogger<SetupWizardRedirectFilter> _logger;

    /// <summary>
    /// Controller names that are excluded from the redirect check.
    /// These controllers must remain accessible even when setup is incomplete.
    /// </summary>
    private static readonly HashSet<string> ExcludedControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "SetupWizard",
        "Account",
        "Checkout",
        "StripeWebhook"
    };

    public SetupWizardRedirectFilter(
        ISetupWizardService setupWizardService,
        ILogger<SetupWizardRedirectFilter> logger)
    {
        _setupWizardService = setupWizardService;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;

        // Skip if user is not authenticated
        if (user.Identity is not { IsAuthenticated: true })
        {
            await next();
            return;
        }

        // Skip if user does not have the IsOwner claim
        if (!user.HasClaim("IsOwner", "true"))
        {
            await next();
            return;
        }

        // Skip if user does not have a BusinessId claim
        var businessIdClaim = user.FindFirst("BusinessId");
        if (businessIdClaim is null || !int.TryParse(businessIdClaim.Value, out var businessId) || businessId == 0)
        {
            await next();
            return;
        }

        // Skip if the current request is targeting an excluded controller
        var controllerName = context.RouteData.Values["controller"]?.ToString();
        if (!string.IsNullOrEmpty(controllerName) && ExcludedControllers.Contains(controllerName))
        {
            await next();
            return;
        }

        // Skip API controllers (identified by ApiController attribute or api/ route prefix)
        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.ApiControllerAttribute>() is not null)
        {
            await next();
            return;
        }

        // Check if setup is complete (BusinessProfile exists)
        var isSetupComplete = await _setupWizardService.IsSetupCompleteAsync(businessId);

        if (!isSetupComplete)
        {
            _logger.LogInformation(
                "Redirecting user to setup wizard. BusinessId {BusinessId} has no BusinessProfile",
                businessId);

            context.Result = new RedirectResult("/Setup/Wizard");
            return;
        }

        await next();
    }
}
