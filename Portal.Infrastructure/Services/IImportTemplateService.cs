namespace Portal.Infrastructure.Services;

/// <summary>
/// Builds downloadable import templates (CSV and Excel) for the External Platform Sales Import
/// canonical contract. Templates contain the header row plus two example rows.
/// </summary>
public interface IImportTemplateService
{
    /// <summary>
    /// Builds a CSV template. When platformCode is null/empty, a neutral placeholder ("ABC") is used
    /// in the example invoice numbers.
    /// </summary>
    (byte[] Content, string FileName, string ContentType) BuildCsvTemplate(string? platformCode);

    /// <summary>
    /// Builds an Excel (.xlsx) template with a "Sales" sheet (header + two example rows) and an
    /// "Instructions" sheet describing each column.
    /// </summary>
    (byte[] Content, string FileName, string ContentType) BuildExcelTemplate(string? platformCode);
}
