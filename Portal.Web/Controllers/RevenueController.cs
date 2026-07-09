using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;
using Portal.Web.Models;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.Revenue)]
public class RevenueController : Controller
{
    private readonly IPaymentService _paymentService;
    private readonly IDashboardService _dashboardService;
    private readonly IReceivablesQueryService _receivablesQueryService;
    private readonly IVatIntegrationService _vatIntegrationService;
    private readonly ICurrentTenantService _tenantService;
    private readonly ICustomerService _customerService;
    private readonly IInvoiceService _invoiceService;
    private readonly IBusinessService _businessService;
    private readonly IPermissionService _permissionService;
    private readonly IPlanCheckService _planCheckService;
    private readonly IPaymentScheduleService _paymentScheduleService;
    private readonly IPaymentScheduleOverviewService _paymentScheduleOverviewService;

    public RevenueController(
        IPaymentService paymentService,
        IDashboardService dashboardService,
        IReceivablesQueryService receivablesQueryService,
        IVatIntegrationService vatIntegrationService,
        ICurrentTenantService tenantService,
        ICustomerService customerService,
        IInvoiceService invoiceService,
        IBusinessService businessService,
        IPermissionService permissionService,
        IPlanCheckService planCheckService,
        IPaymentScheduleService paymentScheduleService,
        IPaymentScheduleOverviewService paymentScheduleOverviewService)
    {
        _paymentService = paymentService;
        _dashboardService = dashboardService;
        _receivablesQueryService = receivablesQueryService;
        _vatIntegrationService = vatIntegrationService;
        _tenantService = tenantService;
        _customerService = customerService;
        _invoiceService = invoiceService;
        _businessService = businessService;
        _permissionService = permissionService;
        _planCheckService = planCheckService;
        _paymentScheduleService = paymentScheduleService;
        _paymentScheduleOverviewService = paymentScheduleOverviewService;
    }

    // === Page Actions (return Views) ===

