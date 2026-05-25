using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;
using Portal.Web.Security;
using Portal.Web.Services;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.Revenue)]
public class StatementController : Controller
{
    private readonly IStatementService _statementService;
    private readonly ICustomerService _customerService;
    private readonly ICurrentTenantService _tenantService;
    private readonly IBusinessService _businessService;
    private readonly IStatementRenderer _statementRenderer;
    private readonly IEmailService _emailService;
    private readonly ILogoService _logoService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<StatementController> _logger;

    public StatementController(
        IStatementService statementService,
        ICustomerService customerService,
        ICurrentTenantService tenantService,
        IBusinessService businessService,
        IStatementRenderer statementRenderer,
        IEmailService emailService,
        ILogoService logoService,
        IWebHostEnvironment environment,
        ILogger<StatementController> logger)
    {
        _statementService = statementService;
        _customerService = customerService;
        _tenantService = tenantService;
        _businessService = businessService;
        _statementRenderer = statementRenderer;
        _emailService = emailService;
        _logoService = logoService;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? customerId)
    {
        var customers = await _customerService.GetCustomersAsync(null, true);
        var orderedCustomers = customers.OrderBy(c => c.Name).ToList();

        ViewBag.Customers = orderedCustomers;
        ViewBag.SelectedCustomerId = customerId;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(int? customerId, string? fromDate, string? toDate)
    {
        if (!customerId.HasValue)
        {
            return Json(new { success = false, message = "Please select a customer." });
        }

        if (string.IsNullOrWhiteSpace(fromDate) || string.IsNullOrWhiteSpace(toDate))
        {
            return Json(new { success = false, message = "Both from and to dates are required." });
        }

        if (!DateOnly.TryParse(fromDate, out var parsedFromDate) || !DateOnly.TryParse(toDate, out var parsedToDate))
        {
            return Json(new { success = false, message = "Both from and to dates are required." });
        }

        if (parsedFromDate > parsedToDate)
        {
            return Json(new { success = false, message = "From date cannot be after to date." });
        }

        var businessId = _tenantService.CurrentBusinessId;
        var customer = await _customerService.GetCustomerByIdAsync(customerId.Value);

        if (customer == null || customer.BusinessId != businessId)
        {
            return Json(new { success = false, message = "Customer not found." });
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? string.Empty;
        var result = await _statementService.GenerateStatementAsync(customerId.Value, parsedFromDate, parsedToDate, businessId, userId);

        return Json(new
        {
            success = true,
            customerEmail = customer.Email ?? string.Empty,
            openingBalance = result.OpeningBalance,
            closingBalance = result.ClosingBalance,
            totalInvoiced = result.TotalInvoiced,
            totalPaid = result.TotalPaid,
            invoiceCount = result.InvoiceCount,
            paymentCount = result.PaymentCount,
            lines = result.Lines.Select(l => new
            {
                date = l.Date.ToString("yyyy-MM-dd"),
                type = l.Type.ToString(),
                reference = l.Reference,
                description = l.Description,
                debit = l.Debit,
                credit = l.Credit,
                runningBalance = l.RunningBalance
            })
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DownloadPdf(int customerId, string fromDate, string toDate)
    {
        var businessId = _tenantService.CurrentBusinessId;
        var customer = await _customerService.GetCustomerByIdAsync(customerId);

        if (customer == null || customer.BusinessId != businessId)
        {
            return Json(new { success = false, message = "Customer not found." });
        }

        if (!DateOnly.TryParse(fromDate, out var parsedFromDate) || !DateOnly.TryParse(toDate, out var parsedToDate))
        {
            return Json(new { success = false, message = "Both from and to dates are required." });
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? string.Empty;

        try
        {
            var result = await _statementService.GenerateStatementAsync(customerId, parsedFromDate, parsedToDate, businessId, userId);

            var business = await _businessService.GetBusinessByIdAsync(businessId);
            var profile = await _businessService.GetBusinessProfileAsync(businessId);
            var logos = await _logoService.GetByBusinessIdAsync(businessId);
            var primaryLogo = logos.FirstOrDefault(l => l.IsPrimary) ?? logos.FirstOrDefault();

            var pdfModel = BuildStatementPdfModel(customer, business, profile, GetLogoAsDataUri(primaryLogo), parsedFromDate, parsedToDate, result);

            var html = await _statementRenderer.RenderAsync(pdfModel);

            byte[] pdfBytes;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                pdfBytes = await GeneratePdfFromHtmlAsync(html, cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogError("PDF generation timed out for customer {CustomerId}, period {FromDate} to {ToDate}", customerId, parsedFromDate, parsedToDate);
                return Json(new { success = false, message = "PDF generation timed out. Please try again." });
            }

            var filename = GenerateFilename(customer.Name, parsedFromDate, parsedToDate);

            await _statementService.LogPdfDownloadAsync(customerId, parsedFromDate, parsedToDate, businessId, userId);

            return File(pdfBytes, "application/pdf", filename);
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("PDF generation timed out for customer {CustomerId}, period {FromDate} to {ToDate}", customerId, parsedFromDate, parsedToDate);
            return Json(new { success = false, message = "PDF generation timed out. Please try again." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate PDF for customer {CustomerId}, period {FromDate} to {ToDate}", customerId, parsedFromDate, parsedToDate);
            return Json(new { success = false, message = "Failed to generate PDF." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EmailStatement(int customerId, string fromDate, string toDate, string? recipientEmail)
    {
        var businessId = _tenantService.CurrentBusinessId;
        var customer = await _customerService.GetCustomerByIdAsync(customerId);

        if (customer == null || customer.BusinessId != businessId)
        {
            return Json(new { success = false, message = "Customer not found." });
        }

        // Determine the target email: use provided alternative, or fall back to customer's registered email
        var targetEmail = !string.IsNullOrWhiteSpace(recipientEmail) ? recipientEmail.Trim() : customer.Email;

        if (string.IsNullOrWhiteSpace(targetEmail))
        {
            return Json(new { success = false, message = "No email address is registered for this customer." });
        }

        // Basic email format validation
        if (!targetEmail.Contains('@') || !targetEmail.Contains('.'))
        {
            return Json(new { success = false, message = "Please enter a valid email address." });
        }

        if (!DateOnly.TryParse(fromDate, out var parsedFromDate) || !DateOnly.TryParse(toDate, out var parsedToDate))
        {
            return Json(new { success = false, message = "Both from and to dates are required." });
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? string.Empty;

        try
        {
            var result = await _statementService.GenerateStatementAsync(customerId, parsedFromDate, parsedToDate, businessId, userId);

            var business = await _businessService.GetBusinessByIdAsync(businessId);
            var profile = await _businessService.GetBusinessProfileAsync(businessId);
            var logos = await _logoService.GetByBusinessIdAsync(businessId);
            var primaryLogo = logos.FirstOrDefault(l => l.IsPrimary) ?? logos.FirstOrDefault();

            var pdfModel = BuildStatementPdfModel(customer, business, profile, GetLogoAsDataUri(primaryLogo), parsedFromDate, parsedToDate, result);

            var html = await _statementRenderer.RenderAsync(pdfModel);

            byte[] pdfBytes;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                pdfBytes = await GeneratePdfFromHtmlAsync(html, cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogError("PDF generation timed out for email statement, customer {CustomerId}, period {FromDate} to {ToDate}", customerId, parsedFromDate, parsedToDate);
                return Json(new { success = false, message = "PDF generation timed out. Please try again." });
            }

            var filename = GenerateFilename(customer.Name, parsedFromDate, parsedToDate);
            var businessName = business?.Name ?? "Portal";

            await _emailService.SendStatementEmailAsync(targetEmail, customer.Name, businessName, pdfBytes, filename);

            await _statementService.LogEmailSentAsync(customerId, parsedFromDate, parsedToDate, targetEmail, businessId, userId);

            return Json(new { success = true, message = "Statement emailed successfully." });
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("PDF generation timed out for email statement, customer {CustomerId}", customerId);
            return Json(new { success = false, message = "PDF generation timed out. Please try again." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send statement email for customer {CustomerId}, period {FromDate} to {ToDate}", customerId, parsedFromDate, parsedToDate);
            return Json(new { success = false, message = "Failed to send email. Please try again." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetEmailHistory(int customerId)
    {
        if (customerId <= 0)
        {
            return Json(new { success = false, message = "Please select a customer." });
        }

        var businessId = _tenantService.CurrentBusinessId;
        var customer = await _customerService.GetCustomerByIdAsync(customerId);

        if (customer == null || customer.BusinessId != businessId)
        {
            return Json(new { success = false, message = "Customer not found." });
        }

        try
        {
            var history = await _statementService.GetEmailHistoryAsync(customerId, businessId);

            return Json(new
            {
                success = true,
                records = history.Select(h => new
                {
                    sentAtUtc = h.SentAtUtc.ToString("yyyy-MM-dd HH:mm"),
                    fromDate = h.FromDate.ToString("yyyy-MM-dd"),
                    toDate = h.ToDate.ToString("yyyy-MM-dd"),
                    recipientEmail = h.RecipientEmail,
                    sentByDisplayName = h.SentByDisplayName
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load email history for customer {CustomerId}", customerId);
            return Json(new { success = true, records = Array.Empty<object>() });
        }
    }

    #region Private Helpers

    private static StatementPdfModel BuildStatementPdfModel(
        Infrastructure.Entities.Customer customer,
        Infrastructure.Entities.Business? business,
        Infrastructure.Entities.BusinessProfile? profile,
        string? logoDataUri,
        DateOnly fromDate,
        DateOnly toDate,
        StatementResultDto result)
    {
        var addressParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(customer.AddressLine1)) addressParts.Add(customer.AddressLine1);
        if (!string.IsNullOrWhiteSpace(customer.AddressLine2)) addressParts.Add(customer.AddressLine2);
        if (!string.IsNullOrWhiteSpace(customer.City)) addressParts.Add(customer.City);
        if (!string.IsNullOrWhiteSpace(customer.PostalCode)) addressParts.Add(customer.PostalCode);
        if (!string.IsNullOrWhiteSpace(customer.Country)) addressParts.Add(customer.Country);

        return new StatementPdfModel
        {
            CustomerName = customer.Name,
            CustomerAddress = addressParts.Count > 0 ? string.Join(", ", addressParts) : null,
            CustomerEmail = customer.Email,
            CustomerPhone = customer.TelephoneNumber ?? customer.MobileNumber,
            BusinessName = business?.Name ?? string.Empty,
            BusinessLogoUrl = logoDataUri,
            CurrencySymbol = profile?.CurrencySymbol ?? "€",
            FromDate = fromDate,
            ToDate = toDate,
            Statement = result
        };
    }

    private string? GetLogoAsDataUri(Infrastructure.Entities.BusinessLogo? logo)
    {
        if (logo == null || string.IsNullOrWhiteSpace(logo.PublicUrl))
            return null;

        try
        {
            // PublicUrl is like "/uploads/logos/{filename}" — resolve to physical path
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

        cancellationToken.ThrowIfCancellationRequested();

        return pdfBytes;
    }

    private static string GenerateFilename(string customerName, DateOnly fromDate, DateOnly toDate)
    {
        var sanitizedName = customerName.Replace(' ', '_');

        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars)
        {
            sanitizedName = sanitizedName.Replace(c.ToString(), string.Empty);
        }

        return $"Statement_{sanitizedName}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.pdf";
    }

    #endregion
}
