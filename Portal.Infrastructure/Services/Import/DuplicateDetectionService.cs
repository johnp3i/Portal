using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Models.Import;

namespace Portal.Infrastructure.Services.Import;

/// <summary>
/// Checks import rows against existing purchases for potential duplicates.
/// Match criteria: SupplierId + InvoiceNumber + InvoiceDate + TotalAmount.
/// Advisory only — does not block import.
/// </summary>
public class DuplicateDetectionService : IDuplicateDetectionService
{
    private readonly PortalDbContext _dbContext;

    public DuplicateDetectionService(PortalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<DuplicateCheckResult>> CheckDuplicatesAsync(List<ValidatedRow> rows, int supplierId, int businessId)
    {
        try
        {
            var results = new List<DuplicateCheckResult>();

            // Only check rows that have enough data for matching
            var rowsToCheck = rows
                .Select((r, i) => new { Row = r, Index = i })
                .Where(x => x.Row.Status != RowValidationStatus.Invalid
                    && x.Row.Data.InvoiceDate.HasValue
                    && x.Row.Data.TotalAmount.HasValue)
                .ToList();

            if (rowsToCheck.Count == 0)
                return results;

            // Load potential matches: all purchases for this supplier in the business
            var existingPurchases = await _dbContext.Purchases
                .Where(p => p.BusinessId == businessId
                    && p.SupplierId == supplierId
                    && !p.IsCancelled)
                .Select(p => new { p.Id, p.InvoiceNumber, p.InvoiceDate, p.TotalAmount })
                .ToListAsync();

            foreach (var item in rowsToCheck)
            {
                var row = item.Row.Data;
                var isDuplicate = false;
                int? matchedId = null;

                // Match on: InvoiceNumber + InvoiceDate + TotalAmount
                if (!string.IsNullOrEmpty(row.InvoiceNumber))
                {
                    var match = existingPurchases.FirstOrDefault(p =>
                        string.Equals(p.InvoiceNumber, row.InvoiceNumber, StringComparison.OrdinalIgnoreCase)
                        && p.InvoiceDate == row.InvoiceDate!.Value
                        && p.TotalAmount == row.TotalAmount!.Value);

                    if (match != null)
                    {
                        isDuplicate = true;
                        matchedId = match.Id;
                    }
                }
                else
                {
                    // Without invoice number, match on date + total only (weaker signal)
                    var match = existingPurchases.FirstOrDefault(p =>
                        p.InvoiceDate == row.InvoiceDate!.Value
                        && p.TotalAmount == row.TotalAmount!.Value
                        && string.IsNullOrEmpty(p.InvoiceNumber));

                    if (match != null)
                    {
                        isDuplicate = true;
                        matchedId = match.Id;
                    }
                }

                results.Add(new DuplicateCheckResult
                {
                    RowIndex = item.Index,
                    IsDuplicate = isDuplicate,
                    MatchedPurchaseId = matchedId
                });
            }

            return results;
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
