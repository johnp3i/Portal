using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Orchestrates proposal sharing: snapshot generation, token creation, email dispatch, and retrieval.
/// </summary>
public interface IProposalService
{
    Task<ProposalShare> ShareAsync(int quotationId, DateTimeOffset expiresAtUtc, List<int> heroLogoIds, int? metaLogoId, string userId, string? recipientEmail = null, bool sendEmail = true);
    Task<string> PreviewAsync(int quotationId, List<int> heroLogoIds, int? metaLogoId);

    /// <summary>
    /// Builds the ProposalRenderModel for a quotation without rendering to HTML.
    /// Used by the PDF service to render a dedicated print-optimised view.
    /// </summary>
    Task<ProposalRenderModel> GetRenderModelAsync(int quotationId, List<int> heroLogoIds, int? metaLogoId);

    /// <summary>
    /// Builds the ProposalRenderModel for a quotation using an explicit business ID.
    /// Used for anonymous PDF generation from shared proposal links.
    /// </summary>
    Task<ProposalRenderModel> GetRenderModelAsync(int quotationId, int businessId, List<int> heroLogoIds, int? metaLogoId);

    Task<ProposalShare?> GetByTokenAsync(string token);
    Task<ProposalShare?> GetActiveShareByQuotationIdAsync(int quotationId);
    Task<List<ProposalShare>> GetSharesByQuotationIdAsync(int quotationId);
}
