using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Security;
using Portal.Web.Services;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.Invoice)]
public class CreditNoteController : Controller
{
    private readonly ICreditNoteService _creditNoteService;
    private readonly ICurrentTenantService _tenantService;
    private readonly ICustomerService _customerService;
    private readonly IBusinessService _businessService;
    private readonly VatSubmissionPeriodRepository _vatPeriodRepository;
    private readonly ICreditNoteRenderer _creditNoteRenderer;
    private readonly ILogoService _logoService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<CreditNoteController> _logger;

    public CreditNoteController(
        ICreditNoteService creditNoteService,
        ICurrentTenantService tenantService,
        ICustomerService customerService,
        IBusinessService businessService,
        VatSubmissionPeriodRepository vatPeriodRepository,
        ICreditNoteRenderer creditNoteRenderer,
        ILogoService logoService,
        IWebHostEnvironment environment,
        ILogger<CreditNoteController> logger)
    {
        _creditNoteService = creditNoteService;
        _tenantService = tenantService;
        _customerService = customerService;
        _businessService = businessService;
        _vatPeriodRepository = vatPeriodRepository;
        _creditNoteRenderer = creditNoteRenderer;
        _logoService = logoService;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var businessId = _tenantService.CurrentBusinessId;

        var kpi = await _creditNoteService.GetKpiAsync(businessId);
        var customers = await _customerService.GetCustomersAsync(null, true);
        var profile = await _businessService.GetBusinessProfileAsync(businessId);

        ViewBag.Kpi = kpi;
        ViewBag.Customers = customers;
        ViewBag.CurrencySymbol = profile?.CurrencySymbol ?? "€";

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var businessId = _tenantService.CurrentBusinessId;

        var eligibleInvoices = await _creditNoteService.GetEligibleInvoicesAsync(businessId);
        var vatPeriods = await _vatPeriodRepository.GetUnsubmittedPeriodsFromAsync(
            businessId, DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2)));
        var profile = await _businessService.GetBusinessProfileAsync(businessId);

        ViewBag.EligibleInvoices = eligibleInvoices;
        ViewBag.VatPeriods = vatPeriods;
        ViewBag.DefaultVatPeriodId = vatPeriods.LastOrDefault()?.Id;
        ViewBag.CurrencySymbol = profile?.CurrencySymbol ?? "€";

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int invoiceId, DateOnly issueDate, string reason,
        int vatSubmissionPeriodId, List<CreateCreditNoteLineDto> lines)
    {
        var businessId = _tenantService.CurrentBusinessId;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var dto = new CreateCreditNoteDto
        {
            InvoiceId = invoiceId,
            IssueDate = issueDate,
            Reason = reason,
            VatSubmissionPeriodId = vatSubmissionPeriodId,
            Lines = lines ?? new List<CreateCreditNoteLineDto>()
        };

        var result = await _creditNoteService.CreateCreditNoteAsync(dto, businessId, userId);

        if (result.Success)
        {
            return RedirectToAction(nameof(Detail), new { id = result.Data });
        }

        // On failure, reload form data and return errors
        ModelState.AddModelError(string.Empty, result.Message ?? "An error occurred while creating the credit note.");

        var eligibleInvoices = await _creditNoteService.GetEligibleInvoicesAsync(businessId);
        var vatPeriods = await _vatPeriodRepository.GetUnsubmittedPeriodsFromAsync(
            businessId, DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2)));

        ViewBag.EligibleInvoices = eligibleInvoices;
        ViewBag.VatPeriods = vatPeriods;
        ViewBag.DefaultVatPeriodId = vatSubmissionPeriodId;

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var businessId = _tenantService.CurrentBusinessId;

        var detail = await _creditNoteService.GetCreditNoteDetailAsync(id, businessId);
        if (detail == null) return NotFound();

        var profile = await _businessService.GetBusinessProfileAsync(businessId);
        ViewBag.CurrencySymbol = profile?.CurrencySymbol ?? "€";

        return View(detail);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var businessId = _tenantService.CurrentBusinessId;

        var detail = await _creditNoteService.GetCreditNoteDetailAsync(id, businessId);
        if (detail == null) return NotFound();

        // Only Draft credit notes can be edited
        if (detail.CreditNoteStatusTypeId != 1)
        {
            return RedirectToAction(nameof(Detail), new { id });
        }

        var vatPeriods = await _vatPeriodRepository.GetUnsubmittedPeriodsFromAsync(
            businessId, DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2)));

        ViewBag.VatPeriods = vatPeriods;
        ViewBag.CreditNote = detail;

        return View(detail);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, DateOnly issueDate, string reason,
        int vatSubmissionPeriodId, List<CreateCreditNoteLineDto> lines)
    {
        var businessId = _tenantService.CurrentBusinessId;

        var dto = new UpdateCreditNoteDto
        {
            IssueDate = issueDate,
            Reason = reason,
            VatSubmissionPeriodId = vatSubmissionPeriodId,
            Lines = lines ?? new List<CreateCreditNoteLineDto>()
        };

        var result = await _creditNoteService.UpdateCreditNoteAsync(id, dto, businessId);

        if (result.Success)
        {
            return RedirectToAction(nameof(Detail), new { id });
        }

        // On failure, reload form data and return errors
        ModelState.AddModelError(string.Empty, result.Message ?? "An error occurred while updating the credit note.");

        var detail = await _creditNoteService.GetCreditNoteDetailAsync(id, businessId);
        if (detail == null) return NotFound();

        var vatPeriods = await _vatPeriodRepository.GetUnsubmittedPeriodsFromAsync(
            businessId, DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2)));

        ViewBag.VatPeriods = vatPeriods;
        ViewBag.CreditNote = detail;

        return View(detail);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Issue(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _creditNoteService.IssueCreditNoteAsync(id, businessId, userId);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _creditNoteService.ApplyCreditNoteAsync(id, businessId, userId);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Void(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _creditNoteService.VoidCreditNoteAsync(id, businessId, userId);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetInvoiceBalance(int invoiceId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var balance = await _creditNoteService.GetInvoiceOutstandingBalanceAsync(invoiceId, businessId);
            return Json(new { balance });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetEligibleInvoices()
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var invoices = await _creditNoteService.GetEligibleInvoicesAsync(businessId);
            return Json(invoices);
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetKpi()
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var kpi = await _creditNoteService.GetKpiAsync(businessId);
            return Json(kpi);
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> GetCreditNotesPaged(int? status, int? customerId,
        string? dateFrom, string? dateTo, string? search, int page = 1)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            var filter = new CreditNoteFilterDto
            {
                StatusId = status,
                CustomerId = customerId,
                FromDate = !string.IsNullOrWhiteSpace(dateFrom) && DateOnly.TryParse(dateFrom, out var from) ? from : null,
                ToDate = !string.IsNullOrWhiteSpace(dateTo) && DateOnly.TryParse(dateTo, out var to) ? to : null,
                SearchTerm = search,
                Page = page < 1 ? 1 : page
            };

            var (items, totalCount) = await _creditNoteService.GetCreditNotesPagedAsync(filter, businessId);

            return Json(new
            {
                items,
                totalCount,
                page = filter.Page,
                pageSize = filter.PageSize
            });
        }
        catch (Exception)
        {
            return Json(new { items = Array.Empty<object>(), totalCount = 0, page = 1, pageSize = 10 });
        }
    }

    [HttpGet]
    public async Task<IActionResult> PreviewPdf(int id)
    {
        var businessId = _tenantService.CurrentBusinessId;

        var detail = await _creditNoteService.GetCreditNoteDetailAsync(id, businessId);
        if (detail == null) return NotFound();

        // Only Issued or Applied credit notes can generate a PDF
        if (detail.CreditNoteStatusTypeId != 2 && detail.CreditNoteStatusTypeId != 3)
        {
            return Json(new { success = false, message = "PDF preview is only available for Issued or Applied credit notes." });
        }

        try
        {
            var business = await _businessService.GetBusinessByIdAsync(businessId);
            var profile = await _businessService.GetBusinessProfileAsync(businessId);
            var customer = await _customerService.GetCustomerByIdAsync(detail.CustomerId);
            var logos = await _logoService.GetByBusinessIdAsync(businessId);
            var primaryLogo = logos.FirstOrDefault(l => l.IsPrimary) ?? logos.FirstOrDefault();

            var customerAddressParts = new List<string>();
            if (customer != null)
            {
                if (!string.IsNullOrWhiteSpace(customer.AddressLine1)) customerAddressParts.Add(customer.AddressLine1);
                if (!string.IsNullOrWhiteSpace(customer.AddressLine2)) customerAddressParts.Add(customer.AddressLine2);
                if (!string.IsNullOrWhiteSpace(customer.City)) customerAddressParts.Add(customer.City);
                if (!string.IsNullOrWhiteSpace(customer.PostalCode)) customerAddressParts.Add(customer.PostalCode);
                if (!string.IsNullOrWhiteSpace(customer.Country)) customerAddressParts.Add(customer.Country);
            }

            var businessAddressParts = new List<string>();
            if (profile != null)
            {
                if (!string.IsNullOrWhiteSpace(profile.AddressLine1)) businessAddressParts.Add(profile.AddressLine1);
                if (!string.IsNullOrWhiteSpace(profile.AddressLine2)) businessAddressParts.Add(profile.AddressLine2);
                if (!string.IsNullOrWhiteSpace(profile.City)) businessAddressParts.Add(profile.City);
                if (!string.IsNullOrWhiteSpace(profile.PostalCode)) businessAddressParts.Add(profile.PostalCode);
                if (!string.IsNullOrWhiteSpace(profile.Country)) businessAddressParts.Add(profile.Country);
            }

            var pdfModel = new CreditNotePdfModel
            {
                BusinessName = business?.Name ?? string.Empty,
                BusinessAddress = businessAddressParts.Count > 0 ? string.Join(", ", businessAddressParts) : null,
                BusinessVatNumber = profile?.VatRegistrationNumber,
                BusinessLogoUrl = GetLogoAsDataUri(primaryLogo),
                CustomerName = detail.CustomerName,
                CustomerAddress = customerAddressParts.Count > 0 ? string.Join(", ", customerAddressParts) : null,
                CreditNoteNumber = detail.CreditNoteNumber,
                IssueDate = detail.IssueDate,
                InvoiceNumber = detail.InvoiceNumber,
                Reason = detail.Reason,
                CurrencySymbol = profile?.CurrencySymbol ?? "€",
                Subtotal = detail.Subtotal,
                TaxAmount = detail.TaxAmount,
                TotalAmount = detail.TotalAmount,
                Lines = detail.Lines
            };

            var html = await _creditNoteRenderer.RenderAsync(pdfModel);

            byte[] pdfBytes;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                pdfBytes = await GeneratePdfFromHtmlAsync(html, cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogError("PDF generation timed out for credit note {CreditNoteId}", id);
                return Json(new { success = false, message = "PDF generation timed out. Please try again." });
            }

            var filename = $"CreditNote_{detail.CreditNoteNumber}.pdf";
            return File(pdfBytes, "application/pdf", filename);
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("PDF generation timed out for credit note {CreditNoteId}", id);
            return Json(new { success = false, message = "PDF generation timed out. Please try again." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate PDF for credit note {CreditNoteId}", id);
            return Json(new { success = false, message = "Failed to generate PDF. Please try again." });
        }
    }

    #region Private Helpers

    private string? GetLogoAsDataUri(Infrastructure.Entities.BusinessLogo? logo)
    {
        if (logo == null || string.IsNullOrWhiteSpace(logo.PublicUrl))
            return null;

        try
        {
            var relativePath = logo.PublicUrl.TrimStart('/');
            var filePath = Path.Combine(_environment.WebRootPath, relativePath);

            if (!System.IO.File.Exists(filePath))
                return null;

            var bytes = System.IO.File.ReadAllBytes(filePath);
            var base64 = Convert.ToBase64String(bytes);
            var contentType = logo.ContentType ?? "image/png";

            return $"data:{contentType};base64,{base64}";
        }
        catch
        {
            return null;
        }
    }

    private static async Task<byte[]> GeneratePdfFromHtmlAsync(string html, CancellationToken cancellationToken)
    {
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

        cancellationToken.ThrowIfCancellationRequested();

        return pdfBytes;
    }

    #endregion
}
