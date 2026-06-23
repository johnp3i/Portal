using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Services;

namespace Portal.Web.Filters;

/// <summary>
/// Global authorization filter that enforces user-level module permissions.
/// Checks the user's granted permission for the resolved module, blocking access
/// when the user has 'none' permission or enforcing readonly restrictions.
/// Runs after PlanPermissionFilter (Order = 2) and skips for demo sessions and business owners.
/// </summary>
public class UserPermissionFilter : IAsyncAuthorizationFilter, IOrderedFilter
{
    private readonly IPlanCheckService _planCheckService;

    public int Order => 2;

    public UserPermissionFilter(IPlanCheckService planCheckService)
    {
        _planCheckService = planCheckService;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Skip if DemoInvitationId claim is present — DemoPermissionFilter handles demo sessions
        var demoInvitationIdClaim = context.HttpContext.User.FindFirst("DemoInvitationId");
        if (demoInvitationIdClaim != null)
            return;

        // Skip if user is not authenticated
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
            return;

        var controllerName = context.RouteData.Values["controller"]?.ToString();

        if (string.IsNullOrEmpty(controllerName))
            return;

        // Skip non-module controllers (exempt list)
        if (IsExemptController(controllerName))
            return;

        // Resolve the module from the controller name
        var module = ModuleControllerMap.ResolveModule(controllerName);

        // Not a module controller — allow through
        if (module == null)
            return;

        // Get the current user's ID
        var userId = context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
            return;

        // Check if user is business owner — owners get full access to all plan-permitted modules
        var isOwner = await _planCheckService.IsOwnerAsync(userId);
        if (isOwner)
            return;

        // Get effective access level for this user and module
        var accessLevel = await _planCheckService.GetEffectiveAccessLevelAsync(userId, module);

        // If access level is 'none' — deny access
        if (accessLevel == AccessLevels.None)
        {
            var isAjax = context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                      || context.HttpContext.Request.ContentType?.Contains("application/json") == true
                      || context.HttpContext.Request.Headers["Accept"].ToString().Contains("application/json");

            if (isAjax)
            {
                context.Result = new JsonResult(new { success = false, message = "You do not have access to this module. Please contact your business owner." })
                {
                    StatusCode = 403
                };
            }
            else
            {
                context.Result = new ViewResult { ViewName = "UserAccessDenied" };
            }
            return;
        }

        // If access level is 'readonly' and request is non-GET
        if (accessLevel == AccessLevels.ReadOnly && context.HttpContext.Request.Method != "GET")
        {
            var actionName = context.RouteData.Values["action"]?.ToString();

            // Allow data-fetching POST endpoints (action names starting with "Get" or "AxGet")
            if (actionName != null &&
                (actionName.StartsWith("Get", StringComparison.OrdinalIgnoreCase) ||
                 actionName.StartsWith("AxGet", StringComparison.OrdinalIgnoreCase)))
            {
                context.HttpContext.Items["UserReadOnly"] = true;
                return;
            }

            // Block non-GET requests for readonly users
            var isAjax = context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                      || context.HttpContext.Request.ContentType?.Contains("application/json") == true
                      || context.HttpContext.Request.Headers["Accept"].ToString().Contains("application/json");

            if (isAjax)
            {
                context.Result = new JsonResult(new { success = false, message = "You have read-only access to this module. Write operations are not permitted." })
                {
                    StatusCode = 403
                };
            }
            else
            {
                context.Result = new ViewResult { ViewName = "ReadOnlyBlocked" };
            }
            return;
        }

        // If access level is 'readonly' and request is GET — set readonly flag and allow through
        if (accessLevel == AccessLevels.ReadOnly)
        {
            context.HttpContext.Items["UserReadOnly"] = true;
        }
    }

    /// <summary>
    /// Determines if the controller is exempt from user permission checks.
    /// Non-module controllers like Home, Account, Demo, Admin, etc. are always allowed.
    /// </summary>
    private static bool IsExemptController(string controllerName)
    {
        return controllerName.Equals("Home", StringComparison.OrdinalIgnoreCase)
            || controllerName.Equals("Account", StringComparison.OrdinalIgnoreCase)
            || controllerName.Equals("Demo", StringComparison.OrdinalIgnoreCase)
            || controllerName.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            || controllerName.Equals("MyBusiness", StringComparison.OrdinalIgnoreCase)
            || controllerName.Equals("Billing", StringComparison.OrdinalIgnoreCase)
            || controllerName.Equals("SetupWizard", StringComparison.OrdinalIgnoreCase)
            || controllerName.Equals("Dashboard", StringComparison.OrdinalIgnoreCase);
    }
}
