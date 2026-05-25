using Portal.Infrastructure.Models;

namespace Portal.Web.Services;

/// <summary>
/// Renders the statement Razor view to an HTML string for PDF conversion.
/// </summary>
public interface IStatementRenderer
{
    /// <summary>
    /// Renders the statement PDF Razor partial view to a self-contained HTML string.
    /// </summary>
    Task<string> RenderAsync(StatementPdfModel model);
}
