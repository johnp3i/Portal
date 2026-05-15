using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for line item catalog management including search, population from quotations, and CRUD operations.
/// </summary>
public class LineItemCatalogService : ILineItemCatalogService
{
    private readonly LineItemCatalogRepository _catalogRepository;
    private readonly QuotationLineRepository _quotationLineRepository;

    public LineItemCatalogService(
        LineItemCatalogRepository catalogRepository,
        QuotationLineRepository quotationLineRepository)
    {
        _catalogRepository = catalogRepository;
        _quotationLineRepository = quotationLineRepository;
    }

    /// <summary>
    /// Searches catalog entries by description. Returns empty list for queries shorter than 2 characters.
    /// </summary>
    public async Task<List<LineItemCatalog>> SearchAsync(int businessId, string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            return new List<LineItemCatalog>();
        }

        return await _catalogRepository.SearchByDescriptionAsync(businessId, query.Trim());
    }

    /// <summary>
    /// Populates the catalog from quotation lines. For each line, creates or updates a catalog entry
    /// matched by BusinessId + Description (upsert). Latest values always win.
    /// </summary>
    public async Task PopulateFromQuotationAsync(int quotationId, int businessId)
    {
        var lines = await _quotationLineRepository.GetByQuotationIdAsync(quotationId);

        foreach (var line in lines)
        {
            var catalogEntry = new LineItemCatalog
            {
                BusinessId = businessId,
                Description = line.Description,
                UnitPrice = line.UnitPrice,
                VatRate = line.VatRate,
                ReferenceUrl = line.ReferenceUrl,
                Discount = line.Discount,
                DiscountType = line.DiscountType,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _catalogRepository.UpsertAsync(catalogEntry);
        }
    }

    /// <summary>
    /// Returns all catalog entries for the specified business.
    /// </summary>
    public async Task<List<LineItemCatalog>> GetAllAsync(int businessId)
    {
        return await _catalogRepository.GetAllByBusinessIdAsync(businessId);
    }

    /// <summary>
    /// Returns a single catalog entry by ID, validating it belongs to the specified business.
    /// </summary>
    public async Task<LineItemCatalog?> GetByIdAsync(int id, int businessId)
    {
        var entry = await _catalogRepository.GetByIdAsync(id);

        if (entry == null)
        {
            return null;
        }

        if (entry.BusinessId != businessId)
        {
            throw new UnauthorizedAccessException("Catalog entry does not belong to the current business.");
        }

        return entry;
    }

    /// <summary>
    /// Deletes a catalog entry after validating ownership (BusinessId match).
    /// </summary>
    public async Task DeleteAsync(int id, int businessId)
    {
        var entry = await _catalogRepository.GetByIdAsync(id);

        if (entry == null)
        {
            return;
        }

        if (entry.BusinessId != businessId)
        {
            throw new UnauthorizedAccessException("Catalog entry does not belong to the current business.");
        }

        await _catalogRepository.DeleteAsync(id);
    }

    /// <summary>
    /// Updates a catalog entry after validating ownership (BusinessId match).
    /// </summary>
    public async Task UpdateAsync(LineItemCatalog entry, int businessId)
    {
        var existing = await _catalogRepository.GetByIdAsync(entry.Id);

        if (existing == null)
        {
            throw new InvalidOperationException("Catalog entry not found.");
        }

        if (existing.BusinessId != businessId)
        {
            throw new UnauthorizedAccessException("Catalog entry does not belong to the current business.");
        }

        entry.UpdatedAtUtc = DateTime.UtcNow;
        await _catalogRepository.UpdateAsync(entry);
    }
}
