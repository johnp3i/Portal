using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;

namespace Portal.Web.Services;

/// <summary>
/// Renders the proposal snapshot Razor view to an HTML string using ViewRenderService.
/// </summary>
public class ProposalRenderer : IProposalRenderer
{
    private readonly IViewRenderService _viewRenderService;

    public ProposalRenderer(IViewRenderService viewRenderService)
    {
        _viewRenderService = viewRenderService;
    }

    public async Task<string> RenderAsync(ProposalRenderModel model)
    {
        return await _viewRenderService.RenderViewToStringAsync("~/Views/Proposal/Snapshot.cshtml", model);
    }
}
