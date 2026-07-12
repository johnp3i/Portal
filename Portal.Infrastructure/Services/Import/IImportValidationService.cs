using Portal.Infrastructure.Models.Import;

namespace Portal.Infrastructure.Services.Import;

/// <summary>
/// Row-level validation against business rules for imported purchase rows.
/// </summary>
public interface IImportValidationService
{
    Task<List<ValidatedRow>> ValidateRowsAsync(List<ParsedRow> rows, int supplierId, int businessId);
    Task<ValidatedRow> ValidateRowAsync(ParsedRow row, int supplierId, int businessId);
}
