namespace Portal.Infrastructure.Services;

public interface IPayslipPdfService
{
    Task<byte[]> GeneratePdfAsync(string html, CancellationToken cancellationToken = default);
    Task<List<byte[]>> GenerateBatchPdfAsync(List<string> htmlDocuments, CancellationToken cancellationToken = default);
}
