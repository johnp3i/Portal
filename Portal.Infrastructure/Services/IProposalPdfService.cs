namespace Portal.Infrastructure.Services;

/// <summary>
/// Generates a PDF byte array for a given quotation proposal using the Snapshot view.
/// </summary>
public interface IProposalPdfService
{
    /// <summary>
    /// Renders the proposal snapshot to HTML and converts it to a PDF document.
    /// </summary>
    /// <param name="quotationId">The quotation identifier.</param>
    /// <param name="heroLogoIds">List of hero logo identifiers for the proposal header.</param>
    /// <param name="metaLogoId">Optional meta logo identifier.</param>
    /// <param name="cancellationToken">Optional cancellation token (30-second timeout applied by caller).</param>
    /// <returns>PDF file as a byte array.</returns>
    Task<byte[]> GenerateAsync(int quotationId, List<int> heroLogoIds, int? metaLogoId, CancellationToken cancellationToken = default);
}
