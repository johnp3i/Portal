using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;

namespace Portal.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly IQuotationService _quotationService;
    private readonly ICustomerService _customerService;
    private readonly IBusinessService _businessService;
    private readonly ICurrentTenantService _tenantService;
    private readonly IDashboardService _dashboardService;

    public HomeController(
        IQuotationService quotationService,
        ICustomerService customerService,
        IBusinessService businessService,
        ICurrentTenantService tenantService,
        IDashboardService dashboardService)
    {
        _quotationService = quotationService;
        _customerService = customerService;
        _businessService = businessService;
        _tenantService = tenantService;
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var businessId = _tenantService.CurrentBusinessId;

        if (businessId == 0)
        {
            return RedirectToAction(nameof(Error));
        }

        // Execute service calls sequentially — DbContext is not thread-safe
        // and cannot handle concurrent operations on the same instance.
        var allQuotations = await _quotationService.GetQuotationsAsync();
        var customers = await _customerService.GetCustomersAsync(null, true);
        var profile = await _businessService.GetBusinessProfileAsync(businessId);
        var kpiData = await _dashboardService.GetKpiDataAsync(businessId);
        var expensesData = await _dashboardService.GetExpensesThisMonthAsync(businessId);
        var revenueVsExpenses = await _dashboardService.GetRevenueVsExpensesAsync(businessId);
        var invoiceStatus = await _dashboardService.GetInvoiceStatusBreakdownAsync(businessId);
        var recentInvoices = await _dashboardService.GetRecentInvoicesAsync(businessId);
        var overdueInvoicesResult = await _dashboardService.GetOverdueInvoicesAsync(businessId, null, 1, 10);
        var recentPaymentsResult = await _dashboardService.GetRecentPaymentsAsync(businessId, null, 1, 5);
        var vatSummary = await _dashboardService.GetVatSummaryAsync(businessId);
        var topCustomers = await _dashboardService.GetTopCustomersAsync(businessId);

        // Quotation KPI logic (migrated from ViewBag)
        var draftCount = allQuotations.Count(q => q.QuotationStatusTypeId == 1);
        var sentThisMonth = allQuotations.Where(q => q.QuotationStatusTypeId == 2).ToList();
        var acceptedCount = allQuotations.Count(q => q.QuotationStatusTypeId == 3);
        var totalSent = allQuotations.Count(q => q.QuotationStatusTypeId >= 2);
        var activeCustomerCount = customers.Count;
        var sentThisMonthValue = sentThisMonth.Sum(q => q.TotalAmount);
        var acceptanceRate = totalSent > 0 ? Math.Round((decimal)acceptedCount / totalSent * 100, 0) : 0;

        var model = new DashboardViewModel
        {
            // Tenant
            CurrencySymbol = profile?.CurrencySymbol ?? "€",

            // Quotation KPIs
            DraftCount = draftCount,
            SentThisMonthCount = sentThisMonth.Count,
            SentThisMonthValue = sentThisMonthValue,
            AcceptedCount = acceptedCount,
            AcceptanceRate = acceptanceRate,
            ActiveCustomerCount = activeCustomerCount,

            // Revenue KPIs
            RevenueThisMonth = kpiData.PaidThisMonth,
            RevenuePaymentCount = kpiData.PaidThisMonthCount,
            OutstandingAmount = kpiData.OutstandingReceivables,
            OutstandingInvoiceCount = kpiData.OutstandingInvoiceCount,
            OverdueAmount = kpiData.OverdueAmount,
            OverdueInvoiceCount = kpiData.OverdueInvoiceCount,
            ExpensesThisMonth = expensesData.TotalExpenses,
            ExpensesPurchaseCount = expensesData.PurchaseCount,

            // Charts
            RevenueVsExpenses = revenueVsExpenses,
            InvoiceStatusBreakdown = invoiceStatus,

            // Tables
            RecentInvoices = recentInvoices,
            OverdueInvoices = overdueInvoicesResult.Items,
            TotalOverdueCount = overdueInvoicesResult.TotalCount,
            TotalOverdueAmount = overdueInvoicesResult.Items.Sum(i => i.OutstandingBalance),
            RecentPayments = recentPaymentsResult.Items,
            RecentQuotations = allQuotations.Take(5).ToList(),

            // VAT Summary
            OutputVat = vatSummary.TotalOutputVat,
            InputVat = vatSummary.TotalInputVat,
            NetVatPayable = vatSummary.NetVatPayable,
            VatPeriodLabel = vatSummary.PeriodLabel,
            HasVatData = vatSummary.HasData,

            // Top Customers
            TopCustomers = topCustomers
        };

        return View(model);
    }

    [AllowAnonymous]
    [Route("/Home/Error")]
    public IActionResult Error()
    {
        return View();
    }
}