    [HttpGet]
    public IActionResult Index()
    {
        return RedirectToAction(nameof(Dashboard));
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var businessId = _tenantService.CurrentBusinessId;

        var kpiData = await _dashboardService.GetKpiDataAsync(businessId);
        var revenueCollected = await _dashboardService.GetRevenueCollectedAsync(businessId);
        var invoicedVsCollected = await _dashboardService.GetInvoicedVsCollectedAsync(businessId);
        var collectionRate = await _dashboardService.GetCollectionRateAsync(businessId);
        var vatSummary = await _vatIntegrationService.GetCurrentPeriodSummaryAsync(businessId);
        var vatLiability = await _vatIntegrationService.GetVatLiabilityByPeriodAsync(businessId);
        var overdueInvoices = await _dashboardService.GetOverdueInvoicesAsync(businessId, null, 1, 10);
        var recentPayments = await _dashboardService.GetRecentPaymentsAsync(businessId, null, 1, 10);

        var profile = await _businessService.GetBusinessProfileAsync(businessId);

        ViewBag.KpiData = kpiData;
        ViewBag.RevenueCollected = revenueCollected;
        ViewBag.InvoicedVsCollected = invoicedVsCollected;
        ViewBag.CollectionRate = collectionRate;
        ViewBag.VatSummary = vatSummary;
        ViewBag.VatLiability = vatLiability;
        ViewBag.OverdueInvoices = overdueInvoices;
        ViewBag.RecentPayments = recentPayments;
        ViewBag.CurrencySymbol = profile?.CurrencySymbol ?? "€";
        ViewBag.PaymentMethods = await _paymentService.GetPaymentMethodTypesAsync();

        // Payment Reminders teaser — show if user doesn't have payment_reminder_auto access
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var showReminderTeaser = false;
        var showCashFlowTeaser = false;
        if (!string.IsNullOrEmpty(userId))
        {
            var permissions = await _permissionService.GetAllAccessLevelsAsync(userId, businessId);
            var hasAutoAccess = permissions.TryGetValue("payment_reminder_auto", out var level) && level != "none";
            showReminderTeaser = !hasAutoAccess;

            var hasCfAccess = permissions.TryGetValue("cashflow", out var cfLevel) && cfLevel != "none";
            showCashFlowTeaser = !hasCfAccess;
        }
        ViewBag.ShowPaymentReminderTeaser = showReminderTeaser;
        ViewBag.ShowCashFlowTeaser = showCashFlowTeaser;

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Receivables(string? search, int? financialStatus, int? customer,
        string? dueFrom, string? dueTo, int page = 1)
    {
        var businessId = _tenantService.CurrentBusinessId;

        DateOnly? dueFromDate = string.IsNullOrWhiteSpace(dueFrom) ? null : DateOnly.Parse(dueFrom);
        DateOnly? dueToDate = string.IsNullOrWhiteSpace(dueTo) ? null : DateOnly.Parse(dueTo);

        var pagedResult = await _receivablesQueryService.GetReceivablesAsync(
            businessId, search, financialStatus, customer, dueFromDate, dueToDate, page);

        var customers = await _customerService.GetCustomersAsync(null, true);
        var paymentMethods = await _paymentService.GetPaymentMethodTypesAsync();
        var profile = await _businessService.GetBusinessProfileAsync(businessId);

        ViewBag.PagedResult = pagedResult;
        ViewBag.SearchTerm = search;
        ViewBag.FinancialStatusFilter = financialStatus;
        ViewBag.CustomerFilter = customer;
        ViewBag.DueFrom = dueFrom;
        ViewBag.DueTo = dueTo;
        ViewBag.Customers = customers;
        ViewBag.PaymentMethods = paymentMethods;
        ViewBag.CurrencySymbol = profile?.CurrencySymbol ?? "€";
        ViewBag.FinancialStatuses = new List<(int Id, string Name)>
        {
            (1, "Unpaid"),
            (2, "Partially Paid"),
            (3, "Paid"),
            (4, "Overdue"),
            (5, "Written Off")
        };

        return View(pagedResult.Items);
    }

    [HttpGet]
    public async Task<IActionResult> InvoiceDetail(int id)
    {
        var businessId = _tenantService.CurrentBusinessId;

        var invoice = await _invoiceService.GetInvoiceByIdAsync(id);
        if (invoice == null || invoice.BusinessId != businessId)
            return NotFound();

        var lines = await _invoiceService.GetInvoiceLinesAsync(id);
        var paymentHistory = await _paymentService.GetPaymentHistoryAsync(id, businessId);
        var paymentMethods = await _paymentService.GetPaymentMethodTypesAsync();
        var customer = await _customerService.GetCustomerByIdAsync(invoice.CustomerId);
        var profile = await _businessService.GetBusinessProfileAsync(businessId);

        var totalPaid = paymentHistory.Where(p => !p.IsVoided).Sum(p => p.Amount);
        var outstandingBalance = invoice.TotalAmount - totalPaid;
        var progressPercentage = RevenueCalculations.ComputeProgressPercentage(invoice.TotalAmount, totalPaid);

        var isOverdue = invoice.DueDate < DateOnly.FromDateTime(DateTime.UtcNow) && outstandingBalance > 0;

        ViewBag.Invoice = invoice;
        ViewBag.Lines = lines;
        ViewBag.PaymentHistory = paymentHistory;
        ViewBag.PaymentMethods = paymentMethods;
        ViewBag.CustomerName = customer?.Name ?? "Unknown";
        ViewBag.TotalPaid = totalPaid;
        ViewBag.OutstandingBalance = outstandingBalance;
        ViewBag.ProgressPercentage = progressPercentage;
        ViewBag.IsOverdue = isOverdue;
        ViewBag.CurrencySymbol = profile?.CurrencySymbol ?? "€";

        // Payment Schedule section data
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var hasSchedulePermission = await HasSchedulePaymentsAccessAsync(userId, businessId);
        ViewData["InvoiceId"] = id;
        ViewData["HasSchedulePermission"] = hasSchedulePermission;
        ViewData["OutstandingBalance"] = outstandingBalance;
        ViewData["InvoiceNumber"] = invoice.InvoiceNumber;
        ViewData["ScheduleCurrencySymbol"] = profile?.CurrencySymbol ?? "€";

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> PaymentSchedules()
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

            // Check plan-level access first
            var isInPlan = await _planCheckService.IsModuleInPlanAsync(PortalModules.SchedulePayments);
            if (!isInPlan)
            {
                var requiredPlan = await _planCheckService.GetRequiredPlanForModuleAsync(PortalModules.SchedulePayments) ?? "Professional";
                return View("PlanSoftGate", new SoftGateViewModel
                {
                    ModuleName = PortalModules.SchedulePayments,
                    ModuleDisplayName = "Payment Schedules",
                    ModuleDescription = "Create instalment plans for your invoices, automatically match payments to scheduled instalments, track progress with visual timelines, and receive VAT deadline warnings — all in one place.",
                    RequiredPlanName = requiredPlan,
                    CurrentPlanName = "your current plan"
                });
            }

            // Then check user-level permission
            var accessLevel = await _permissionService.GetAccessLevelAsync(userId, PortalModules.SchedulePayments, businessId);
            if (accessLevel == "none")
            {
                return View("PlanSoftGate", new SoftGateViewModel
                {
                    ModuleName = PortalModules.SchedulePayments,
                    ModuleDisplayName = "Payment Schedules",
                    ModuleDescription = "Create instalment plans for your invoices, automatically match payments to scheduled instalments, track progress with visual timelines, and receive VAT deadline warnings — all in one place.",
                    RequiredPlanName = "Professional",
                    CurrentPlanName = "your current plan"
                });
            }

            return View();
        }
        catch (Exception ex)
        {
            return RedirectToAction(nameof(Dashboard));
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetPaymentSchedulesOverview()
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

            // Check plan-level access
            var isInPlan = await _planCheckService.IsModuleInPlanAsync(PortalModules.SchedulePayments);
            if (!isInPlan)
                return Json(new { success = false, message = "Payment Schedules is available in the Professional plan. Please upgrade to access this feature." });

            // Check user-level permission
            var accessLevel = await _permissionService.GetAccessLevelAsync(userId, PortalModules.SchedulePayments, businessId);
            if (accessLevel == "none")
                return Json(new { success = false, message = "You do not have permission to view payment schedules." });

            var overview = await _paymentScheduleOverviewService.GetOverviewAsync(businessId);
            return Json(new { success = true, data = overview });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred loading payment schedules." });
        }
    }

