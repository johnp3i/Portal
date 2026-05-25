using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Searches the Product catalog and historical InvoiceLine/QuotationLine records
/// to provide autocomplete suggestions for line item forms.
/// </summary>
public class ProductAutocompleteService : IProductAutocompleteService
{
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ProductRepository _productRepository;
    private readonly PortalDbContext _dbContext;
    private readonly ILogger<ProductAutocompleteService> _logger;

    public ProductAutocompleteService(
        ICurrentTenantService currentTenantService,
        ProductRepository productRepository,
        PortalDbContext dbContext,
        ILogger<ProductAutocompleteService> logger)
    {
        _currentTenantService = currentTenantService;
        _productRepository = productRepository;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<AutocompleteResultDto>> SearchAsync(string query, int maxResults = 20)
    {
        try
        {
            // Enforce minimum 2-character query length
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
                return new List<AutocompleteResultDto>();

            var businessId = _currentTenantService.CurrentBusinessId;
            if (businessId == 0)
                return new List<AutocompleteResultDto>();

            var trimmedQuery = query.Trim();
            var results = new List<AutocompleteResultDto>();

            // 1. Search Product table for active products matching query
            var products = await _productRepository.SearchForAutocompleteAsync(businessId, trimmedQuery, maxResults);

            // Load supplier names for products that have a SupplierId
            var supplierNames = await GetSupplierNamesAsync(
                products.Where(p => p.SupplierId.HasValue).Select(p => p.SupplierId!.Value).Distinct().ToList(),
                businessId);

            foreach (var product in products)
            {
                results.Add(new AutocompleteResultDto
                {
                    Source = "Product",
                    ProductCode = product.ProductCode,
                    Description = product.Description,
                    UnitPrice = product.DefaultSellingPrice,
                    VatRate = product.DefaultVatRate,
                    CostPrice = product.DefaultCostPrice,
                    SupplierName = product.SupplierId.HasValue && supplierNames.ContainsKey(product.SupplierId.Value)
                        ? supplierNames[product.SupplierId.Value]
                        : null,
                    Date = product.LastUsedDate
                });
            }

            // 2. Search InvoiceLine history
            var invoiceLineResults = await SearchInvoiceLinesAsync(businessId, trimmedQuery, maxResults);
            results.AddRange(invoiceLineResults);

            // 3. Search QuotationLine history
            var quotationLineResults = await SearchQuotationLinesAsync(businessId, trimmedQuery, maxResults);
            results.AddRange(quotationLineResults);

            // 4. Combine all results, sort by Date descending (most recent first), limit to maxResults
            return results
                .OrderByDescending(r => r.Date ?? DateTime.MinValue)
                .Take(maxResults)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Autocomplete search failed for query={Query}", query);
            return new List<AutocompleteResultDto>();
        }
    }

    private async Task<List<AutocompleteResultDto>> SearchInvoiceLinesAsync(int businessId, string query, int maxResults)
    {
        const string sql = @"
            SELECT TOP (@MaxResults)
                [invoice].[InvoiceLine].[Description],
                [invoice].[InvoiceLine].[UnitPrice],
                [invoice].[InvoiceLine].[VatRate],
                [invoice].[InvoiceLine].[CostPrice],
                [invoice].[InvoiceLine].[ProductCode],
                [invoice].[Invoice].[InvoiceDate]
            FROM [invoice].[InvoiceLine]
            INNER JOIN [invoice].[Invoice]
                ON [invoice].[InvoiceLine].[InvoiceId] = [invoice].[Invoice].[Id]
            WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
              AND [invoice].[Invoice].[IsDeleted] = 0
              AND ([invoice].[InvoiceLine].[Description] LIKE '%' + @Query + '%'
                   OR [invoice].[InvoiceLine].[ProductCode] LIKE '%' + @Query + '%')
            ORDER BY [invoice].[Invoice].[InvoiceDate] DESC";

        string escapedQuery = query.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");

        var results = new List<AutocompleteResultDto>();
        var connection = _dbContext.Database.GetDbConnection();

        try
        {
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
            command.Parameters.Add(new SqlParameter("@Query", escapedQuery));
            command.Parameters.Add(new SqlParameter("@MaxResults", maxResults));

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new AutocompleteResultDto
                {
                    Source = "Invoice",
                    Description = reader.GetString(reader.GetOrdinal("Description")),
                    UnitPrice = reader.GetDecimal(reader.GetOrdinal("UnitPrice")),
                    VatRate = reader.GetDecimal(reader.GetOrdinal("VatRate")),
                    CostPrice = reader.IsDBNull(reader.GetOrdinal("CostPrice"))
                        ? null
                        : reader.GetDecimal(reader.GetOrdinal("CostPrice")),
                    ProductCode = reader.IsDBNull(reader.GetOrdinal("ProductCode"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("ProductCode")),
                    SupplierName = null,
                    Date = reader.GetFieldValue<DateOnly>(reader.GetOrdinal("InvoiceDate")).ToDateTime(TimeOnly.MinValue)
                });
            }
        }
        finally
        {
            if (connection.State == ConnectionState.Open && _dbContext.Database.CurrentTransaction == null)
                await connection.CloseAsync();
        }

        return results;
    }

