using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Entities;
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
    private readonly IPermissionService _permissionService;

    public HomeController(
        IQuotationService quotationService,
        ICustomerService customerService,
        IBusinessService businessService,
        ICurrentTenantService tenantService,
        IDashboardService dashboardService,
        IPermissionService permissionService)
    {
        _quotationService = quotationService;
        _customerService = customerService;
        _businessService = businessService;
        _tenantService = tenantService;
        _dashboardService = dashboardService;
        _permissionService = permissionService;
    }

    [HttpGet]
    [Route("/Dashboard")]
    public async Task<IActionResult> Index()
    {
        var businessId = _tenantService.CurrentBusinessId;

        if (businessId == 0)
        {
            return RedirectToAction(nameof(Error));
        }

        // Resolve scope
        DashboardScopeDto scope;
        var isPrivileged = User.HasClaim("IsOwner", "true")
                        || User.IsInRole("SuperAdmin");

        var demoInvitationIdClaim = User.FindFirst("DemoInvitationId")?.Value;

        if (isPrivileged)
        {
            scope = DashboardScopeDto.FullAccess();
        }
        else if (!string.IsNullOrEmpty(demoInvitationIdClaim) && int.TryParse(demoInvitationIdClaim, out var invitationId))
        {
            // Demo session — load permissions from DemoInvitationPermission
            var permissions = await _permissionService.GetDemoPermissionsAsync(invitationId);
            scope = DashboardScopeDto.FromPermissions(permissions);
        }
        else
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            try
            {
                var permissions = await _permissionService.GetAllAccessLevelsAsync(userId!, businessId);
                scope = DashboardScopeDto.FromPermissions(permissions);
            }
            catch
            {
                // Permission service failure — show empty state
                return View(new DashboardViewModel
                {
                    HasAnyKpiSection = false,
                    BusinessName = (await _businessService.GetBusinessByIdAsync(businessId))?.Name
                });
            }
        }

        // Execute service calls sequentially — DbContext is not thread-safe
        // and cannot handle concurrent operations on the same instance.
        // Only fetch data for sections the user has access to.
        var profile = await _businessService.GetBusinessProfileAsync(businessId);

        // Revenue-scoped calls
        var kpiData = new DashboardKpiDto();
        var revenueVsExpenses = new List<RevenueVsExpensesDto>();
        var overdueInvoicesResult = new PagedResult<OverdueInvoiceDto>();
        var recentPaymentsResult = new PagedResult<RecentPaymentDto>();
        var topCustomers = new List<TopCustomerDto>();

        if (scope.ShowRevenue)
        {
            kpiData = await _dashboardService.GetKpiDataAsync(businessId);
            overdueInvoicesResult = await _dashboardService.GetOverdueInvoicesAsync(businessId, null, 1, 10);
            recentPaymentsResult = await _dashboardService.GetRecentPaymentsAsync(businessId, null, 1, 5);
            revenueVsExpenses = await _dashboardService.GetRevenueVsExpensesAsync(businessId);
            topCustomers = await _dashboardService.GetTopCustomersAsync(businessId);
        }

        // Invoice-scoped calls
        var invoiceStatus = new InvoiceStatusBreakdownDto();
        var recentInvoices = new List<RecentInvoiceDto>();

        if (scope.ShowInvoice)
        {
            invoiceStatus = await _dashboardService.GetInvoiceStatusBreakdownAsync(businessId);
            recentInvoices = await _dashboardService.GetRecentInvoicesAsync(businessId);
        }

        // Quotation-scoped calls
        var allQuotations = new List<QuotationListDto>();
        var customers = new List<Customer>();

        if (scope.ShowQuotation)
        {
            allQuotations = await _quotationService.GetQuotationsAsync();
            customers = await _customerService.GetCustomersAsync(null, true);
        }

        // Purchase-scoped calls
        var expensesData = new ExpensesKpiDto();

        if (scope.ShowPurchase)
        {
            expensesData = await _dashboardService.GetExpensesThisMonthAsync(businessId);
        }

        // VAT-scoped calls
        var vatSummary = new VatSummaryDto();

        if (scope.ShowVat)
        {
            vatSummary = await _dashboardService.GetVatSummaryAsync(businessId);
        }

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
            VatPeriodLabel = vatSummary.PeriodLabel ?? string.Empty,
            HasVatData = vatSummary.HasData,

            // Top Customers
            TopCustomers = topCustomers,

            // Scope visibility flags
            ShowRevenue = scope.ShowRevenue,
            ShowInvoice = scope.ShowInvoice,
            ShowQuotation = scope.ShowQuotation,
            ShowPurchase = scope.ShowPurchase,
            ShowVat = scope.ShowVat,
            ShowCustomer = scope.ShowCustomer,
            HasAnyKpiSection = scope.HasAnyKpiSection
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
