using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;
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

    public RevenueController(
        IPaymentService paymentService,
        IDashboardService dashboardService,
        IReceivablesQueryService receivablesQueryService,
        IVatIntegrationService vatIntegrationService,
        ICurrentTenantService tenantService,
        ICustomerService customerService,
        IInvoiceService invoiceService,
        IBusinessService businessService,
        IPermissionService permissionService)
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
        if (!string.IsNullOrEmpty(userId))
        {
            var permissions = await _permissionService.GetAllAccessLevelsAsync(userId, businessId);
            var hasAutoAccess = permissions.TryGetValue("payment_reminder_auto", out var level) && level != "none";
            showReminderTeaser = !hasAutoAccess;
        }
        ViewBag.ShowPaymentReminderTeaser = showReminderTeaser;

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

        return View();
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
        catch (Exception)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }
}
