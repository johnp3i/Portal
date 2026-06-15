using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Services;

namespace Portal.Web.Filters;

/// <summary>
/// Global authorization filter that enforces module-level permissions for demo sessions.
/// Checks for the DemoInvitationId claim, resolves the module from the controller route,
/// and enforces access levels: 'none' → denied, 'readonly' → GET only, 'full' → all allowed.
/// Non-demo users and non-module controllers are allowed through without restriction.
/// </summary>
public class DemoPermissionFilter : IAsyncAuthorizationFilter
{
    private readonly IDemoInvitationService _demoService;

    /// <summary>
    /// Maps module names to the controller names that belong to each module.
    /// Used to resolve which module a given controller belongs to.
    /// </summary>
    private static readonly Dictionary<string, string[]> ModuleControllers = new()
    {
        [PortalModules.Customer] = new[] { "Customer", "Customers" },
        [PortalModules.Quotation] = new[] { "Quotation", "Quotations", "Proposal" },
        [PortalModules.Invoice] = new[] { "Invoice", "Invoices" },
        [PortalModules.Revenue] = new[] { "Payment", "Payments", "Revenue" },
        [PortalModules.Purchase] = new[] { "Purchase", "Purchases", "Supplier", "Expense" },
        [PortalModules.Vat] = new[] { "Vat", "VatSubmission" },
        [PortalModules.Credit] = new[] { "CreditNote", "CreditNotes" },
        [PortalModules.Audit] = new[] { "AuditLog", "Audit" },
        [PortalModules.Products] = new[] { "Product", "Products" }
    };

    public DemoPermissionFilter(IDemoInvitationService demoService)
    {
        _demoService = demoService;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Only applies to demo sessions — skip if no DemoInvitationId claim
        var demoInvitationIdClaim = context.HttpContext.User.FindFirst("DemoInvitationId");
        if (demoInvitationIdClaim == null)
            return;

        var invitationId = int.Parse(demoInvitationIdClaim.Value);
        var controllerName = context.RouteData.Values["controller"]?.ToString();
        var actionName = context.RouteData.Values["action"]?.ToString();

        if (string.IsNullOrEmpty(controllerName))
            return;

        // Block all email-sending actions for demo users (regardless of access level)
        if (IsEmailSendingAction(controllerName, actionName))
        {
            var isAjax = context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                      || context.HttpContext.Request.ContentType?.Contains("application/json") == true;

            if (isAjax)
            {
                context.Result = new JsonResult(new { success = false, message = "Email sending is disabled in demo mode." });
            }
            else
            {
                context.Result = new ViewResult { ViewName = "DemoAccessRestricted" };
            }
            return;
        }
        // Resolve the module from the controller name
        var module = ModuleControllers
            .FirstOrDefault(kv => kv.Value.Contains(controllerName, StringComparer.OrdinalIgnoreCase))
            .Key;

        // Not a module controller (Home, Account, Demo, etc.) — allow through
        if (module == null)
            return;

        // Get permissions for this invitation
        var permissions = await _demoService.GetPermissionsForInvitationAsync(invitationId);

        // Deny access if module has 'none' permission or no permission entry
        if (!permissions.TryGetValue(module, out var accessLevel) || accessLevel == AccessLevels.None)
        {
            context.Result = new ViewResult { ViewName = "DemoAccessRestricted" };
            return;
        }

        // Block non-GET requests for readonly modules
        if (accessLevel == AccessLevels.ReadOnly && context.HttpContext.Request.Method != "GET")
        {
            // Allow AJAX data-fetching POST endpoints (action names starting with "Get") for readonly access
            if (actionName != null && actionName.StartsWith("Get", StringComparison.OrdinalIgnoreCase))
            {
                // Data retrieval action — allow even for readonly
                context.HttpContext.Items["DemoReadOnly"] = true;
                return;
            }

            // Check if this is an AJAX request
            var isAjax = context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                      || context.HttpContext.Request.ContentType?.Contains("application/json") == true
                      || context.HttpContext.Request.Headers["Accept"].ToString().Contains("application/json");

            if (isAjax)
            {
                context.Result = new JsonResult(new { success = false, message = "Demo access is read-only for this module." })
                {
                    StatusCode = 403
                };
            }
            else
            {
                context.Result = new ViewResult { ViewName = "DemoReadOnlyBlocked" };
            }
            return;
        }

        // AccessLevel is 'full' or GET on 'readonly' — allow through
        // For readonly, set a flag so views can show a read-only banner
        if (accessLevel == AccessLevels.ReadOnly)
        {
            context.HttpContext.Items["DemoReadOnly"] = true;
        }
    }

    /// <summary>
    /// Determines if the action is one that sends emails. Demo users are blocked from all email operations.
    /// </summary>
    private static bool IsEmailSendingAction(string? controllerName, string? actionName)
    {
        if (string.IsNullOrEmpty(actionName) || string.IsNullOrEmpty(controllerName))
            return false;

        // Block sharing actions (these send emails to customers)
        if (actionName.Equals("Share", StringComparison.OrdinalIgnoreCase))
            return true;

        // Block statement email sending
        if (actionName.Equals("EmailStatement", StringComparison.OrdinalIgnoreCase))
            return true;

        // Block any action containing "SendEmail" or "ResendEmail"
        if (actionName.Contains("SendEmail", StringComparison.OrdinalIgnoreCase)
            || actionName.Contains("ResendEmail", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
