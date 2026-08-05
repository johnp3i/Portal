using System.Text.RegularExpressions;
using Portal.Infrastructure.Services;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace Portal.Web.Services;

public class PayslipPdfService : IPayslipPdfService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogoService _logoService;
    private readonly ICurrentTenantService _tenantService;

    public PayslipPdfService(
        IWebHostEnvironment environment,
        ILogoService logoService,
        ICurrentTenantService tenantService)
    {
        _environment = environment;
        _logoService = logoService;
        _tenantService = tenantService;
    }

    public async Task<byte[]> GeneratePdfAsync(string html, CancellationToken cancellationToken = default)
    {
        try
        {
            html = await EmbedLogoAsBase64Async(html);
            return await GeneratePdfFromHtmlAsync(html, cancellationToken);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<byte[]>> GenerateBatchPdfAsync(List<string> htmlDocuments, CancellationToken cancellationToken = default)
    {
        try
        {
            await new BrowserFetcher().DownloadAsync();

            await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
            });

            var results = new List<byte[]>();
            foreach (var html in htmlDocuments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var embeddedHtml = await EmbedLogoAsBase64Async(html);

                await using var page = await browser.NewPageAsync();
                await page.SetContentAsync(embeddedHtml, new NavigationOptions
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
                        Top = "0mm",
                        Bottom = "0mm",
                        Left = "0mm",
                        Right = "0mm"
                    }
                });
                results.Add(pdfBytes);
            }
            return results;
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

        var pattern = @"(<img\s[^>]*src\s*=\s*"")(/uploads/[^""]+)("")";
        html = Regex.Replace(html, pattern, $"$1{dataUri}$3", RegexOptions.IgnoreCase);

        return html;
    }

    private string? GetLogoAsDataUri(Portal.Infrastructure.Entities.BusinessLogo? logo)
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
                Top = "0mm",
                Bottom = "0mm",
                Left = "0mm",
                Right = "0mm"
            }
        });

        cancellationToken.ThrowIfCancellationRequested();
        return pdfBytes;
    }
}
