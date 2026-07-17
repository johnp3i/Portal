using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Models.Receipt;
using Portal.Infrastructure.Services;
using Portal.Web.Security;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.Revenue)]
public class ReceiptController : Controller
{
    private readonly IPaymentReceiptService _receiptService;
    private readonly ICurrentTenantService _tenantService;
    private readonly PortalDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public ReceiptController(
        IPaymentReceiptService receiptService,
        ICurrentTenantService tenantService,
        PortalDbContext dbContext,
        IConfiguration configuration)
    {
        _receiptService = receiptService;
        _tenantService = tenantService;
        _dbContext = dbContext;
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var receipt = await _receiptService.GetReceiptAsync(id, businessId);
            if (receipt == null) return NotFound();
            return View(receipt);
        }
        catch (Exception ex)
        {
            return RedirectToAction("Index");
        }
    }

    [HttpPost]
    public async Task<IActionResult> AxPostGenerate(int paymentId, int? signatureId, string? notes)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[ReceiptController] AxPostGenerate called: paymentId={paymentId}, signatureId={signatureId}, notes={notes}");

            if (paymentId <= 0)
                return Json(new { success = false, message = "Invalid request. Payment ID is required. Received: " + paymentId });

            var businessId = _tenantService.CurrentBusinessId;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            var result = await _receiptService.GenerateReceiptAsync(
                paymentId, businessId, userId, signatureId, notes);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            return Json(new
            {
                success = true,
                message = "Receipt generated successfully.",
                receiptId = result.Data?.Id ?? 0,
                receiptNumber = result.Data?.ReceiptNumber ?? ""
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to generate receipt: " + ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetList(int? customerId, DateTime? fromDate, DateTime? toDate, bool? isVoided, int page = 1, int pageSize = 15)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var (items, totalCount) = await _receiptService.GetReceiptsPagedAsync(
                businessId, customerId, fromDate, toDate, isVoided, page, pageSize);

            return Json(new
            {
                success = true,
                data = items,
                totalCount,
                currentPage = page,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load receipts." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetDetail(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var receipt = await _receiptService.GetReceiptAsync(id, businessId);
            if (receipt == null)
                return Json(new { success = false, message = "Receipt not found." });

            return Json(new { success = true, data = receipt });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load receipt." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostVoid(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _receiptService.VoidReceiptAsync(id, businessId);
            return Json(new { success = result.Success, message = result.Success ? "Receipt voided." : result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to void receipt." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetHasReceipt(int paymentId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var receipt = await _dbContext.PaymentReceipts.IgnoreQueryFilters()
                .Where(r => r.PaymentId == paymentId && r.BusinessId == businessId && !r.IsVoided)
                .Select(r => new { r.Id, r.ReceiptNumber })
                .FirstOrDefaultAsync();

            return Json(new { success = true, hasReceipt = receipt != null, receiptId = receipt?.Id, receiptNumber = receipt?.ReceiptNumber });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to check receipt status." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetSignatures()
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var signatures = await _dbContext.Signatures.IgnoreQueryFilters()
                .Where(s => s.BusinessId == businessId && s.IsActive)
                .OrderByDescending(s => s.IsDefault)
                .ThenBy(s => s.Label)
                .Select(s => new { s.Id, s.Label, s.IsDefault })
                .ToListAsync();

            return Json(new { success = true, data = signatures });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load signatures." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetDownloadPdf(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var receipt = await _receiptService.GetReceiptAsync(id, businessId);
            if (receipt == null)
                return NotFound();

            var html = BuildReceiptHtml(receipt);

            byte[] pdfBytes;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            pdfBytes = await GeneratePdfFromHtmlAsync(html, cts.Token);

            var filename = $"{receipt.ReceiptNumber}.pdf";
            return File(pdfBytes, "application/pdf", filename);
        }
        catch (OperationCanceledException)
        {
            return Json(new { success = false, message = "PDF generation timed out." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to generate PDF." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetCustomers()
    {
        try
        {
            var customers = await _dbContext.Customers
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name })
                .ToListAsync();

            return Json(new { success = true, data = customers });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load customers." });
        }
    }

    private string BuildReceiptHtml(ReceiptViewModel receipt)
    {
        var cs = receipt.CurrencySymbol ?? "€";
        var lines = string.Join("", receipt.Lines.Select(l =>
            $"<tr><td style='padding:10px 12px;border-bottom:1px solid #e8edf2;'>{l.InvoiceNumber}</td>" +
            $"<td style='padding:10px 12px;border-bottom:1px solid #e8edf2;text-align:right;'>{cs}{l.InvoiceTotal:N2}</td>" +
            $"<td style='padding:10px 12px;border-bottom:1px solid #e8edf2;text-align:right;'>{cs}{l.InvoiceOutstandingBefore:N2}</td>" +
            $"<td style='padding:10px 12px;border-bottom:1px solid #e8edf2;text-align:right;font-weight:700;color:#129867;'>{cs}{l.Amount:N2}</td>" +
            $"<td style='padding:10px 12px;border-bottom:1px solid #e8edf2;text-align:right;'>{cs}{l.InvoiceOutstandingAfter:N2}</td>" +
            $"<td style='padding:10px 12px;border-bottom:1px solid #e8edf2;'>{(l.IsFullPayment ? "Paid in Full" : "Partial")}</td></tr>"));

        var creditNote = receipt.CreditAmount.HasValue && receipt.CreditAmount.Value > 0
            ? $"<p style='margin-top:14px;padding:10px 16px;background:#EEF4F8;border-radius:8px;font-size:13px;'><strong>Credit held on account:</strong> {cs}{receipt.CreditAmount.Value:N2}</p>"
            : "";

        // Build signature block with embedded image
        var signature = "";
        if (!string.IsNullOrEmpty(receipt.SignatureFilePath))
        {
            var fileBasePath = _configuration["FileStorage:BasePath"] ?? "C:/BusinessPortal/Uploads";
            var sigFullPath = Path.Combine(fileBasePath, receipt.SignatureFilePath);
            if (System.IO.File.Exists(sigFullPath))
            {
                var sigBytes = System.IO.File.ReadAllBytes(sigFullPath);
                var sigBase64 = Convert.ToBase64String(sigBytes);
                var sigMime = receipt.SignatureFilePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? "image/svg+xml" : "image/png";
                signature = $"<div style='margin-top:30px;text-align:left;'>" +
                    $"<p style='font-size:13px;color:#5a6a7a;font-style:italic;margin:0 0 12px 0;'>Issued by</p>" +
                    $"<img src='data:{sigMime};base64,{sigBase64}' style='max-height:60px;margin-bottom:8px;display:block;' />" +
                    $"<div style='border-top:2px solid #1a2b3c;width:180px;margin-bottom:8px;'></div>" +
                    $"<p style='font-size:13px;color:#1a2b3c;font-weight:700;margin:0;'>{receipt.SignatureLabel}</p>" +
                    (!string.IsNullOrEmpty(receipt.SignaturePosition) ? $"<p style='font-size:12px;color:#5a6a7a;margin:3px 0 0 0;'>{receipt.SignaturePosition}</p>" : "") +
                    $"</div>";
            }
            else
            {
                signature = $"<div style='margin-top:30px;text-align:left;'><p style='font-size:13px;color:#5a6a7a;font-style:italic;margin:0 0 12px 0;'>Issued by</p><p style='font-size:13px;color:#1a2b3c;font-weight:700;'>{receipt.SignatureLabel}</p></div>";
            }
        }
        else if (!string.IsNullOrEmpty(receipt.SignatureLabel))
        {
            signature = $"<div style='margin-top:30px;text-align:left;'><p style='font-size:13px;color:#5a6a7a;font-style:italic;margin:0 0 12px 0;'>Issued by</p><p style='font-size:13px;color:#1a2b3c;font-weight:700;'>{receipt.SignatureLabel}</p></div>";
        }

        return $@"<!DOCTYPE html>
<html><head><meta charset='utf-8'/><style>
body {{ font-family: 'Inter', -apple-system, sans-serif; color: #1a2b3c; padding: 40px; max-width: 800px; margin: 0 auto; }}
h1 {{ font-family: 'Manrope', sans-serif; font-size: 28px; font-weight: 800; color: #0D5EA6; margin: 0 0 6px 0; }}
table {{ width: 100%; border-collapse: collapse; margin-top: 20px; }}
th {{ text-align: left; font-size: 11px; font-weight: 700; color: #5E7385; text-transform: uppercase; letter-spacing: .04em; padding: 10px 12px; border-bottom: 2px solid #0D5EA6; }}
</style></head><body>
<div style='display:flex;justify-content:space-between;align-items:flex-start;margin-bottom:30px;'>
  <div>
    <h1>Payment Receipt</h1>
    <p style='font-size:14px;color:#5a6a7a;margin:0;'>{receipt.ReceiptNumber}</p>
    <p style='font-size:13px;color:#5a6a7a;margin:4px 0 0 0;'>{receipt.PaymentType}</p>
  </div>
  <div style='text-align:right;'>
    <p style='font-weight:700;margin:0;'>{receipt.BusinessName}</p>
    <p style='font-size:13px;color:#5a6a7a;margin:4px 0 0 0;'>{receipt.BusinessAddress}</p>
    {(receipt.BusinessVatNumber != null ? $"<p style='font-size:12px;color:#5a6a7a;margin:4px 0 0 0;'>VAT: {receipt.BusinessVatNumber}</p>" : "")}
  </div>
</div>
<div style='display:flex;justify-content:space-between;margin-bottom:24px;padding:16px;background:#f8fafc;border-radius:10px;'>
  <div>
    <p style='font-size:11px;font-weight:700;color:#5E7385;text-transform:uppercase;margin:0 0 4px 0;'>Bill To</p>
    <p style='font-weight:600;margin:0;'>{receipt.CustomerName}</p>
    <p style='font-size:13px;color:#5a6a7a;margin:4px 0 0 0;'>{receipt.CustomerAddress}</p>
  </div>
  <div style='text-align:right;'>
    <p style='font-size:11px;font-weight:700;color:#5E7385;text-transform:uppercase;margin:0 0 4px 0;'>Receipt Date</p>
    <p style='font-weight:600;margin:0;'>{receipt.ReceiptDate:dd MMM yyyy}</p>
    <p style='font-size:13px;color:#5a6a7a;margin:8px 0 0 0;'>Method: {receipt.PaymentMethodName}</p>
    {(receipt.PaymentReference != null ? $"<p style='font-size:13px;color:#5a6a7a;margin:4px 0 0 0;'>Ref: {receipt.PaymentReference}</p>" : "")}
  </div>
</div>
<table>
  <thead><tr><th>Invoice</th><th style='text-align:right;'>Invoice Total</th><th style='text-align:right;'>Outstanding Before</th><th style='text-align:right;'>Amount Received</th><th style='text-align:right;'>Outstanding After</th><th>Status</th></tr></thead>
  <tbody>{lines}</tbody>
  <tfoot><tr style='font-weight:700;border-top:2px solid #0D5EA6;'><td colspan='3' style='padding:12px;text-align:right;'>Total Received</td><td style='padding:12px;text-align:right;color:#129867;'>{cs}{receipt.TotalAmountReceived:N2}</td><td colspan='2'></td></tr></tfoot>
</table>
{creditNote}
{signature}
</body></html>";
    }

    private static async Task<byte[]> GeneratePdfFromHtmlAsync(string html, CancellationToken cancellationToken)
    {
        await new BrowserFetcher().DownloadAsync();
        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true, Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" } });
        await using var page = await browser.NewPageAsync();
        await page.SetContentAsync(html, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Networkidle0 } });
        var pdfBytes = await page.PdfDataAsync(new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            MarginOptions = new MarginOptions { Top = "15mm", Bottom = "15mm", Left = "15mm", Right = "15mm" }
        });
        cancellationToken.ThrowIfCancellationRequested();
        return pdfBytes;
    }
}
