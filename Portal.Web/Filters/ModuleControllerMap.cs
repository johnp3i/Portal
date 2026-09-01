using Portal.Infrastructure.Constants;

namespace Portal.Web.Filters;

/// <summary>
/// Maps module keys to their corresponding controller names.
/// Shared between DemoPermissionFilter, PlanPermissionFilter, and UserPermissionFilter
/// to resolve which module a given controller belongs to.
/// </summary>
public static class ModuleControllerMap
{
    /// <summary>
    /// Maps each module key to the controller names that belong to it.
    /// Only modules with dedicated controllers are included.
    /// </summary>
    public static readonly Dictionary<string, string[]> Map = new()
    {
        [PortalModules.Customer] = new[] { "Customer", "Customers" },
        [PortalModules.Quotation] = new[] { "Quotation", "Quotations", "Proposal", "ProposalSection", "LineItemCatalog", "LineItemCatalogManagement", "Logo" },
        [PortalModules.Invoice] = new[] { "Invoice", "Invoices" },
        [PortalModules.Revenue] = new[] { "Payment", "Payments", "Revenue", "Statement", "ZReport", "Receipt" },
        [PortalModules.ZReportImport] = new[] { "ZReportImport", "SalesImport" },
        [PortalModules.ExternalPlatformImport] = new[] { "ExternalPlatform" },
        [PortalModules.Purchase] = new[] { "Purchase", "Purchases", "Supplier", "Expense", "ExpenseCategory", "ExpenseCategoryLimit" },
        [PortalModules.Vat] = new[] { "Vat", "VatSubmission" },
        [PortalModules.Credit] = new[] { "CreditNote", "CreditNotes" },
        [PortalModules.Audit] = new[] { "Audit" },
        [PortalModules.Products] = new[] { "Product", "Products" },
        [PortalModules.PaymentLinkManual] = new[] { "PaymentLink" },
        [PortalModules.PaymentReminderManual] = new[] { "PaymentReminder" },
        [PortalModules.PaymentLinkAuto] = new[] { "PaymentLinkAuto" },
        [PortalModules.PaymentReminderAuto] = new[] { "PaymentReminderAuto" },
        [PortalModules.Cashflow] = new[] { "Cashflow", "CashFlow" },
        [PortalModules.Pnl] = new[] { "ProfitLoss", "Pnl" },
        [PortalModules.ExpenseInsights] = new[] { "ExpenseInsight", "ExpenseInsights" },
        [PortalModules.Attachments] = new[] { "Attachment", "Attachments" },
        [PortalModules.ClientPortal] = new[] { "ClientPortal" },
        [PortalModules.ActivityTimeline] = new[] { "ActivityTimeline" },
        [PortalModules.AuditLog] = new[] { "AuditLog", "Activity" },
        [PortalModules.Api] = new[] { "Api" },
        [PortalModules.Webhooks] = new[] { "Webhook", "Webhooks" },
        [PortalModules.MultiCurrency] = new[] { "MultiCurrency", "Currency" },
        [PortalModules.SchedulePayments] = new[] { "PaymentSchedule" },
        [PortalModules.RecurringExpenseValidation] = new[] { "RecurringExpense" },
        [PortalModules.PurchaseImport] = new[] { "PurchaseImport", "ParserTemplate" },
        [PortalModules.Sales] = new[] { "Sales" },
        [PortalModules.StripeConnect] = new[] { "CardPayments" },
        [PortalModules.Compliance] = new[] { "Compliance", "AdminCompliance" },
        [PortalModules.Payroll] = new[] { "Payroll", "AdminPayroll", "PayrollReport", "PayrollCompliance" },
    };

    /// <summary>
    /// Reverse lookup: controller name (case-insensitive) → module key. O(1) per lookup.
    /// Built once at class initialization from the forward Map.
    /// </summary>
    private static readonly Dictionary<string, string> ReverseMap = BuildReverseMap();

    private static Dictionary<string, string> BuildReverseMap()
    {
        var reverse = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in Map)
        {
            foreach (var controllerName in kv.Value)
            {
                reverse[controllerName] = kv.Key;
            }
        }
        return reverse;
    }

    /// <summary>
    /// Resolves which module a controller belongs to by searching the reverse map.
    /// Returns null if the controller is not mapped to any module.
    /// </summary>
    public static string? ResolveModule(string controllerName)
    {
        return ReverseMap.TryGetValue(controllerName, out var module) ? module : null;
    }
}
