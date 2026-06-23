using System.Text.RegularExpressions;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace Portal.Web.Services;

/// <summary>
/// Generates a PDF byte array for a P&L summary report using PuppeteerSharp.
/// </summary>
public class PnlPdfService : IPnlPdfService
{
    private readonly IViewRenderService _viewRenderService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogoService _logoService;
    private readonly ICurrentTenantService _tenantService;

    public PnlPdfService(
        IViewRenderService viewRenderService,
        IWebHostEnvironment environment,
        ILogoService logoService,
        ICurrentTenantService tenantService)
    {
        _viewRenderService = viewRenderService;
        _environment = environment;
        _logoService = logoService;
        _tenantService = tenantService;
    }

    public async Task<byte[]> GenerateAsync(PnlPdfModel model, CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Render the P&L PDF view to HTML
            var html = await _viewRenderService.RenderViewToStringAsync("~/Views/ProfitLoss/PdfExport.cshtml", model);

            // 2. Post-process HTML: replace logo <img src="/uploads/..."> with base64 data URI
            html = await EmbedLogoAsBase64Async(html);

            // 3. Launch PuppeteerSharp and generate PDF
            return await GeneratePdfFromHtmlAsync(html, cancellationToken);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private async Task<string> EmbedLogoAsBase64Async(string html)
    {
        var logos = await _logoService.GetByBusinessIdAsync(_tenantService.CurrentBusinessId);
        var primaryLogo = logos.FirstOrDefault(l => l.IsPrimary) ?? logos.FirstOrDefault();

        var dataUri = GetLogoAsDataUri(primaryLogo);
        if (string.IsNullOrEmpty(dataUri))
            return html;

        // Replace <img> tags with src="/uploads/..." with the base64 data URI
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
}
