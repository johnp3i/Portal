using Portal.Infrastructure.Models;

namespace Portal.Web.Services;

/// <summary>
/// Renders the credit note PDF Razor partial view to an HTML string using ViewRenderService.
/// </summary>
public class CreditNoteRenderer : ICreditNoteRenderer
{
    private readonly IViewRenderService _viewRenderService;

    public CreditNoteRenderer(IViewRenderService viewRenderService)
    {
        _viewRenderService = viewRenderService;
    }

    public async Task<string> RenderAsync(CreditNotePdfModel model)
    {
        return await _viewRenderService.RenderViewToStringAsync("~/Views/CreditNote/_CreditNotePdf.cshtml", model);
    }
}
