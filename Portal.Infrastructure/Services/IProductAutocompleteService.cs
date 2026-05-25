using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Service interface for product autocomplete search across the Product catalog
/// and historical InvoiceLine/QuotationLine records.
/// </summary>
public interface IProductAutocompleteService
{
    /// <summary>
    /// Searches products and historical line items matching the query.
    /// Returns results sorted by most recent date, limited to maxResults.
    /// Returns empty list if query is less than 2 characters or on any exception.
    /// </summary>
    /// <param name="query">The search text (minimum 2 characters).</param>
    /// <param name="maxResults">Maximum number of results to return (default 20).</param>
    Task<List<AutocompleteResultDto>> SearchAsync(string query, int maxResults = 20);
}
