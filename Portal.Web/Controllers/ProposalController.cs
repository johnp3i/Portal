using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Services;
using Portal.Web.Services;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace Portal.Web.Controllers;

/// <summary>
/// Public, unauthenticated controller for viewing shared proposals via token-based URLs.
/// </summary>
[AllowAnonymous]
public class ProposalController : Controller
{
    private readonly IProposalService _proposalService;
    private readonly IProposalAcceptanceService _acceptanceService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogoService _logoService;
    private readonly IViewRenderService _viewRenderService;
    private readonly ILogger<ProposalController> _logger;

    public ProposalController(
        IProposalService proposalService,
        IProposalAcceptanceService acceptanceService,
        IWebHostEnvironment environment,
        ILogoService logoService,
        IViewRenderService viewRenderService,
        ILogger<ProposalController> logger)
    {
        _proposalService = proposalService;
        _acceptanceService = acceptanceService;
        _environment = environment;
        _logoService = logoService;
        _viewRenderService = viewRenderService;
        _logger = logger;
    }

    [HttpGet("/proposal/{token}")]
    public async Task<IActionResult> ViewProposal(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return NotFound();

        var share = await _proposalService.GetByTokenAsync(token);
        if (share == null)
            return NotFound();

        // Set cache-control to prevent intermediary caching
        Response.Headers["Cache-Control"] = "no-store";

        // Check if cancelled or expired
        if (!share.IsActive || share.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return View("~/Views/Shared/Unavailable.cshtml");
        }

        // Inject acceptance UI into the HTML snapshot
        var html = share.SnapshotHtml;
        var pageWrap = html.IndexOf("<div class=\"page-wrap\">");
        if (pageWrap >= 0)
        {
            // Check if an acceptance already exists for this share
            var acceptance = await _acceptanceService.GetByProposalShareIdAsync(share.Id);
            string acceptanceHtml;

            if (acceptance != null)
            {
                // Read-only accepted message
                acceptanceHtml = $@"<div class=""no-print"" style=""text-align:center;margin-top:20px;margin-bottom:20px;"">
                    <div style=""display:inline-flex;align-items:center;gap:8px;padding:12px 24px;background:#e6f7f1;border:1px solid #129867;border-radius:12px;color:#129867;font-size:14px;font-weight:700;font-family:inherit;"">
                        &#x2713; Accepted on <span>{acceptance.AcceptedAtUtc:dd MMM yyyy}</span>
                    </div>
                </div>";
            }
            else
            {
                // Acceptance form with checkbox and button
                acceptanceHtml = $@"<div class=""no-print"" id=""acceptance-section"" style=""text-align:center;margin-top:20px;margin-bottom:20px;"">
                    <div style=""display:inline-block;padding:20px 32px;background:#f8fbff;border:1px solid #d8e4ef;border-radius:14px;"">
                        <label style=""display:flex;align-items:center;gap:10px;cursor:pointer;font-size:14px;color:#2d3748;font-family:inherit;margin-bottom:14px;"">
                            <input type=""checkbox"" id=""acceptTermsCheckbox"" style=""width:18px;height:18px;cursor:pointer;"" />
                            I accept this proposal and agree to proceed with the quoted work.
                        </label>
                        <button id=""acceptProposalBtn"" disabled style=""padding:10px 24px;background:linear-gradient(180deg,#1A6BB8 0%,#0D5EA6 100%);color:#fff;border:none;border-radius:12px;font-size:13px;font-weight:700;cursor:pointer;font-family:inherit;opacity:0.5;"">
                            Accept Proposal
                        </button>
                    </div>
                    <script>
                        (function() {{
                            var checkbox = document.getElementById('acceptTermsCheckbox');
                            var btn = document.getElementById('acceptProposalBtn');
                            checkbox.addEventListener('change', function() {{
                                btn.disabled = !checkbox.checked;
                                btn.style.opacity = checkbox.checked ? '1' : '0.5';
                            }});
                            btn.addEventListener('click', async function() {{
                                if (!checkbox.checked) return;
                                BlockUI.show('Processing...');
                                try {{
                                    var response = await fetch('/proposal/{token}/accept', {{
                                        method: 'POST',
                                        headers: {{ 'Content-Type': 'application/json' }}
                                    }});
                                    var data = await response.json();
                                    BlockUI.hide();
                                    if (data.success || data.alreadyAccepted) {{
                                        var acceptedDate = new Date(data.acceptedAt).toLocaleDateString();
                                        Swal.fire({{ title: 'Proposal Accepted', text: 'Thank you for accepting this proposal.', icon: 'success', confirmButtonColor: '#0D5EA6' }});
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

            html = html.Insert(pageWrap, acceptanceHtml);
        }

        // Replace window.print() with downloadProposalPdf() function
        html = html.Replace("onclick=\"window.print()\"", "onclick=\"downloadProposalPdf()\"");
        html = html.Replace("onclick=\"downloadQuotationPdf()\"", "onclick=\"downloadProposalPdf()\"");

        // Inject downloadProposalPdf script before </body> or </html>
        var downloadScript = $@"
<div id=""pdfBlockOverlay"" style=""display:none;position:fixed;inset:0;z-index:9999;background:rgba(255,255,255,.85);backdrop-filter:blur(4px);align-items:center;justify-content:center;flex-direction:column;gap:16px;"">
    <svg width=""48"" height=""48"" viewBox=""0 0 48 48"" style=""animation:spin 1s linear infinite;""><circle cx=""24"" cy=""24"" r=""20"" fill=""none"" stroke=""#EEF4F8"" stroke-width=""4""/><circle cx=""24"" cy=""24"" r=""20"" fill=""none"" stroke=""#0D5EA6"" stroke-width=""4"" stroke-dasharray=""80"" stroke-dashoffset=""60"" stroke-linecap=""round""/></svg>
    <div style=""font-family:Inter,sans-serif;font-size:15px;font-weight:600;color:#1A2B3C;"" id=""pdfBlockMessage"">Generating PDF...</div>
</div>
<style>@keyframes spin{{from{{transform:rotate(0deg)}}to{{transform:rotate(360deg)}}}}</style>
<script>
if (typeof BlockUI === 'undefined') {{
    window.BlockUI = {{
        show: function(msg) {{
            var el = document.getElementById('pdfBlockOverlay');
            if (el) {{ document.getElementById('pdfBlockMessage').textContent = msg || 'Processing...'; el.style.display = 'flex'; }}
        }},
        hide: function() {{
            var el = document.getElementById('pdfBlockOverlay');
            if (el) {{ el.style.display = 'none'; }}
        }}
    }};
}}
if (typeof Swal === 'undefined') {{
    window.Swal = {{ fire: function(opts) {{ alert(opts.text || opts.title || 'Error'); }} }};
}}
async function downloadProposalPdf() {{
    BlockUI.show('Generating PDF...');
    try {{
        var response = await fetch('/proposal/{token}/download-pdf');
        if (!response.ok || !response.headers.get('content-type')?.includes('application/pdf')) {{
            var data = await response.json();
            BlockUI.hide();
            Swal.fire({{ title: 'Error', text: data.message || 'Failed to generate PDF.', icon: 'error', confirmButtonColor: '#0D5EA6' }});
            return;
        }}
        var blob = await response.blob();
        var contentDisposition = response.headers.get('content-disposition');
        var filename = 'proposal.pdf';
        if (contentDisposition) {{
            var match = contentDisposition.match(/filename[^;=\n]*=((['""]).*?\2|[^;\n]*)/);
            if (match && match[1]) {{
                filename = match[1].replace(/['""]/g, '');
            }}
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
        Swal.fire({{ title: 'Error', text: 'Could not complete download due to a connection problem.', icon: 'error', confirmButtonColor: '#0D5EA6' }});
    }}
}}
</script>";

        var bodyClose = html.LastIndexOf("</body>");
        if (bodyClose >= 0)
            html = html.Insert(bodyClose, downloadScript);
        else
        {
            var htmlClose = html.LastIndexOf("</html>");
            if (htmlClose >= 0)
                html = html.Insert(htmlClose, downloadScript);
            else
                html += downloadScript;
        }

        return Content(html, "text/html");
    }

    [HttpPost("/proposal/{token}/accept")]
    public async Task<IActionResult> AcceptProposal(string token)
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

    [HttpGet("/proposal/{token}/download-pdf")]
    public async Task<IActionResult> DownloadPdf(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return NotFound();

        var share = await _proposalService.GetByTokenAsync(token);
        if (share == null)
            return NotFound();

        if (!share.IsActive || share.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            return NotFound();

        if (string.IsNullOrEmpty(share.SnapshotHtml))
            return NotFound();

        try
        {
            // Build render model from live data using explicit businessId (no tenant context needed)
            var logos = await _logoService.GetByBusinessIdAsync(share.BusinessId);
            var primaryLogo = logos.FirstOrDefault(l => l.IsPrimary);
            var heroLogoIds = primaryLogo != null ? new List<int> { primaryLogo.Id } : new List<int>();

            var model = await _proposalService.GetRenderModelAsync(share.QuotationId, share.BusinessId, heroLogoIds, primaryLogo?.Id);

            // Render the professional PDF view
            var html = await _viewRenderService.RenderViewToStringAsync("~/Views/Proposal/_QuotationPdf.cshtml", model);

            // Embed logos as base64 data URIs
            html = await EmbedLogoAsBase64Async(html, share.BusinessId);

            // Generate PDF with 30-second timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var pdfBytes = await GeneratePdfFromHtmlAsync(html, cts.Token);

            // Build filename from quotation reference
            var reference = share.Quotation?.Reference ?? model.Reference ?? "download";
            var filename = GenerateProposalPdfFilename(reference);

            return File(pdfBytes, "application/pdf", filename);
        }
        catch (OperationCanceledException)
        {
            return StatusCode(500, new { success = false, message = "PDF generation timed out. Please try again." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate PDF for shared proposal token {Token}", token);
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

    private static string GenerateProposalPdfFilename(string reference)
    {
        var invalidChars = new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
        var sanitized = new string(reference
            .Where(c => !invalidChars.Contains(c) && c > '\u001F')
            .ToArray());
        sanitized = sanitized.Trim().Trim('.');
        if (string.IsNullOrWhiteSpace(sanitized))
            return "QUO-download.pdf";
        return $"QUO-{sanitized}.pdf";
    }
}