    // === AJAX POST Actions ===

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordPayment(DateTime paymentDate, decimal amount,
        int paymentMethodTypeId, int invoiceId, string? reference, string? notes)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? string.Empty;

            var dto = new RecordPaymentDto
            {
                InvoiceId = invoiceId,
                PaymentMethodTypeId = paymentMethodTypeId,
                PaymentDateUtc = paymentDate,
                Amount = amount,
                Reference = reference,
                Notes = notes
            };

            var result = await _paymentService.RecordPaymentAsync(dto, businessId, userId);
            return Json(new { success = result.Success, message = result.Success ? "Payment recorded successfully." : result.Message });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VoidPayment(int paymentId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _paymentService.VoidPaymentAsync(paymentId, businessId);
            return Json(new { success = result.Success, message = result.Success ? "Payment voided successfully." : result.Message });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    // === AJAX GET Data Endpoints ===

    [HttpGet]
    public async Task<IActionResult> GetOverdueInvoices(string? search, int page = 1, int pageSize = 10)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _dashboardService.GetOverdueInvoicesAsync(businessId, search, page, pageSize);
            return Json(new
            {
                success = true,
                data = result.Items,
                currentPage = result.CurrentPage,
                totalPages = result.TotalPages,
                totalCount = result.TotalCount
            });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetRecentPayments(string? search, int page = 1, int pageSize = 10)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _dashboardService.GetRecentPaymentsAsync(businessId, search, page, pageSize);
            return Json(new
            {
                success = true,
                data = result.Items,
                currentPage = result.CurrentPage,
                totalPages = result.TotalPages,
                totalCount = result.TotalCount
            });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetInvoicesWithOutstandingBalance(string? search)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _receivablesQueryService.GetReceivablesAsync(
                businessId, search, null, null, null, null, 1, 50);

            var invoicesWithBalance = result.Items
                .Where(r => r.HasOutstandingBalance)
                .Select(r => new
                {
                    id = r.Id,
                    invoiceNumber = r.InvoiceNumber,
                    customerName = r.CustomerName,
                    outstandingBalance = r.OutstandingBalance
                })
                .ToList();

            return Json(new { success = true, data = invoicesWithBalance });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    // === Payment Schedule AJAX Endpoints ===

    private async Task<bool> HasSchedulePaymentsAccessAsync(string userId, int businessId)
    {
        var isInPlan = await _planCheckService.IsModuleInPlanAsync(PortalModules.SchedulePayments);
        if (!isInPlan) return false;

        var accessLevel = await _permissionService.GetAccessLevelAsync(userId, PortalModules.SchedulePayments, businessId);
        return accessLevel != "none";
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreatePaymentSchedule([FromBody] CreatePaymentScheduleDto dto)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

            if (!await HasSchedulePaymentsAccessAsync(userId, businessId))
                return Json(new { success = false, message = "You do not have permission to manage payment schedules." });

            var result = await _paymentScheduleService.CreateScheduleAsync(dto, businessId, userId);
            return Json(new { success = result.Success, message = result.Success ? "Payment schedule created." : result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateInstalment([FromBody] UpdateInstalmentDto dto)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

            if (!await HasSchedulePaymentsAccessAsync(userId, businessId))
                return Json(new { success = false, message = "You do not have permission to manage payment schedules." });

            var result = await _paymentScheduleService.UpdateInstalmentAsync(dto, businessId, userId);
            return Json(new { success = result.Success, message = result.Success ? "Instalment updated." : result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostAddInstalment([FromBody] AddInstalmentDto dto)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

            if (!await HasSchedulePaymentsAccessAsync(userId, businessId))
                return Json(new { success = false, message = "You do not have permission to manage payment schedules." });

            var result = await _paymentScheduleService.AddInstalmentAsync(dto, businessId, userId);
            return Json(new { success = result.Success, message = result.Success ? "Instalment added." : result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostRemoveInstalment([FromBody] int instalmentId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

            if (!await HasSchedulePaymentsAccessAsync(userId, businessId))
                return Json(new { success = false, message = "You do not have permission to manage payment schedules." });

            var result = await _paymentScheduleService.RemoveInstalmentAsync(instalmentId, businessId, userId);
            return Json(new { success = result.Success, message = result.Success ? "Instalment removed." : result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostDeletePaymentSchedule([FromBody] int scheduleId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

            if (!await HasSchedulePaymentsAccessAsync(userId, businessId))
                return Json(new { success = false, message = "You do not have permission to manage payment schedules." });

            var result = await _paymentScheduleService.DeleteScheduleAsync(scheduleId, businessId, userId);
            return Json(new { success = result.Success, message = result.Success ? "Payment schedule deleted." : result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetPaymentSchedule(int invoiceId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var schedule = await _paymentScheduleService.GetScheduleByInvoiceIdAsync(invoiceId, businessId);
            return Json(new { success = true, data = schedule });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetScheduleHistory(int scheduleId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var history = await _paymentScheduleService.GetScheduleHistoryAsync(scheduleId, businessId);
            return Json(new { success = true, data = history });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetVatWarning(int invoiceId, string? firstDueDate, decimal firstAmount)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            DateOnly? parsedDueDate = null;
            if (!string.IsNullOrWhiteSpace(firstDueDate))
                parsedDueDate = DateOnly.Parse(firstDueDate);

            var warning = await _paymentScheduleService.GetVatWarningAsync(invoiceId, parsedDueDate, firstAmount, businessId);
            return Json(new { success = true, data = warning });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }
}
