using Portal.Infrastructure.Models;

namespace Portal.Web.Services;

/// <summary>
/// Renders the statement PDF Razor partial view to an HTML string using ViewRenderService.
/// </summary>
public class StatementRenderer : IStatementRenderer
{
    private readonly IViewRenderService _viewRenderService;

    public StatementRenderer(IViewRenderService viewRenderService)
    {
        _viewRenderService = viewRenderService;
    }

    public async Task<string> RenderAsync(StatementPdfModel model)
    {
        return await _viewRenderService.RenderViewToStringAsync("~/Views/Statement/_StatementPdf.cshtml", model);
    }
}
