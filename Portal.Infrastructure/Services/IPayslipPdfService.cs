namespace Portal.Infrastructure.Services;

public interface IPayslipPdfService
{
    Task<byte[]> GeneratePdfAsync(string html);
}
