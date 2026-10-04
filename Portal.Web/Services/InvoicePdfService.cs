using System.Text.RegularExpressions;
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

    public InvoicePdfService(
        IInvoiceRenderer invoiceRenderer,
        IConfiguration configuration,
        ILogoService logoService,
        ICurrentTenantService tenantService)
    {
        _invoiceRenderer = invoiceRenderer;
        _configuration = configuration;
        _logoService = logoService;
        _tenantService = tenantService;
    }

    public async Task<byte[]> GenerateAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        // 1. Get HTML from existing IInvoiceRenderer
        var html = await _invoiceRenderer.RenderAsync(invoiceId);

        // 2. Post-process HTML: replace logo <img src="/uploads/..."> with base64 data URI
        html = await EmbedLogoAsBase64Async(html);

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
        var logos = await _logoService.GetByBusinessIdAsync(_tenantService.CurrentBusinessId);
        var primaryLogo = logos.FirstOrDefault(l => l.IsPrimary) ?? logos.FirstOrDefault();

        var dataUri = GetLogoAsDataUri(primaryLogo);
        if (string.IsNullOrEmpty(dataUri))
            return html;

        // Replace the logo <img src="/logo/..."> with the base64 data URI so the PDF is self-contained.
        var pattern = @"(<img\s[^>]*src\s*=\s*"")(/logo/[^""]+)("")";
        html = Regex.Replace(html, pattern, $"$1{dataUri}$3", RegexOptions.IgnoreCase);

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
