using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Portal.Infrastructure.Services;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace Portal.Web.Services;

/// <summary>
/// Generates a PDF byte array for a given quotation proposal using a dedicated print-optimised
/// Razor view (_QuotationPdf.cshtml) and PuppeteerSharp.
/// </summary>
public class ProposalPdfService : IProposalPdfService
{
    private readonly IProposalService _proposalService;
    private readonly IViewRenderService _viewRenderService;
    private readonly IConfiguration _configuration;
    private readonly ILogoService _logoService;
    private readonly ICurrentTenantService _tenantService;
    private readonly ILogger<ProposalPdfService> _logger;

    public ProposalPdfService(
        IProposalService proposalService,
        IViewRenderService viewRenderService,
        IConfiguration configuration,
        ILogoService logoService,
        ICurrentTenantService tenantService,
        ILogger<ProposalPdfService> logger)
    {
        _proposalService = proposalService;
        _viewRenderService = viewRenderService;
        _configuration = configuration;
        _logoService = logoService;
        _tenantService = tenantService;
        _logger = logger;
    }

    /// <summary>
    /// Pre-render canary: scans the final HTML for any image whose src still points at a local
    /// app/storage URL (/logo/, /uploads/, or an app-relative path) rather than an embedded data:
    /// URI or a public http(s) URL. Such a src cannot be fetched by Puppeteer and would render as a
    /// broken image — which some PDF viewers then report as a corrupt file. We log a warning (with
    /// the offending srcs) so the condition is visible in logs; the HTML has already been through the
    /// embed step and its display:none safety net, so generation still proceeds.
    /// </summary>
    private void WarnOnUnembeddedImages(string html, int quotationId)
    {
        var offenders = Regex.Matches(
                html,
                @"<img\s[^>]*src\s*=\s*[""'](?<src>(?:/|~/)[^""']*)[""']",
                RegexOptions.IgnoreCase)
            .Select(m => m.Groups["src"].Value)
            .Where(src => !src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();

        if (offenders.Count > 0)
        {
            _logger.LogWarning(
                "Proposal PDF for quotation {QuotationId} contains {Count} image(s) that were not embedded and may render broken: {Sources}",
                quotationId, offenders.Count, string.Join(", ", offenders));
        }
    }

    public async Task<byte[]> GenerateAsync(int quotationId, List<int> heroLogoIds, int? metaLogoId, CancellationToken cancellationToken = default)
    {
        // 1. Build the render model (same data as the Proposal Snapshot)
        var model = await _proposalService.GetRenderModelAsync(quotationId, heroLogoIds, metaLogoId);

        // 2. Render the dedicated print-optimised PDF view
        var html = await _viewRenderService.RenderViewToStringAsync("~/Views/Proposal/_QuotationPdf.cshtml", model);

        // 3. Post-process HTML: replace logo <img src="/uploads/..."> with base64 data URI
        html = await EmbedLogoAsBase64Async(html);

        // 3b. Pre-render canary: warn if any local image src survived the embed step.
        WarnOnUnembeddedImages(html, quotationId);

        // 4. Launch PuppeteerSharp and generate PDF with a per-page footer (line + page numbers)
        return await GeneratePdfFromHtmlAsync(html, cancellationToken);
    }

    /// <summary>
    /// Per-page PDF footer: a thin top rule plus centered "Powered by" branding and page numbers.
    /// Styles are inlined because Puppeteer renders footer templates in an isolated context where
    /// the document's &lt;style&gt; and web fonts do not apply.
    /// </summary>
    private const string FooterTemplate =
        "<div style=\"width:100%;padding:0 12mm;box-sizing:border-box;font-family:'Helvetica Neue',Arial,sans-serif;\">" +
            "<div style=\"border-top:1px solid rgba(13,94,166,.15);padding-top:4px;font-size:7px;color:#b0bec5;text-align:center;letter-spacing:0.03em;\">" +
                "Powered by 3 Inventors &mdash; Operational Intelligence" +
                "&nbsp;&middot;&nbsp;" +
                "Page <span class=\"pageNumber\"></span> of <span class=\"totalPages\"></span>" +
            "</div>" +
        "</div>";

    private async Task<string> EmbedLogoAsBase64Async(string html)
    {
        // Embed EVERY business logo that could appear in the proposal (hero logos + meta logo) as a
        // base64 data URI, matched by its exact PublicUrl. The proposal view renders logos by their
        // PublicUrl (which may be "/logo/..." or another prefix depending on storage config), and a
        // proposal can use more than one hero logo. The old approach only embedded the single primary
        // logo and only matched a "/logo/" prefix, so any other logo stayed as a URL Puppeteer could
        // not fetch (private storage) and rendered as a broken image — which some PDF viewers then
        // report as a corrupt file. Embedding by exact URL makes the PDF fully self-contained.
        var logos = await _logoService.GetByBusinessIdAsync(_tenantService.CurrentBusinessId);

        foreach (var logo in logos)
        {
            if (string.IsNullOrWhiteSpace(logo.PublicUrl))
                continue;

            var dataUri = GetLogoAsDataUri(logo);
            if (string.IsNullOrEmpty(dataUri))
                continue;

            // Replace the exact PublicUrl wherever it appears as an <img src>. Escape it for regex.
            var escapedUrl = Regex.Escape(logo.PublicUrl);
            var pattern = $@"(<img\s[^>]*src\s*=\s*[""']){escapedUrl}([""'])";
            html = Regex.Replace(html, pattern, $"$1{dataUri}$2", RegexOptions.IgnoreCase);
        }

        // Safety net: if any logo <img> still points at a local app URL (/logo/... or /uploads/...)
        // that we could not embed, drop its src so it renders as nothing rather than a broken-image
        // icon (a dangling src is what makes some viewers flag the PDF as corrupt).
        html = Regex.Replace(
            html,
            @"<img\s([^>]*?)src\s*=\s*[""'](?:/logo/|/uploads/)[^""']*[""']([^>]*)>",
            "<img $1$2 style=\"display:none\">",
            RegexOptions.IgnoreCase);

        return html;
    }

    private string? GetLogoAsDataUri(Infrastructure.Entities.BusinessLogo? logo)
    {
        if (logo == null || string.IsNullOrWhiteSpace(logo.FileName))
            return null;

        try
        {
            // Logos now live under the private storage root: {BasePath}/{businessId}/logos/{fileName}
            var basePath = _configuration["FileStorage:BasePath"];
            if (string.IsNullOrWhiteSpace(basePath))
                return null;

            var filePath = Path.Combine(basePath, logo.BusinessId.ToString(), "logos", logo.FileName);

            if (!System.IO.File.Exists(filePath))
                return null;

            var bytes = System.IO.File.ReadAllBytes(filePath);
            var base64 = Convert.ToBase64String(bytes);
            var contentType = logo.ContentType ?? "image/png";

            return $"data:{contentType};base64,{base64}";
        }
        catch (Exception ex)
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
            Landscape = false,
            Format = PaperFormat.A4,
            PrintBackground = true,
            DisplayHeaderFooter = true,
            // Empty header suppresses Chromium's default date/title header.
            HeaderTemplate = "<span></span>",
            FooterTemplate = FooterTemplate,
            MarginOptions = new MarginOptions
            {
                Top = "14mm",
                // Reserve room for the running footer so content never overlaps it.
                Bottom = "16mm",
                Left = "0mm",
                Right = "0mm"
            }
        });

        cancellationToken.ThrowIfCancellationRequested();

        return pdfBytes;
    }
}
