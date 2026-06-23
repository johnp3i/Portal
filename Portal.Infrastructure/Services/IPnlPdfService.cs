using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Generates PDF byte arrays for Profit &amp; Loss reports.
/// </summary>
public interface IPnlPdfService
{
    /// <summary>
    /// Generates a PDF byte array from a fully computed P&amp;L summary.
    /// </summary>
    Task<byte[]> GenerateAsync(PnlPdfModel model, CancellationToken cancellationToken = default);
}
