using Portal.Infrastructure.Models.Sales;

namespace Portal.Infrastructure.Models;

/// <summary>
/// Aggregated view model for the upgraded home dashboard.
/// Replaces the previous ViewBag-based approach with a strongly-typed model
/// that provides compile-time safety and cleaner view code.
/// </summary>
public class DashboardViewModel
{
    // Tenant
    public string CurrencySymbol { get; set; } = "€";

    // Quotation KPIs (existing)
    public int DraftCount { get; set; }
    public int SentThisMonthCount { get; set; }
    public decimal SentThisMonthValue { get; set; }
    public int AcceptedCount { get; set; }
    public decimal AcceptanceRate { get; set; }
    public int ActiveCustomerCount { get; set; }

    // Revenue KPIs
    public decimal RevenueThisMonth { get; set; }
    public int RevenuePaymentCount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public int OutstandingInvoiceCount { get; set; }
    public decimal OverdueAmount { get; set; }
    public int OverdueInvoiceCount { get; set; }
    public decimal ExpensesThisMonth { get; set; }
    public int ExpensesPurchaseCount { get; set; }

    // Charts
    public List<RevenueVsExpensesDto> RevenueVsExpenses { get; set; } = new();
    public InvoiceStatusBreakdownDto InvoiceStatusBreakdown { get; set; } = new();

    // Tables
    public List<RecentInvoiceDto> RecentInvoices { get; set; } = new();
    public List<OverdueInvoiceDto> OverdueInvoices { get; set; } = new();
    public int TotalOverdueCount { get; set; }
    public decimal TotalOverdueAmount { get; set; }
    public List<RecentPaymentDto> RecentPayments { get; set; } = new();
    public List<QuotationListDto> RecentQuotations { get; set; } = new();

    // VAT Summary
    public decimal OutputVat { get; set; }
    public decimal InputVat { get; set; }
    public decimal NetVatPayable { get; set; }
    public string VatPeriodLabel { get; set; } = string.Empty;
    public bool HasVatData { get; set; }

    // Top Customers
    public List<TopCustomerDto> TopCustomers { get; set; } = new();

    // Today's Brief (Sales module)
    public List<DashboardTaskBriefDto> BriefTasks { get; set; } = new();
    public List<DashboardMeetingBriefDto> BriefMeetings { get; set; } = new();
    public bool ShowSales { get; set; }

    // Scope visibility flags
    public bool ShowRevenue { get; set; } = true;
    public bool ShowInvoice { get; set; } = true;
    public bool ShowQuotation { get; set; } = true;
    public bool ShowPurchase { get; set; } = true;
    public bool ShowVat { get; set; } = true;
    public bool ShowCustomer { get; set; } = true;
    public bool HasAnyKpiSection { get; set; } = true;
    public bool ShowPnlTeaser { get; set; }

    // Empty state
    public string? BusinessName { get; set; }
}
