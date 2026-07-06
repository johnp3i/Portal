using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Models;
using Portal.Web.Security;
using Portal.Web.Services;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.Invoice)]
public class InvoiceController : Controller
{
    private readonly IInvoiceService _invoiceService;
    private readonly IInvoiceSectionService _sectionService;
    private readonly ICurrentTenantService _tenantService;
    private readonly ICustomerService _customerService;
    private readonly ILogoService _logoService;
    private readonly IBusinessService _businessService;
    private readonly BusinessPaymentDetailRepository _paymentDetailRepository;
    private readonly IInvoiceSharingService _sharingService;
    private readonly IInvoiceAcceptanceService _acceptanceService;
    private readonly IDocumentDuplicationService _duplicationService;
    private readonly IDocumentSoftDeleteService _softDeleteService;
    private readonly VatSubmissionPeriodRepository _vatPeriodRepository;
    private readonly IViewRenderService _viewRenderService;
    private readonly IInvoicePdfService _invoicePdfService;
    private readonly IPaymentInstructionsService _paymentInstructionsService;
    private readonly PortalDbContext _dbContext;
    private readonly ILogger<InvoiceController> _logger;

    public InvoiceController(
        IInvoiceService invoiceService,
        IInvoiceSectionService sectionService,
        ICurrentTenantService tenantService,
        ICustomerService customerService,
        ILogoService logoService,
        IBusinessService businessService,
        BusinessPaymentDetailRepository paymentDetailRepository,
        IInvoiceSharingService sharingService,
        IInvoiceAcceptanceService acceptanceService,
        IDocumentDuplicationService duplicationService,
        IDocumentSoftDeleteService softDeleteService,
        VatSubmissionPeriodRepository vatPeriodRepository,
        IViewRenderService viewRenderService,
        IInvoicePdfService invoicePdfService,
        IPaymentInstructionsService paymentInstructionsService,
        PortalDbContext dbContext,
        ILogger<InvoiceController> logger)
    {
        _invoiceService = invoiceService;
        _sectionService = sectionService;
        _tenantService = tenantService;
        _customerService = customerService;
        _logoService = logoService;
        _businessService = businessService;
        _paymentDetailRepository = paymentDetailRepository;
        _sharingService = sharingService;
        _acceptanceService = acceptanceService;
        _duplicationService = duplicationService;
        _softDeleteService = softDeleteService;
        _vatPeriodRepository = vatPeriodRepository;
        _viewRenderService = viewRenderService;
        _invoicePdfService = invoicePdfService;
        _paymentInstructionsService = paymentInstructionsService;
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? status, int? financialStatus, int? customer, string? search, int? vatPeriodId, int page = 1)
    {
        var pagedResult = await _invoiceService.GetInvoicesPagedAsync(status, financialStatus, customer, search, page, vatPeriodId: vatPeriodId);
        var customers = await _customerService.GetCustomersAsync(null, true);
        var profile = await _businessService.GetBusinessProfileAsync(_tenantService.CurrentBusinessId);
        var vatPeriods = await _vatPeriodRepository.GetAllByBusinessIdAsync(_tenantService.CurrentBusinessId);

        // Load acceptance status for invoices that have active shares
        var invoiceIds = pagedResult.Items.Select(i => i.Id).ToList();
        var activeShares = new Dictionary<int, int>(); // invoiceId → shareId
        foreach (var invoiceId in invoiceIds)
        {
            var share = await _sharingService.GetActiveShareByInvoiceIdAsync(invoiceId);
            if (share != null)
                activeShares[invoiceId] = share.Id;
        }

        if (activeShares.Count > 0)
        {
            var acceptedShareIds = await _acceptanceService.GetAcceptedShareIdsAsync(activeShares.Values);
            foreach (var item in pagedResult.Items)
            {
                if (activeShares.TryGetValue(item.Id, out var shareId))
                {
                    item.AcceptanceStatus = acceptedShareIds.Contains(shareId) ? "accepted" : "awaiting";
                }
            }
        }

        ViewBag.PagedResult = pagedResult;
        ViewBag.SearchTerm = search;
        ViewBag.StatusFilter = status;
        ViewBag.FinancialStatusFilter = financialStatus;
        ViewBag.CustomerFilter = customer;
        ViewBag.Customers = customers;
        ViewBag.CurrencySymbol = profile?.CurrencySymbol ?? "€";
        ViewBag.VatPeriods = vatPeriods;
        ViewBag.VatPeriodFilter = vatPeriodId;
        ViewBag.Statuses = new List<InvoiceStatusType>
        {
            new() { Id = 1, Name = "Draft" },
            new() { Id = 2, Name = "Issued" },
            new() { Id = 3, Name = "Cancelled" }
        };
        ViewBag.FinancialStatuses = new List<InvoiceFinancialStatusType>
        {
            new() { Id = 1, Name = "Unpaid" },
            new() { Id = 2, Name = "PartiallyPaid" },
            new() { Id = 3, Name = "Paid" },
            new() { Id = 4, Name = "Overdue" },
            new() { Id = 5, Name = "WrittenOff" }
        };

        return View(pagedResult.Items);
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(int? status, int? financialStatus, int? customer, string? search, int? vatPeriodId)
    {
        var invoices = await _invoiceService.GetInvoicesFilteredAsync(status, financialStatus, customer, search, vatPeriodId);
        var profile = await _businessService.GetBusinessProfileAsync(_tenantService.CurrentBusinessId);
        var currencySymbol = profile?.CurrencySymbol ?? "€";

        var csv = new StringBuilder();
        csv.AppendLine("Invoice Number,Customer,Invoice Date,Due Date,Total Amount,Status,Financial Status");
        foreach (var inv in invoices)
        {
            csv.AppendLine($"\"{inv.InvoiceNumber}\",\"{inv.CustomerName}\",{inv.InvoiceDate:yyyy-MM-dd},{inv.DueDate:yyyy-MM-dd},{inv.TotalAmount:F2},\"{inv.StatusName}\",\"{inv.FinancialStatusName}\"");
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv", $"invoices-export-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
    }

    [HttpGet]
    public async Task<IActionResult> ExportPdf(int? status, int? financialStatus, int? customer, string? search, int? vatPeriodId)
    {
        var invoices = await _invoiceService.GetInvoicesFilteredAsync(status, financialStatus, customer, search, vatPeriodId);
        var profile = await _businessService.GetBusinessProfileAsync(_tenantService.CurrentBusinessId);
        var currencySymbol = profile?.CurrencySymbol ?? "€";

        var model = new InvoiceExportPdfModel
        {
            Invoices = invoices,
            CurrencySymbol = currencySymbol,
            GeneratedAt = DateTime.Now
        };

        var html = await _viewRenderService.RenderViewToStringAsync("~/Views/Invoice/_ExportPdf.cshtml", model);

        await new BrowserFetcher().DownloadAsync();

        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
        });

        await using var page = await browser.NewPageAsync();
        await page.SetContentAsync(html, new NavigationOptions
        {
            WaitUntil = new[] { WaitUntilNavigation.Networkidle0 }
        });

        var pdfBytes = await page.PdfDataAsync(new PdfOptions
        {
            Landscape = true,
            Format = PaperFormat.A4,
            PrintBackground = true,
            MarginOptions = new MarginOptions
            {
                Top = "10mm",
                Bottom = "10mm",
                Left = "10mm",
                Right = "10mm"
            }
        });

        return File(pdfBytes, "application/pdf", $"invoices-export-{DateTime.Now:yyyyMMdd-HHmmss}.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var invoice = await _invoiceService.GetInvoiceByIdAsync(id);
        if (invoice == null) return NotFound();

        var lines = await _invoiceService.GetInvoiceLinesAsync(id);
        var sections = await _sectionService.GetByInvoiceIdAsync(id);
        var customer = await _customerService.GetCustomerByIdAsync(invoice.CustomerId);

        var statusNames = new Dictionary<int, string>
        {
            { 1, "Draft" }, { 2, "Issued" }, { 3, "Cancelled" }
        };

        var financialStatusNames = new Dictionary<int, string>
        {
            { 1, "Unpaid" }, { 2, "PartiallyPaid" }, { 3, "Paid" },
            { 4, "Overdue" }, { 5, "WrittenOff" }, { 6, "PaymentOnboard" }
        };

        ViewBag.Invoice = invoice;
        ViewBag.Lines = lines;
        ViewBag.Sections = sections;
        ViewBag.CustomerName = customer?.Name ?? "Unknown";
        ViewBag.CustomerEmail = customer?.Email ?? "";
        ViewBag.StatusName = statusNames.GetValueOrDefault(invoice.InvoiceStatusTypeId, "Unknown");
        ViewBag.FinancialStatusName = financialStatusNames.GetValueOrDefault(invoice.InvoiceFinancialStatusTypeId, "Unknown");

        var profile = await _businessService.GetBusinessProfileAsync(_tenantService.CurrentBusinessId);
        ViewBag.CurrencySymbol = profile?.CurrencySymbol ?? "€";

        // Load VAT period label separately (navigation property is not eager-loaded by raw SQL query)
        if (invoice.VatSubmissionPeriodId.HasValue)
        {
            var vatPeriod = await _vatPeriodRepository.GetByIdAndBusinessIdAsync(
                invoice.VatSubmissionPeriodId.Value, _tenantService.CurrentBusinessId);
            if (vatPeriod != null)
                invoice.VatSubmissionPeriod = vatPeriod;
        }

        // Load acceptance status for the invoice share
        var activeShare = await _sharingService.GetActiveShareByInvoiceIdAsync(id);
        if (activeShare != null)
        {
            var acceptance = await _acceptanceService.GetByInvoiceShareIdAsync(activeShare.Id);
            if (acceptance != null)
            {
                ViewBag.AcceptanceStatus = "accepted";
                ViewBag.AcceptedAtUtc = acceptance.AcceptedAtUtc;
            }
            else
            {
                ViewBag.AcceptanceStatus = "awaiting";
            }
        }
        else
        {
            ViewBag.AcceptanceStatus = null;
        }

        // Payment Instructions override state for per-invoice toggle
        ViewBag.IsBusinessPaymentInstructionsEnabled = await _paymentInstructionsService.IsEnabledForBusinessAsync(_tenantService.CurrentBusinessId);

        return View(invoice);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var customers = await _customerService.GetCustomersAsync(null, true);
        ViewBag.Customers = customers;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int customerId, DateOnly invoiceDate, DateOnly dueDate,
        string? notes, bool isGrandTotalShown, List<CreateInvoiceLineDto> lines,
        List<CreateInvoiceSectionDto>? sections)
    {
        try
        {
            var invoice = await _invoiceService.CreateInvoiceAsync(customerId, invoiceDate, dueDate,
                notes, isGrandTotalShown, lines, sections);
            return RedirectToAction(nameof(Detail), new { id = invoice.Id });
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var customers = await _customerService.GetCustomersAsync(null, true);
            ViewBag.Customers = customers;
            return View();
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Preview(int id, bool print = false)
    {
        var invoice = await _invoiceService.GetInvoiceByIdAsync(id);
        if (invoice == null) return NotFound();

        var lines = await _invoiceService.GetInvoiceLinesAsync(id);
        var sections = await _sectionService.GetByInvoiceIdAsync(id);
        var customer = await _customerService.GetCustomerByIdAsync(invoice.CustomerId);
        var logos = await _logoService.GetByBusinessIdAsync(_tenantService.CurrentBusinessId);
        var primaryLogo = logos.FirstOrDefault(l => l.IsPrimary) ?? logos.FirstOrDefault();
        var business = await _businessService.GetBusinessByIdAsync(_tenantService.CurrentBusinessId);
        var profile = await _businessService.GetBusinessProfileAsync(_tenantService.CurrentBusinessId);
        var paymentDetails = await _paymentDetailRepository.GetByBusinessIdAsync(_tenantService.CurrentBusinessId);

        ViewBag.Invoice = invoice;
        ViewBag.Lines = lines;
        ViewBag.Sections = sections;
        ViewBag.CustomerName = customer?.Name ?? "Unknown";
        ViewBag.LogoUrl = primaryLogo?.PublicUrl;
        ViewBag.BusinessName = business?.Name ?? "";
        ViewBag.Profile = profile;
        ViewBag.PaymentDetails = paymentDetails;
        ViewBag.AutoPrint = print;

        return View(invoice);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var invoice = await _invoiceService.GetInvoiceByIdAsync(id);
        if (invoice == null) return NotFound();

        if (invoice.InvoiceStatusTypeId != 1)
        {
            return RedirectToAction(nameof(Detail), new { id });
        }

        var lines = await _invoiceService.GetInvoiceLinesAsync(id);
        var sections = await _sectionService.GetByInvoiceIdAsync(id);
        var customers = await _customerService.GetCustomersAsync(null, true);

        ViewBag.Lines = lines;
        ViewBag.Sections = sections;
        ViewBag.Customers = customers;

        return View(invoice);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string invoiceNumber, int customerId, DateOnly invoiceDate, DateOnly dueDate,
        string? notes, bool isGrandTotalShown, bool isQuotationReferenceShown)
    {
        try
        {
            await _invoiceService.UpdateInvoiceAsync(id, customerId, invoiceDate, dueDate, notes, isGrandTotalShown, isQuotationReferenceShown, invoiceNumber);
            return RedirectToAction(nameof(Detail), new { id });
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var invoice = await _invoiceService.GetInvoiceByIdAsync(id);
            if (invoice == null) return NotFound();

            var lines = await _invoiceService.GetInvoiceLinesAsync(id);
            var sections = await _sectionService.GetByInvoiceIdAsync(id);
            var customers = await _customerService.GetCustomersAsync(null, true);

            ViewBag.Lines = lines;
            ViewBag.Sections = sections;
            ViewBag.Customers = customers;

            return View(invoice);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Detail), new { id });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConvertFromQuotation(int quotationId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? string.Empty;
            var invoice = await _invoiceService.ConvertFromQuotationAsync(quotationId, userId);
            return RedirectToAction(nameof(Detail), new { id = invoice.Id });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return Redirect($"/Quotation/Detail/{quotationId}");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duplicate(int id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var duplicate = await _duplicationService.DuplicateInvoiceAsync(id, userId);
            return Json(new { success = true, redirectUrl = Url.Action("Detail", new { id = duplicate.Id }) });
        }
        catch (InvalidOperationException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TransitionStatus(int invoiceId, int newStatusId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? string.Empty;
            await _invoiceService.TransitionStatusAsync(invoiceId, newStatusId, userId);
            return Json(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddLine(int invoiceId, string description, decimal quantity,
        decimal unitPrice, decimal vatRate, decimal discount, string discountType,
        decimal? costPrice, string? referenceUrl, string? subtitle, int? invoiceSectionId,
        string? productCode = null, bool isReverseCharge = false)
    {
        try
        {
            var line = await _invoiceService.AddLineAsync(invoiceId, description, quantity,
                unitPrice, vatRate, discount, discountType, costPrice, referenceUrl, subtitle, invoiceSectionId,
                productCode, isReverseCharge: isReverseCharge);
            return Json(new { success = true, lineId = line.Id });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateLine(int lineId, string description, decimal quantity,
        decimal unitPrice, decimal vatRate, decimal discount, string discountType,
        decimal? costPrice, string? referenceUrl, string? subtitle, int? invoiceSectionId,
        bool isReverseCharge = false)
    {
        try
        {
            await _invoiceService.UpdateLineAsync(lineId, description, quantity,
                unitPrice, vatRate, discount, discountType, costPrice, referenceUrl, subtitle, invoiceSectionId,
                isReverseCharge: isReverseCharge);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveLine(int lineId)
    {
        try
        {
            await _invoiceService.RemoveLineAsync(lineId);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSection(int invoiceId, string name, string? description,
        string columnConfiguration, string sectionType, bool isEmphasized,
        string? accentColor, string? label, bool isTotalsTableShown)
    {
        try
        {
            await _sectionService.AddSectionAsync(invoiceId, name, description,
                columnConfiguration, sectionType, isEmphasized, accentColor, label, isTotalsTableShown);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSection(int sectionId, string name, string? description,
        string? notes, string? columnConfiguration, string? sectionType, bool? isEmphasized,
        string? accentColor, string? label, bool? isTotalsTableShown)
    {
        try
        {
            await _sectionService.UpdateSectionAsync(sectionId, name, description, notes,
                columnConfiguration, sectionType, isEmphasized, accentColor, label, isTotalsTableShown);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSection(int sectionId, int invoiceId)
    {
        try
        {
            await _sectionService.RemoveSectionAsync(sectionId, invoiceId);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReorderSections(int invoiceId, List<int> orderedSectionIds)
    {
        await _sectionService.ReorderSectionsAsync(invoiceId, orderedSectionIds);
        return Json(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveLineToSection(int lineId, int? targetSectionId)
    {
        await _sectionService.MoveLineToSectionAsync(lineId, targetSectionId);
        return Json(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReorderLines(List<int> orderedLineIds)
    {
        await _sectionService.ReorderLinesAsync(orderedLineIds);
        return Json(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Share(int invoiceId, DateTimeOffset? expiresAtUtc, bool sendEmail = false, string? recipientEmail = null)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.Identity?.Name ?? string.Empty;

            var expiration = expiresAtUtc ?? DateTimeOffset.UtcNow.AddDays(7);

            var share = await _sharingService.ShareAsync(invoiceId, expiration, sendEmail, userId, recipientEmail);
            var shareUrl = $"/invoice-view/{share.ShareToken}";

            return Json(new { success = true, shareUrl, token = share.ShareToken, expiresAt = share.ExpiresAtUtc });
        }
        catch (ArgumentException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetActiveShare(int invoiceId)
    {
        var share = await _sharingService.GetActiveShareByInvoiceIdAsync(invoiceId);
        if (share == null)
            return Json(new { hasActiveShare = false });

        return Json(new {
            hasActiveShare = true,
            shareUrl = $"/invoice-view/{share.ShareToken}",
            expiresAt = share.ExpiresAtUtc,
            customerEmail = share.CustomerEmail
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ModuleAccess(PortalModules.Invoice, AccessLevels.Full)]
    public async Task<IActionResult> SoftDelete(int id)
    {
        try
        {
            var result = await _softDeleteService.SoftDeleteInvoiceAsync(id);

            if (result.Success)
                return Json(new { success = true, message = "Invoice deleted successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An unexpected error occurred while deleting the invoice." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ModuleAccess(PortalModules.Invoice, AccessLevels.Full)]
    public async Task<IActionResult> ReassignVatPeriod(int invoiceId, int targetPeriodId)
    {
        var result = await _invoiceService.ReassignVatPeriodAsync(invoiceId, targetPeriodId);
        return Json(new { success = result.Success, message = result.Message });
    }

    [HttpGet]
    public async Task<IActionResult> GetReassignmentImpact(int invoiceId, int targetPeriodId)
    {
        var result = await _invoiceService.GetReassignmentImpactAsync(invoiceId, targetPeriodId);
        if (!result.Success)
            return Json(new { success = false, message = result.Message });
        return Json(new { success = true, data = result.Data });
    }

    [HttpGet]
    public async Task<IActionResult> GetUnsubmittedPeriods(int invoiceId)
    {
        var periods = await _invoiceService.GetUnsubmittedPeriodsAsync(invoiceId);
        return Json(periods);
    }

    [HttpGet]
    public async Task<IActionResult> AxGetDownloadPdf(int id)
    {
        var invoice = await _invoiceService.GetInvoiceByIdAsync(id);
        if (invoice == null || invoice.BusinessId != _tenantService.CurrentBusinessId)
        {
            return NotFound();
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var pdfBytes = await _invoicePdfService.GenerateAsync(id, cts.Token);
            var filename = GenerateInvoicePdfFilename(invoice.InvoiceNumber);
            return File(pdfBytes, "application/pdf", filename);
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("PDF generation timed out for invoice {InvoiceId}", id);
            return StatusCode(500, new { success = false, message = "PDF generation timed out. Please try again." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate PDF for invoice {InvoiceId}", id);
            return StatusCode(500, new { success = false, message = "Failed to generate PDF. Please try again." });
        }
    }

    /// <summary>
    /// Sets the per-invoice payment instructions override.
    /// Value: null = follow business default, 1 = force show, 0 = force hide.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostSetPaymentInstructionsOverride(int invoiceId, byte? overrideValue)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var invoice = await _dbContext.Invoices
                .FirstOrDefaultAsync(i => i.Id == invoiceId && i.BusinessId == businessId);

            if (invoice == null)
                return Json(new { success = false, message = "Invoice not found." });

            invoice.PaymentInstructionsOverride = overrideValue;
            await _dbContext.SaveChangesAsync();

            return Json(new { success = true, message = "Payment instructions setting updated." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to update setting." });
        }
    }

    private static string GenerateInvoicePdfFilename(string invoiceNumber)
    {
        var invalidChars = new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
        var sanitized = new string(invoiceNumber.Where(c => !invalidChars.Contains(c)).ToArray());
        if (string.IsNullOrWhiteSpace(sanitized))
            return "INV-download.pdf";
        return $"INV-{sanitized}.pdf";
    }
}
