using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Portal.Infrastructure.Services;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace Portal.Web.Services;

/// <summary>
/// Generates a PDF byte array for a given invoice using the Snapshot view and PuppeteerSharp.
/// </summary>
public class InvoicePdfService : IInvoicePdfService
{
    private readonly IInvoiceRenderer _invoiceRenderer;
    private readonly IConfiguration _configuration;
    private readonly ILogoService _logoService;
    private readonly ICurrentTenantService _tenantService;
    private readonly ILogger<InvoicePdfService> _logger;

    public InvoicePdfService(
        IInvoiceRenderer invoiceRenderer,
        IConfiguration configuration,
        ILogoService logoService,
        ICurrentTenantService tenantService,
        ILogger<InvoicePdfService> logger)
    {
        _invoiceRenderer = invoiceRenderer;
        _configuration = configuration;
        _logoService = logoService;
        _tenantService = tenantService;
        _logger = logger;
    }

    /// <summary>
    /// Pre-render canary: warns if any image src still points at a local app/storage URL (not an
    /// embedded data: URI or a public URL), which would render as a broken image and can make some
    /// PDF viewers report the file as corrupt. Logged only; generation proceeds (the embed step's
    /// display:none safety net already hides any such image).
    /// </summary>
    private void WarnOnUnembeddedImages(string html, int invoiceId)
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
                "Invoice PDF for invoice {InvoiceId} contains {Count} image(s) that were not embedded and may render broken: {Sources}",
                invoiceId, offenders.Count, string.Join(", ", offenders));
        }
    }

    public async Task<byte[]> GenerateAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        // 1. Get HTML from existing IInvoiceRenderer
        var html = await _invoiceRenderer.RenderAsync(invoiceId);

        // 2. Post-process HTML: replace logo <img src="/uploads/..."> with base64 data URI
        html = await EmbedLogoAsBase64Async(html);

        // 2b. Pre-render canary: warn if any local image src survived the embed step.
        WarnOnUnembeddedImages(html, invoiceId);

        // 3. Extract the per-page footer template (payment details + branding) authored in the view.
        var footerTemplate = ExtractFooterTemplate(html);

        // 4. Launch PuppeteerSharp and generate PDF with a repeating footer on every page.
        return await GeneratePdfFromHtmlAsync(html, footerTemplate, cancellationToken);
    }

    /// <summary>
    /// Pulls the inner HTML of the view's &lt;template id="pdf-footer"&gt; element so it can be
    /// handed to Puppeteer as a running FooterTemplate (repeated on every page). Returns an empty
    /// string if the template is absent, in which case no footer is rendered.
    /// </summary>
    private static string ExtractFooterTemplate(string html)
    {
        var match = Regex.Match(
            html,
            @"<template\s+id\s*=\s*""pdf-footer""\s*>(?<body>.*?)</template>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        return match.Success ? match.Groups["body"].Value.Trim() : string.Empty;
    }

    private async Task<string> EmbedLogoAsBase64Async(string html)
    {
        // Embed every business logo by its exact PublicUrl as a base64 data URI so the PDF is fully
        // self-contained. Matching only a "/logo/" prefix (and only the primary logo) left other
        // logo URLs as unresolvable links that render as broken images — which some PDF viewers
        // report as a corrupt file.
        var logos = await _logoService.GetByBusinessIdAsync(_tenantService.CurrentBusinessId);

        foreach (var logo in logos)
        {
            if (string.IsNullOrWhiteSpace(logo.PublicUrl))
                continue;

            var dataUri = GetLogoAsDataUri(logo);
            if (string.IsNullOrEmpty(dataUri))
                continue;

            var escapedUrl = Regex.Escape(logo.PublicUrl);
            var pattern = $@"(<img\s[^>]*src\s*=\s*[""']){escapedUrl}([""'])";
            html = Regex.Replace(html, pattern, $"$1{dataUri}$2", RegexOptions.IgnoreCase);
        }

        // Safety net: hide any logo <img> we could not embed so it renders as nothing rather than a
        // broken-image icon (a dangling src is what makes some viewers flag the PDF as corrupt).
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

    private static async Task<byte[]> GeneratePdfFromHtmlAsync(string html, string footerTemplate, CancellationToken cancellationToken)
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

        var hasFooter = !string.IsNullOrWhiteSpace(footerTemplate);

        var pdfOptions = new PdfOptions
        {
            Landscape = false,
            Format = PaperFormat.A4,
            PrintBackground = true,
            // Reserve bottom space for the running footer so page content never overlaps it.
            // A larger bottom margin is needed only when a footer is present.
            MarginOptions = new MarginOptions
            {
                Top = "14mm",
                Bottom = hasFooter ? "26mm" : "0mm",
                Left = "0mm",
                Right = "0mm"
            }
        };

        if (hasFooter)
        {
            pdfOptions.DisplayHeaderFooter = true;
            // An empty header keeps Chromium from injecting its default date/title header.
            pdfOptions.HeaderTemplate = "<span></span>";
            pdfOptions.FooterTemplate = footerTemplate;
        }

        var pdfBytes = await page.PdfDataAsync(pdfOptions);

        cancellationToken.ThrowIfCancellationRequested();

        return pdfBytes;
    }
}
