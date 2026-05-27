using Portal.Infrastructure.Models;

namespace Portal.Web.Services;

/// <summary>
/// Renders the credit note Razor view to an HTML string for PDF conversion.
/// </summary>
public interface ICreditNoteRenderer
{
    /// <summary>
    /// Renders the credit note PDF Razor partial view to a self-contained HTML string.
    /// </summary>
    Task<string> RenderAsync(CreditNotePdfModel model);
}
