using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Imports the prospecting workbook ("30 Prospects" sheet) into a campaign. Parse + preview
/// is side-effect free; confirm writes in a transaction. Re-import is safe (idempotent) via the
/// workbook ID / name+website+email duplicate match — existing prospects are updated, not doubled.
/// </summary>
public interface IProspectImportService
{
    /// <summary>Parses and validates the workbook against a campaign. No DB writes.</summary>
    Task<ServiceResult<ProspectImportPreview>> ParseAndPreviewAsync(Stream fileStream, string fileName, int campaignId);

    /// <summary>Applies a previously generated preview: inserts new prospects, updates duplicates.</summary>
    Task<ServiceResult<ProspectImportResult>> ConfirmImportAsync(ProspectImportPreview preview);

    /// <summary>
    /// Builds a downloadable .xlsx template with the "30 Prospects" sheet, the exact headers the
    /// importer expects, and a few realistic example rows. Returned as raw bytes.
    /// </summary>
    byte[] GenerateTemplate();
}