    private async Task<List<AutocompleteResultDto>> SearchQuotationLinesAsync(int businessId, string query, int maxResults)
    {
        const string sql = @"
            SELECT TOP (@MaxResults)
                [quotation].[QuotationLine].[Description],
                [quotation].[QuotationLine].[UnitPrice],
                [quotation].[QuotationLine].[VatRate],
                [quotation].[QuotationLine].[CostPrice],
                [quotation].[QuotationLine].[ProductCode],
                [quotation].[Quotation].[CreatedAtUtc]
            FROM [quotation].[QuotationLine]
            INNER JOIN [quotation].[Quotation]
                ON [quotation].[QuotationLine].[QuotationId] = [quotation].[Quotation].[Id]
            WHERE [quotation].[Quotation].[BusinessId] = @BusinessId
              AND [quotation].[Quotation].[IsDeleted] = 0
              AND ([quotation].[QuotationLine].[Description] LIKE '%' + @Query + '%'
                   OR [quotation].[QuotationLine].[ProductCode] LIKE '%' + @Query + '%')
            ORDER BY [quotation].[Quotation].[CreatedAtUtc] DESC";

        string escapedQuery = query.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");

        var results = new List<AutocompleteResultDto>();
        var connection = _dbContext.Database.GetDbConnection();

        try
        {
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
            command.Parameters.Add(new SqlParameter("@Query", escapedQuery));
            command.Parameters.Add(new SqlParameter("@MaxResults", maxResults));

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new AutocompleteResultDto
                {
                    Source = "Quotation",
                    Description = reader.GetString(reader.GetOrdinal("Description")),
                    UnitPrice = reader.GetDecimal(reader.GetOrdinal("UnitPrice")),
                    VatRate = reader.GetDecimal(reader.GetOrdinal("VatRate")),
                    CostPrice = reader.IsDBNull(reader.GetOrdinal("CostPrice"))
                        ? null
                        : reader.GetDecimal(reader.GetOrdinal("CostPrice")),
                    ProductCode = reader.IsDBNull(reader.GetOrdinal("ProductCode"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("ProductCode")),
                    SupplierName = null,
                    Date = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"))
                });
            }
        }
        finally
        {
            if (connection.State == ConnectionState.Open && _dbContext.Database.CurrentTransaction == null)
                await connection.CloseAsync();
        }

        return results;
    }

    private async Task<Dictionary<int, string>> GetSupplierNamesAsync(List<int> supplierIds, int businessId)
    {
        if (!supplierIds.Any())
            return new Dictionary<int, string>();

        var result = new Dictionary<int, string>();
        var connection = _dbContext.Database.GetDbConnection();

        // Build parameterized IN clause
        var paramNames = supplierIds.Select((_, i) => $"@SupplierId{i}").ToList();
        var inClause = string.Join(", ", paramNames);

        var sql = $@"
            SELECT [purchase].[Supplier].[Id], [purchase].[Supplier].[Name]
            FROM [purchase].[Supplier]
            WHERE [purchase].[Supplier].[Id] IN ({inClause})
              AND [purchase].[Supplier].[BusinessId] = @BusinessId";

        try
        {
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.Add(new SqlParameter("@BusinessId", businessId));

            for (int i = 0; i < supplierIds.Count; i++)
            {
                command.Parameters.Add(new SqlParameter(paramNames[i], supplierIds[i]));
            }

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32(reader.GetOrdinal("Id"));
                var name = reader.GetString(reader.GetOrdinal("Name"));
                result[id] = name;
            }
        }
        finally
        {
            if (connection.State == ConnectionState.Open && _dbContext.Database.CurrentTransaction == null)
                await connection.CloseAsync();
        }

        return result;
    }
}
