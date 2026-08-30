using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;
using Portal.Infrastructure.Services;
using Portal.Infrastructure.Services.Sales;

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
    private readonly IDashboardBriefingService _briefingService;
    private readonly ISystemBriefingService _systemBriefingService;
    private readonly IOnboardingService _onboardingService;
    private readonly IAnnouncementService _announcementService;
    private readonly IFollowUpTaskService _followUpTaskService;
    private readonly IMeetingService _meetingService;
    private readonly IPlanCheckService _planCheckService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        IQuotationService quotationService,
        ICustomerService customerService,
        IBusinessService businessService,
        ICurrentTenantService tenantService,
        IDashboardService dashboardService,
        IPermissionService permissionService,
        IDashboardBriefingService briefingService,
        ISystemBriefingService systemBriefingService,
        IOnboardingService onboardingService,
        IAnnouncementService announcementService,
        IFollowUpTaskService followUpTaskService,
        IMeetingService meetingService,
        IPlanCheckService planCheckService,
        ILogger<HomeController> logger)

    {
        _quotationService = quotationService;
        _customerService = customerService;
        _businessService = businessService;
        _tenantService = tenantService;
        _dashboardService = dashboardService;
        _permissionService = permissionService;
        _briefingService = briefingService;
        _systemBriefingService = systemBriefingService;
        _onboardingService = onboardingService;
        _announcementService = announcementService;
        _followUpTaskService = followUpTaskService;
        _meetingService = meetingService;
        _planCheckService = planCheckService;
        _logger = logger;
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
        var upcomingSupplierPayments = new List<UpcomingSupplierPaymentDto>();

        if (scope.ShowPurchase)
        {
            expensesData = await _dashboardService.GetExpensesThisMonthAsync(businessId);
            upcomingSupplierPayments = await _dashboardService.GetUpcomingSupplierPaymentsAsync(businessId);
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

            // Upcoming Supplier Payments
            UpcomingSupplierPayments = upcomingSupplierPayments,

            // Scope visibility flags
            ShowRevenue = scope.ShowRevenue,
            ShowInvoice = scope.ShowInvoice,
            ShowQuotation = scope.ShowQuotation,
            ShowPurchase = scope.ShowPurchase,
            ShowVat = scope.ShowVat,
            ShowCustomer = scope.ShowCustomer,
            HasAnyKpiSection = scope.HasAnyKpiSection,
            ShowPnlTeaser = scope.ShowPnlTeaser
        };

        // Generate operational briefing
        var briefing = await _briefingService.GenerateBriefingAsync(businessId, scope, profile?.CurrencySymbol ?? "€");
        ViewBag.Briefing = briefing;

        // Today's Brief — only for users with Sales module access
        var hasSalesAccess = await _planCheckService.IsModuleInPlanAsync(PortalModules.Sales);
        model.ShowSales = hasSalesAccess;

        if (hasSalesAccess)
        {
            try
            {
                model.BriefTasks = await _followUpTaskService.GetDashboardBriefAsync(businessId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load dashboard tasks brief");
                model.BriefTasks = new List<DashboardTaskBriefDto>();
            }

            try
            {
                model.BriefMeetings = await _meetingService.GetDashboardMeetingsBriefAsync(businessId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load dashboard meetings brief");
                model.BriefMeetings = new List<DashboardMeetingBriefDto>();
            }
        }

        // Generate system briefing for SuperAdmin only
        if (User.IsInRole("SuperAdmin"))
        {
            try
            {
                var systemBriefing = await _systemBriefingService.GenerateBriefingAsync();
                ViewBag.SystemBriefing = systemBriefing;
            }
            catch { ViewBag.SystemBriefing = null; }
        }

        // Onboarding state
        var onboardingState = await _onboardingService.GetOnboardingStateAsync(businessId);
        ViewBag.Onboarding = onboardingState;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostDismissOnboarding()
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            await _onboardingService.DismissOnboardingAsync(businessId);
            return Json(new { success = true, message = "Onboarding dismissed." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Something went wrong. Please try again." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostDismissAnnouncement(int announcementId)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Json(new { success = false, message = "Not authenticated." });

            var updatedCount = await _announcementService.DismissAsync(userId, announcementId);
            return Json(new { success = true, unreadCount = updatedCount });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to dismiss announcement." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostDismissAllAnnouncements()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Json(new { success = false, message = "Not authenticated." });

            var updatedCount = await _announcementService.DismissAllAsync(userId);
            return Json(new { success = true, unreadCount = updatedCount });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to dismiss announcements." });
        }
    }

    [HttpGet("/Help")]
    public IActionResult Help()
    {
        ViewData["Title"] = "Help & Getting Started";
        return View();
    }

    [AllowAnonymous]
    [Route("/Home/Error")]
    public IActionResult Error()
    {
        return View();
    }
}
