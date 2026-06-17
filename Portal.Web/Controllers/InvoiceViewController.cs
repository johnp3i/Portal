using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Services;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace Portal.Web.Controllers;

/// <summary>
/// Public, unauthenticated controller for viewing shared invoices via token-based URLs.
/// </summary>
[AllowAnonymous]
public class InvoiceViewController : Controller
{
    private readonly IInvoiceSharingService _sharingService;
    private readonly IInvoiceAcceptanceService _acceptanceService;
    private readonly IInvoiceRenderer _invoiceRenderer;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogoService _logoService;
    private readonly ILogger<InvoiceViewController> _logger;

    public InvoiceViewController(
        IInvoiceSharingService sharingService,
        IInvoiceAcceptanceService acceptanceService,
        IInvoiceRenderer invoiceRenderer,
        IWebHostEnvironment environment,
        ILogoService logoService,
        ILogger<InvoiceViewController> logger)
    {
        _sharingService = sharingService;
        _acceptanceService = acceptanceService;
        _invoiceRenderer = invoiceRenderer;
        _environment = environment;
        _logoService = logoService;
        _logger = logger;
    }

    [HttpGet("/invoice-view/{token}")]
    public async Task<IActionResult> ViewInvoice(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return NotFound();

        var share = await _sharingService.GetByTokenAsync(token);
        if (share == null)
            return NotFound();

        Response.Headers["Cache-Control"] = "no-store";

        // Check if cancelled or expired
        if (!share.IsActive || share.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return View("~/Views/Shared/Unavailable.cshtml");
        }

        // Inject dependencies (SweetAlert2 + BlockUI) before </head>
        var dependencyScripts = @"<script src=""https://cdn.jsdelivr.net/npm/sweetalert2@11""></script>
    <script src=""/js/block-ui.js""></script>";
        var html = share.SnapshotHtml;
        var headClose = html.IndexOf("</head>");
        if (headClose >= 0)
        {
            html = html.Insert(headClose, dependencyScripts);
        }

        // Inject Download PDF and Print buttons with download script
        var downloadButton = $@"<div class=""no-print"" style=""display:flex;justify-content:flex-end;gap:10px;margin-bottom:16px;"">
            <button onclick=""window.print()"" style=""display:inline-flex;align-items:center;gap:8px;padding:10px 20px;background:#fff;color:#0D5EA6;border:1.5px solid #0D5EA6;border-radius:12px;font-size:13px;font-weight:700;cursor:pointer;font-family:inherit;"">
                &#x1F5B6; Print
            </button>
            <button onclick=""downloadInvoicePdf()"" style=""display:inline-flex;align-items:center;gap:8px;padding:10px 20px;background:linear-gradient(180deg,#1A6BB8 0%, #0D5EA6 100%);color:#fff;border:none;border-radius:12px;font-size:13px;font-weight:700;cursor:pointer;font-family:inherit;"">
                &#x2B73; Download PDF
            </button>
        </div>
        <script>
            async function downloadInvoicePdf() {{
                BlockUI.show('Generating PDF...');
                try {{
                    var response = await fetch('/invoice-view/{token}/download-pdf');
                    if (!response.ok || !response.headers.get('content-type')?.includes('application/pdf')) {{
                        var data = await response.json();
                        BlockUI.hide();
                        Swal.fire({{ title: 'Error', text: data.message || 'Failed to generate PDF.', icon: 'error', confirmButtonColor: '#0D5EA6' }});
                        return;
                    }}
                    var blob = await response.blob();
                    var contentDisposition = response.headers.get('content-disposition');
                    var filename = 'invoice.pdf';
                    if (contentDisposition) {{
                        var match = contentDisposition.match(/filename[^;=\n]*=((['""]).*?\2|[^;\n]*)/);
                        if (match && match[1]) filename = match[1].replace(/['\""]/g, '');
                    }}
                    var url = URL.createObjectURL(blob);
                    var a = document.createElement('a');
                    a.href = url;
                    a.download = filename;
                    document.body.appendChild(a);
                    a.click();
                    document.body.removeChild(a);
                    URL.revokeObjectURL(url);
                    BlockUI.hide();
                }} catch (e) {{
                    BlockUI.hide();
                    Swal.fire({{ title: 'Error', text: 'An unexpected error occurred.', icon: 'error', confirmButtonColor: '#0D5EA6' }});
                }}
            }}
        </script>";

        // Insert after <div class="page"> or at the start of body content
        var pageDiv = html.IndexOf("<div class=\"page\">");
        if (pageDiv >= 0)
        {
            var insertPos = html.IndexOf(">", pageDiv) + 1;
            html = html.Insert(insertPos, downloadButton);

            // Inject acceptance UI after the download button
            var acceptance = await _acceptanceService.GetByInvoiceShareIdAsync(share.Id);
            string acceptanceHtml;

            if (acceptance != null)
            {
                // Read-only accepted message
                acceptanceHtml = $@"<div class=""no-print"" style=""text-align:center;margin-bottom:20px;"">
                    <div style=""display:inline-flex;align-items:center;gap:8px;padding:12px 24px;background:#e6f7f1;border:1px solid #129867;border-radius:12px;color:#129867;font-size:14px;font-weight:700;font-family:inherit;"">
                        &#x2713; Accepted on <span>{acceptance.AcceptedAtUtc:dd MMM yyyy}</span>
                    </div>
                </div>";
            }
            else
            {
                // Acceptance form with checkbox and button
                acceptanceHtml = $@"<div class=""no-print"" id=""acceptance-section"" style=""text-align:center;margin-bottom:20px;"">
                    <div style=""display:inline-block;padding:20px 32px;background:#f8fbff;border:1px solid #d8e4ef;border-radius:14px;"">
                        <label style=""display:flex;align-items:center;gap:10px;cursor:pointer;font-size:14px;color:#2d3748;font-family:inherit;margin-bottom:14px;"">
                            <input type=""checkbox"" id=""acceptance-checkbox"" style=""width:18px;height:18px;cursor:pointer;"" />
                            I accept this invoice as correct and agree to pay by the due date.
                        </label>
                        <button id=""accept-btn"" disabled style=""padding:10px 24px;background:linear-gradient(180deg,#15b37a 0%,#129867 100%);color:#fff;border:none;border-radius:12px;font-size:13px;font-weight:700;cursor:pointer;font-family:inherit;opacity:0.5;"">
                            Accept Invoice
                        </button>
                    </div>
                    <script>
                        (function() {{
                            var checkbox = document.getElementById('acceptance-checkbox');
                            var btn = document.getElementById('accept-btn');
                            checkbox.addEventListener('change', function() {{
                                btn.disabled = !checkbox.checked;
                                btn.style.opacity = checkbox.checked ? '1' : '0.5';
                            }});
                            btn.addEventListener('click', async function() {{
                                if (!checkbox.checked) return;
                                BlockUI.show('Processing...');
                                try {{
                                    var response = await fetch('/invoice-view/{token}/accept', {{
                                        method: 'POST',
                                        headers: {{ 'Content-Type': 'application/json' }}
                                    }});
                                    var data = await response.json();
                                    BlockUI.hide();
                                    if (data.success || data.alreadyAccepted) {{
                                        var acceptedDate = new Date(data.acceptedAt).toLocaleDateString();
                                        Swal.fire({{ title: 'Invoice Accepted', text: 'Thank you for accepting this invoice.', icon: 'success', confirmButtonColor: '#0D5EA6' }});
                                        document.getElementById('acceptance-section').innerHTML = '<div style=""display:inline-flex;align-items:center;gap:8px;padding:12px 24px;background:#e6f7f1;border:1px solid #129867;border-radius:12px;color:#129867;font-size:14px;font-weight:700;font-family:inherit;"">&#x2713; Accepted on ' + acceptedDate + '</div>';
                                    }} else {{
                                        Swal.fire({{ title: 'Error', text: data.message || 'An unexpected error occurred. Please try again.', icon: 'error', confirmButtonColor: '#0D5EA6' }});
                                    }}
                                }} catch (e) {{
                                    BlockUI.hide();
                                    Swal.fire({{ title: 'Error', text: 'An unexpected error occurred. Please try again.', icon: 'error', confirmButtonColor: '#0D5EA6' }});
                                }}
                            }});
                        }})();
                    </script>
                </div>";
            }

            // Insert acceptance HTML after the download button
            var acceptanceInsertPos = insertPos + downloadButton.Length;
            html = html.Insert(acceptanceInsertPos, acceptanceHtml);
        }

        return Content(html, "text/html");
    }

    [HttpPost("/invoice-view/{token}/accept")]
    public async Task<IActionResult> AcceptInvoice(string token)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = Request.Headers["User-Agent"].ToString();

        var result = await _acceptanceService.AcceptAsync(token, ipAddress, userAgent);

        return Json(new
        {
            success = result.Success,
            message = result.Message,
            acceptedAt = result.AcceptedAtUtc,
            alreadyAccepted = result.AlreadyAccepted
        });
    }

    [HttpGet("/invoice-view/{token}/download-pdf")]
    public async Task<IActionResult> DownloadPdf(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return NotFound();

        var share = await _sharingService.GetByTokenAsync(token);
        if (share == null)
            return NotFound();

        if (!share.IsActive || share.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            return NotFound();

        try
        {
            // Render the invoice Snapshot view fresh from the database (same as authenticated download)
            var html = await _invoiceRenderer.RenderAsync(share.InvoiceId, share.BusinessId);

            // Post-process HTML to embed logo as base64 data URI
            html = await EmbedLogoAsBase64Async(html, share.BusinessId);

            // Generate PDF with 30-second timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var pdfBytes = await GeneratePdfFromHtmlAsync(html, cts.Token);

            // Extract invoice number for filename
            var invoiceNumber = share.Invoice?.InvoiceNumber ?? "download";
            var filename = GenerateInvoicePdfFilename(invoiceNumber);

            return File(pdfBytes, "application/pdf", filename);
        }
        catch (OperationCanceledException)
        {
            return StatusCode(500, new { success = false, message = "PDF generation timed out. Please try again." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate PDF for shared invoice token {Token}", token);
            return StatusCode(500, new { success = false, message = "Failed to generate PDF. Please try again." });
        }
    }

    private async Task<string> EmbedLogoAsBase64Async(string html, int businessId)
    {
        var logos = await _logoService.GetByBusinessIdAsync(businessId);
        var primaryLogo = logos.FirstOrDefault(l => l.IsPrimary) ?? logos.FirstOrDefault();

        var dataUri = GetLogoAsDataUri(primaryLogo);
        if (string.IsNullOrEmpty(dataUri))
            return html;

        var pattern = @"(<img\s[^>]*src\s*=\s*"")(/uploads/[^""]+)("")";
        html = Regex.Replace(html, pattern, $"$1{dataUri}$3", RegexOptions.IgnoreCase);

        return html;
    }

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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read logo file for data URI embedding");
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
            Landscape = false,
            Format = PaperFormat.A4,
            PrintBackground = true,
            MarginOptions = new MarginOptions
            {
                Top = "14mm",
                Bottom = "0mm",
                Left = "0mm",
                Right = "0mm"
            }
        });

        cancellationToken.ThrowIfCancellationRequested();

        return pdfBytes;
    }

    private static string? ExtractInvoiceNumberFromHtml(string html)
    {
        // Try to extract invoice number from the HTML snapshot
        var match = Regex.Match(html, @"Invoice\s*#?\s*:?\s*([A-Za-z0-9\-]+)", RegexOptions.IgnoreCase);
        if (match.Success && match.Groups[1].Value.Length > 0)
            return match.Groups[1].Value;

        return null;
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
