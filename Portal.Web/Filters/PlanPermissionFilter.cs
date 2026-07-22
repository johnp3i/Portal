using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Portal.Infrastructure.Services;
using Portal.Web.Models;

namespace Portal.Web.Filters;

/// <summary>
/// Global authorization filter that enforces plan-level module access.
/// Checks if the business's active subscription plan includes the requested module.
/// Non-module controllers and demo sessions are exempt from plan checks.
/// Executes at Order = 1 (after DemoPermissionFilter at 0, before UserPermissionFilter at 2).
/// </summary>
public class PlanPermissionFilter : IAsyncAuthorizationFilter, IOrderedFilter
{
    private readonly IPlanCheckService _planCheckService;

    /// <summary>
    /// Controllers exempt from plan permission checks — these are non-module system pages.
    /// </summary>
    private static readonly HashSet<string> NonModuleControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Home",
        "Account",
        "Demo",
        "Admin",
        "MyBusiness",
        "Billing",
        "SetupWizard",
        "Dashboard"
    };

    public int Order => 1;

    public PlanPermissionFilter(IPlanCheckService planCheckService)
    {
        _planCheckService = planCheckService;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // 1. Skip if DemoInvitationId claim is present — defer to DemoPermissionFilter
        var demoInvitationIdClaim = context.HttpContext.User.FindFirst("DemoInvitationId");
        if (demoInvitationIdClaim != null)
            return;

        // 2. Skip if user is not authenticated
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
            return;

        // 3. Get controller name from route
        var controllerName = context.RouteData.Values["controller"]?.ToString();
        if (string.IsNullOrEmpty(controllerName))
            return;

        // 4. Skip if controller is non-module (exempt list)
        if (NonModuleControllers.Contains(controllerName))
            return;

        // 5. Resolve module from controller name
        var module = ModuleControllerMap.ResolveModule(controllerName);

        // If module is null (controller not mapped to any module), allow through
        if (module == null)
            return;

        // Detect AJAX request (same pattern as DemoPermissionFilter)
        var isAjax = context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                  || context.HttpContext.Request.ContentType?.Contains("application/json") == true
                  || context.HttpContext.Request.Headers["Accept"].ToString().Contains("application/json");

        // 6. Check if business has an active subscription
        var hasActiveSubscription = await _planCheckService.HasActiveSubscriptionAsync();
        if (!hasActiveSubscription)
        {
            if (isAjax)
            {
                context.Result = new JsonResult(new { success = false, message = "Your subscription is inactive. Please renew your subscription to continue." })
                {
                    StatusCode = 403
                };
            }
            else
            {
                context.Result = new ViewResult
                {
                    ViewName = "SubscriptionInactive",
                    ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary<SoftGateViewModel>(
                        new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
                        new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary())
                    {
                        Model = new SoftGateViewModel
                        {
                            ModuleName = module,
                            ModuleDisplayName = FormatModuleDisplayName(module),
                            ModuleDescription = GetModuleDescription(module),
                            RequiredPlanName = "an active plan",
                            CurrentPlanName = "Inactive"
                        }
                    }
                };
            }
            return;
        }

        // 7. Check if module is included in the business's plan
        var isModuleInPlan = await _planCheckService.IsModuleInPlanAsync(module);
        if (!isModuleInPlan)
        {
            var requiredPlan = await _planCheckService.GetRequiredPlanForModuleAsync(module) ?? "a higher plan";

            if (isAjax)
            {
                context.Result = new JsonResult(new { success = false, message = $"This feature requires the {requiredPlan} plan. Please upgrade to access it." })
                {
                    StatusCode = 403
                };
            }
            else
            {
                context.Result = new ViewResult
                {
                    ViewName = "PlanSoftGate",
                    ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary<SoftGateViewModel>(
                        new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
                        new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary())
                    {
                        Model = new SoftGateViewModel
                        {
                            ModuleName = module,
                            ModuleDisplayName = FormatModuleDisplayName(module),
                            ModuleDescription = GetModuleDescription(module),
                            RequiredPlanName = requiredPlan,
                            CurrentPlanName = await _planCheckService.GetCurrentPlanNameAsync() ?? "your current plan"
                        }
                    }
                };
            }
            return;
        }

        // 8. Store resolved plan access levels in HttpContext.Items for downstream use
        var planModules = await _planCheckService.GetPlanModulesAsync();
        context.HttpContext.Items["PlanPermissions"] = planModules;
    }

    /// <summary>
    /// Formats a module key into a human-readable display name.
    /// E.g., "cashflow" → "Cash Flow", "expense_insights" → "Expense Insights"
    /// </summary>
    private static string FormatModuleDisplayName(string moduleKey)
    {
        if (string.IsNullOrEmpty(moduleKey))
            return string.Empty;

        // Known display name overrides
        var overrides = new Dictionary<string, string>
        {
            ["pnl"] = "Profit & Loss",
            ["vat"] = "VAT",
            ["api"] = "API",
            ["zreport_import"] = "Z-Report Import",
            ["sales"] = "Opportunities"
        };

        if (overrides.TryGetValue(moduleKey, out var displayName))
            return displayName;

        // Split on underscores and capitalize each word
        var words = moduleKey.Split('_');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpper(words[i][0]) + words[i][1..];
            }
        }

        return string.Join(" ", words);
    }

    /// <summary>
    /// Returns a brief description of what the module does, for display on the soft-gate view.
    /// </summary>
    private static string GetModuleDescription(string moduleKey)
    {
        return moduleKey switch
        {
            "quotation" => "Create and manage professional quotations and proposals for your customers.",
            "invoice" => "Generate invoices from quotations and track billing for your business.",
            "revenue" => "Track payments, outstanding balances, and manage your receivables.",
            "customer" => "Manage your customer database, contacts, and communication history.",
            "purchase" => "Record purchases, manage suppliers, and track business expenses.",
            "vat" => "Calculate VAT obligations and manage submission periods.",
            "credit" => "Issue credit notes and manage customer refunds.",
            "products" => "Maintain your product and service catalog with pricing.",
            "payment_link_manual" => "Send manual payment links to customers for easy online payment.",
            "payment_reminder_manual" => "Send manual payment reminders for overdue invoices.",
            "payment_link_auto" => "Automatically send payment links when invoices are issued.",
            "payment_reminder_auto" => "Automatically send payment reminders for overdue invoices.",
            "cashflow" => "Visualise your cash flow trends and forecast future liquidity.",
            "pnl" => "View profit and loss statements with detailed breakdowns.",
            "expense_insights" => "Analyse spending patterns and identify cost-saving opportunities.",
            "attachments" => "Attach documents and files to invoices, quotations, and purchases.",
            "client_portal" => "Provide customers with a self-service portal to view their documents.",
            "activity_timeline" => "Track all activity and changes across your business records.",
            "audit_log" => "Full audit trail of user actions and system events.",
            "schedule_payments" => "Create instalment plans for your invoices, automatically match payments to scheduled instalments, track progress with visual timelines, and receive VAT deadline warnings.",
            "api" => "Access the Portal API for third-party integrations.",
            "webhooks" => "Configure webhooks to receive real-time event notifications.",
            "multi_currency" => "Work with multiple currencies for international business.",
            "purchase_import" => "Import purchases from CSV files with intelligent column mapping and template management.",
            "zreport_import" => "Bulk-import Z-Reports from CSV and import transaction-level sales records from your POS system.",
            "recurring_expense_validation" => "Define expected recurring purchases per supplier, validate that all expected expenses are recorded before VAT submission, and catch missing invoices automatically.",
            "sales" => "Manage your sales pipeline, track leads from enquiry to conversion, schedule meetings, and use response templates.",
            _ => "Access advanced features to enhance your business operations."
        };
    }
}
