using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Renders a ProposalRenderModel into a self-contained HTML string.
/// </summary>
public interface IProposalRenderer
{
    Task<string> RenderAsync(ProposalRenderModel model);
}
