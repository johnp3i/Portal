namespace Portal.Infrastructure.Services;

public class PayslipPdfService : IPayslipPdfService
{
    public Task<byte[]> GeneratePdfAsync(string html)
    {
        // Stub - returns empty bytes. Full PDF generation (wkhtmltopdf/Puppeteer) to be implemented.
        return Task.FromResult(Array.Empty<byte>());
    }
}
