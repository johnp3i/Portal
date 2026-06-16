using System.Text.RegularExpressions;
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
    private readonly IWebHostEnvironment _environment;
    private readonly ILogoService _logoService;
    private readonly ICurrentTenantService _tenantService;

    public ProposalPdfService(
        IProposalService proposalService,
        IViewRenderService viewRenderService,
        IWebHostEnvironment environment,
        ILogoService logoService,
        ICurrentTenantService tenantService)
    {
        _proposalService = proposalService;
        _viewRenderService = viewRenderService;
        _environment = environment;
        _logoService = logoService;
        _tenantService = tenantService;
    }

    public async Task<byte[]> GenerateAsync(int quotationId, List<int> heroLogoIds, int? metaLogoId, CancellationToken cancellationToken = default)
    {
        // 1. Build the render model (same data as the Proposal Snapshot)
        var model = await _proposalService.GetRenderModelAsync(quotationId, heroLogoIds, metaLogoId);

        // 2. Render the dedicated print-optimised PDF view
        var html = await _viewRenderService.RenderViewToStringAsync("~/Views/Proposal/_QuotationPdf.cshtml", model);

        // 3. Post-process HTML: replace logo <img src="/uploads/..."> with base64 data URI
        html = await EmbedLogoAsBase64Async(html);

        // 4. Launch PuppeteerSharp and generate PDF
        return await GeneratePdfFromHtmlAsync(html, cancellationToken);
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
