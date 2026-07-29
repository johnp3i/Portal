namespace Portal.Infrastructure.Constants;

public static class PortalModules
{
    public const string Customer = "customer";
    public const string Quotation = "quotation";
    public const string Invoice = "invoice";
    public const string Revenue = "revenue";
    public const string Purchase = "purchase";
    public const string Vat = "vat";
    public const string Credit = "credit";
    public const string Audit = "audit";
    public const string Products = "products";
    public const string PaymentLinkManual = "payment_link_manual";
    public const string PaymentReminderManual = "payment_reminder_manual";
    public const string PaymentLinkAuto = "payment_link_auto";
    public const string PaymentReminderAuto = "payment_reminder_auto";
    public const string Cashflow = "cashflow";
    public const string Pnl = "pnl";
    public const string ExpenseInsights = "expense_insights";
    public const string Attachments = "attachments";
    public const string ClientPortal = "client_portal";
    public const string ActivityTimeline = "activity_timeline";
    public const string AuditLog = "audit_log";
    public const string Api = "api";
    public const string Webhooks = "webhooks";
    public const string MultiCurrency = "multi_currency";
    public const string SchedulePayments = "schedule_payments";
    public const string RecurringExpenseValidation = "recurring_expense_validation";
    public const string PurchaseImport = "purchase_import";
    public const string ZReportImport = "zreport_import";
    public const string Sales = "sales";
    public const string StripeConnect = "stripe_connect";
    public const string Compliance = "compliance";

    public static readonly string[] All =
    {
        Customer, Quotation, Invoice, Revenue, Purchase, Vat, Credit, Audit, Products,
        PaymentLinkManual, PaymentReminderManual, PaymentLinkAuto, PaymentReminderAuto,
        Cashflow, Pnl, ExpenseInsights, Attachments, ClientPortal,
        ActivityTimeline, AuditLog, Api, Webhooks, MultiCurrency, SchedulePayments,
        RecurringExpenseValidation, PurchaseImport, ZReportImport, Sales, StripeConnect,
        Compliance
    };

    public static bool IsValid(string module) => module is not null && All.Contains(module);
}
