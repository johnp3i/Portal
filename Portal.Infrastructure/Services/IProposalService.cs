using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Orchestrates proposal sharing: snapshot generation, token creation, email dispatch, and retrieval.
/// </summary>
public interface IProposalService
{
    Task<ProposalShare> ShareAsync(int quotationId, DateTimeOffset expiresAtUtc, List<int> heroLogoIds, int? metaLogoId, string userId);
    Task<string> PreviewAsync(int quotationId, List<int> heroLogoIds, int? metaLogoId);
    Task<ProposalShare?> GetByTokenAsync(string token);
    Task<ProposalShare?> GetActiveShareByQuotationIdAsync(int quotationId);
    Task<List<ProposalShare>> GetSharesByQuotationIdAsync(int quotationId);
}
