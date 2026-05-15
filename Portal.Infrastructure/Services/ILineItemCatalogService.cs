using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for line item catalog management including search, population from quotations, and CRUD operations.
/// </summary>
public interface ILineItemCatalogService
{
    Task<List<LineItemCatalog>> SearchAsync(int businessId, string query);
    Task PopulateFromQuotationAsync(int quotationId, int businessId);
    Task<List<LineItemCatalog>> GetAllAsync(int businessId);
    Task<LineItemCatalog?> GetByIdAsync(int id, int businessId);
    Task DeleteAsync(int id, int businessId);
    Task UpdateAsync(LineItemCatalog entry, int businessId);
}
