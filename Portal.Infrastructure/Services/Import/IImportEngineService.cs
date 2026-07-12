using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Import;

namespace Portal.Infrastructure.Services.Import;

/// <summary>
/// Orchestrates the full import lifecycle: parse → validate → preview → confirm.
/// </summary>
public interface IImportEngineService
{
    Task<ServiceResult<ImportSessionResult>> ParseFileAsync(Stream fileStream, string fileName, int supplierId, int? templateId, int businessId);
    Task<ServiceResult<ImportSessionResult>> RevalidateRowAsync(int sessionId, int rowIndex, string field, string value, int businessId, int supplierId);
    Task<ServiceResult> RemoveRowAsync(int sessionId, int rowIndex, int businessId);
    Task<ServiceResult<ImportConfirmationResult>> ConfirmImportAsync(int sessionId, int businessId, string userId);
}
