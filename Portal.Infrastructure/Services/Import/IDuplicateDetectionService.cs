using Portal.Infrastructure.Models.Import;

namespace Portal.Infrastructure.Services.Import;

/// <summary>
/// Advisory duplicate checking for import rows against existing purchases.
/// </summary>
public interface IDuplicateDetectionService
{
    Task<List<DuplicateCheckResult>> CheckDuplicatesAsync(List<ValidatedRow> rows, int supplierId, int businessId);
}
