using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for Product entity CRUD operations against the [product].[Product] table.
/// </summary>
public class ProductRepository : GenericStoredProcedureRepository<Product>
{
    public ProductRepository(DbContext context) : base(context) { }

    public virtual async Task<(List<Product> Items, int TotalCount)> GetPagedByBusinessIdAsync(
        int businessId,
        ProductListFilter filter,
        int offset,
        int pageSize)
    {
        try
        {
            // ─── Whitelist the status filter → WHERE fragment (no user text reaches SQL) ───
            string statusClause = (filter.Status ?? "active").ToLowerInvariant() switch
            {
                "inactive" => " AND [product].[Product].[IsActive] = 0",
                "all"      => string.Empty,
                _          => " AND [product].[Product].[IsActive] = 1"   // "active" (default)
            };

            // ─── Whitelist the sort key → ORDER BY fragment ───
            string orderClause = (filter.Sort ?? "code").ToLowerInvariant() switch
            {
                "created_desc" => "[product].[Product].[CreatedAtUtc] DESC, [product].[Product].[ProductCode] ASC",
                "created_asc"  => "[product].[Product].[CreatedAtUtc] ASC, [product].[Product].[ProductCode] ASC",
                "price_desc"   => "[product].[Product].[DefaultSellingPrice] DESC, [product].[Product].[ProductCode] ASC",
                "price_asc"    => "[product].[Product].[DefaultSellingPrice] ASC, [product].[Product].[ProductCode] ASC",
                "lastused_desc"=> "[product].[Product].[LastUsedDate] DESC, [product].[Product].[ProductCode] ASC",
                _              => "[product].[Product].[ProductCode] ASC"   // "code" (default)
            };

            string query = $@"
                SELECT [product].[Product].[Id],
                       [product].[Product].[BusinessId],
                       [product].[Product].[ProductCode],
                       [product].[Product].[Description],
                       [product].[Product].[DefaultSellingPrice],
                       [product].[Product].[DefaultCostPrice],
                       [product].[Product].[DefaultVatRate],
                       [product].[Product].[SupplierId],
                       [product].[Product].[IsActive],
                       [product].[Product].[LastUsedDate],
                       [product].[Product].[CreatedAtUtc],
                       [product].[Product].[ProductTypeId],
                       [purchase].[Supplier].[Name] AS [SupplierName],
                       COUNT(*) OVER() AS [TotalCount]
                FROM [product].[Product]
                LEFT JOIN [purchase].[Supplier]
                    ON [product].[Product].[SupplierId] = [purchase].[Supplier].[Id]
                WHERE [product].[Product].[BusinessId] = @BusinessId
                  AND (@Search IS NULL
                       OR [product].[Product].[ProductCode] LIKE '%' + @Search + '%'
                       OR [product].[Product].[Description] LIKE '%' + @Search + '%')
                  AND (@SupplierId IS NULL OR [product].[Product].[SupplierId] = @SupplierId)
                  AND (@ProductTypeId IS NULL OR [product].[Product].[ProductTypeId] = @ProductTypeId)
                  AND (@CreatedFrom IS NULL OR [product].[Product].[CreatedAtUtc] >= @CreatedFrom)
                  AND (@CreatedTo IS NULL OR [product].[Product].[CreatedAtUtc] < @CreatedTo){statusClause}
                ORDER BY {orderClause}
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var results = new List<Product>();
            int totalCount = 0;
            var connection = _context.Database.GetDbConnection();

            string? escapedSearch = filter.Search != null
                ? filter.Search.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]")
                : null;

            // CreatedTo is treated as inclusive of the whole day → compare against the next day (exclusive upper bound).
            DateTime? createdFrom = filter.CreatedFrom.HasValue
                ? filter.CreatedFrom.Value.ToDateTime(TimeOnly.MinValue)
                : null;
            DateTime? createdTo = filter.CreatedTo.HasValue
                ? filter.CreatedTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue)
                : null;

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@Search", (object?)escapedSearch ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@SupplierId", (object?)filter.SupplierId ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@ProductTypeId", (object?)filter.ProductTypeId ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@CreatedFrom", (object?)createdFrom ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@CreatedTo", (object?)createdTo ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Offset", offset));
                command.Parameters.Add(new SqlParameter("@PageSize", pageSize));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    totalCount = reader.GetInt32(reader.GetOrdinal("TotalCount"));
                    var product = new Product
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                        BusinessId = reader.GetInt32(reader.GetOrdinal("BusinessId")),
                        ProductCode = reader.GetString(reader.GetOrdinal("ProductCode")),
                        Description = reader.GetString(reader.GetOrdinal("Description")),
                        DefaultSellingPrice = reader.GetDecimal(reader.GetOrdinal("DefaultSellingPrice")),
                        DefaultCostPrice = reader.GetDecimal(reader.GetOrdinal("DefaultCostPrice")),
                        DefaultVatRate = reader.GetDecimal(reader.GetOrdinal("DefaultVatRate")),
                        SupplierId = reader.IsDBNull(reader.GetOrdinal("SupplierId"))
                            ? null
                            : reader.GetInt32(reader.GetOrdinal("SupplierId")),
                        IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                        LastUsedDate = reader.IsDBNull(reader.GetOrdinal("LastUsedDate"))
                            ? null
                            : reader.GetDateTime(reader.GetOrdinal("LastUsedDate")),
                        CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc")),
                        ProductTypeId = reader.IsDBNull(reader.GetOrdinal("ProductTypeId"))
                            ? null
                            : reader.GetInt32(reader.GetOrdinal("ProductTypeId"))
                    };

                    // Hydrate the Supplier navigation name so the view can render it without an N+1.
                    if (product.SupplierId.HasValue && !reader.IsDBNull(reader.GetOrdinal("SupplierName")))
                    {
                        product.Supplier = new Supplier
                        {
                            Id = product.SupplierId.Value,
                            Name = reader.GetString(reader.GetOrdinal("SupplierName"))
                        };
                    }

                    results.Add(product);
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return (results, totalCount);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task<Product?> GetByIdAndBusinessIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [ProductCode], [Description],
                       [DefaultSellingPrice], [DefaultCostPrice], [DefaultVatRate],
                       [SupplierId], [IsActive], [LastUsedDate], [CreatedAtUtc],
                       [ProductTypeId]
                FROM [product].[Product]
                WHERE [product].[Product].[Id] = @Id
                  AND [product].[Product].[BusinessId] = @BusinessId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task<Product?> GetByProductCodeAndBusinessIdAsync(string productCode, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [ProductCode], [Description],
                       [DefaultSellingPrice], [DefaultCostPrice], [DefaultVatRate],
                       [SupplierId], [IsActive], [LastUsedDate], [CreatedAtUtc],
                       [ProductTypeId]
                FROM [product].[Product]
                WHERE [product].[Product].[ProductCode] = @ProductCode
                  AND [product].[Product].[BusinessId] = @BusinessId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@ProductCode", productCode),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task<Product?> GetByDescriptionAndBusinessIdAsync(string description, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [ProductCode], [Description],
                       [DefaultSellingPrice], [DefaultCostPrice], [DefaultVatRate],
                       [SupplierId], [IsActive], [LastUsedDate], [CreatedAtUtc],
                       [ProductTypeId]
                FROM [product].[Product]
                WHERE [product].[Product].[Description] = @Description
                  AND [product].[Product].[BusinessId] = @BusinessId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Description", description),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task<int> InsertAsync(Product product)
    {
        try
        {
            const string query = @"
                INSERT INTO [product].[Product]
                    ([BusinessId], [ProductCode], [Description], [DefaultSellingPrice],
                     [DefaultCostPrice], [DefaultVatRate], [SupplierId], [IsActive],
                     [LastUsedDate], [CreatedAtUtc], [ProductTypeId])
                VALUES
                    (@BusinessId, @ProductCode, @Description, @DefaultSellingPrice,
                     @DefaultCostPrice, @DefaultVatRate, @SupplierId, @IsActive,
                     @LastUsedDate, @CreatedAtUtc, @ProductTypeId);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@BusinessId", product.BusinessId));
                command.Parameters.Add(new SqlParameter("@ProductCode", product.ProductCode));
                command.Parameters.Add(new SqlParameter("@Description", product.Description));
                command.Parameters.Add(new SqlParameter("@DefaultSellingPrice", product.DefaultSellingPrice));
                command.Parameters.Add(new SqlParameter("@DefaultCostPrice", product.DefaultCostPrice));
                command.Parameters.Add(new SqlParameter("@DefaultVatRate", product.DefaultVatRate));
                command.Parameters.Add(new SqlParameter("@SupplierId", product.SupplierId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@IsActive", product.IsActive));
                command.Parameters.Add(new SqlParameter("@LastUsedDate", product.LastUsedDate ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@CreatedAtUtc", product.CreatedAtUtc));
                command.Parameters.Add(new SqlParameter("@ProductTypeId", product.ProductTypeId ?? (object)DBNull.Value));

                var result = await command.ExecuteScalarAsync();
                var insertedId = result != null ? Convert.ToInt32(result) : 0;
                product.Id = insertedId;
                return insertedId;
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task UpdateAsync(Product product)
    {
        try
        {
            const string query = @"
                UPDATE [product].[Product]
                SET
                    [ProductCode] = @ProductCode,
                    [Description] = @Description,
                    [DefaultSellingPrice] = @DefaultSellingPrice,
                    [DefaultCostPrice] = @DefaultCostPrice,
                    [DefaultVatRate] = @DefaultVatRate,
                    [SupplierId] = @SupplierId,
                    [IsActive] = @IsActive,
                    [LastUsedDate] = @LastUsedDate,
                    [ProductTypeId] = @ProductTypeId
                WHERE [product].[Product].[Id] = @Id
                  AND [product].[Product].[BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", product.Id),
                new SqlParameter("@BusinessId", product.BusinessId),
                new SqlParameter("@ProductCode", product.ProductCode),
                new SqlParameter("@Description", product.Description),
                new SqlParameter("@DefaultSellingPrice", product.DefaultSellingPrice),
                new SqlParameter("@DefaultCostPrice", product.DefaultCostPrice),
                new SqlParameter("@DefaultVatRate", product.DefaultVatRate),
                new SqlParameter("@SupplierId", product.SupplierId ?? (object)DBNull.Value),
                new SqlParameter("@IsActive", product.IsActive),
                new SqlParameter("@LastUsedDate", product.LastUsedDate ?? (object)DBNull.Value),
                new SqlParameter("@ProductTypeId", product.ProductTypeId ?? (object)DBNull.Value)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task DeactivateAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [product].[Product]
                SET
                    [IsActive] = 0
                WHERE [product].[Product].[Id] = @Id
                  AND [product].[Product].[BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task<ProductKpiDto> GetKpiDataAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT
                    (SELECT COUNT(*)
                     FROM [product].[Product]
                     WHERE [product].[Product].[BusinessId] = @BusinessId) AS [TotalProducts],

                    (SELECT COUNT(*)
                     FROM [product].[Product]
                     WHERE [product].[Product].[BusinessId] = @BusinessId
                       AND [product].[Product].[IsActive] = 1) AS [ActiveProducts],

                    (SELECT ISNULL(AVG([product].[Product].[DefaultSellingPrice]), 0)
                     FROM [product].[Product]
                     WHERE [product].[Product].[BusinessId] = @BusinessId
                       AND [product].[Product].[IsActive] = 1) AS [AverageSellingPrice],

                    (SELECT TOP 1 [product].[Product].[Description]
                     FROM [product].[Product]
                     LEFT JOIN (
                         SELECT [invoice].[InvoiceLine].[ProductCode], COUNT(*) AS [UsageCount]
                         FROM [invoice].[InvoiceLine]
                         INNER JOIN [invoice].[Invoice]
                             ON [invoice].[InvoiceLine].[InvoiceId] = [invoice].[Invoice].[Id]
                         WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
                           AND [invoice].[InvoiceLine].[ProductCode] IS NOT NULL
                         GROUP BY [invoice].[InvoiceLine].[ProductCode]
                     ) AS InvoiceUsage ON [product].[Product].[ProductCode] = InvoiceUsage.[ProductCode]
                     LEFT JOIN (
                         SELECT [quotation].[QuotationLine].[ProductCode], COUNT(*) AS [UsageCount]
                         FROM [quotation].[QuotationLine]
                         INNER JOIN [quotation].[Quotation]
                             ON [quotation].[QuotationLine].[QuotationId] = [quotation].[Quotation].[Id]
                         WHERE [quotation].[Quotation].[BusinessId] = @BusinessId
                           AND [quotation].[QuotationLine].[ProductCode] IS NOT NULL
                         GROUP BY [quotation].[QuotationLine].[ProductCode]
                     ) AS QuotationUsage ON [product].[Product].[ProductCode] = QuotationUsage.[ProductCode]
                     WHERE [product].[Product].[BusinessId] = @BusinessId
                     ORDER BY (ISNULL(InvoiceUsage.[UsageCount], 0) + ISNULL(QuotationUsage.[UsageCount], 0)) DESC
                    ) AS [BestSellerDescription],

                    (SELECT TOP 1 (ISNULL(InvoiceUsage.[UsageCount], 0) + ISNULL(QuotationUsage.[UsageCount], 0))
                     FROM [product].[Product]
                     LEFT JOIN (
                         SELECT [invoice].[InvoiceLine].[ProductCode], COUNT(*) AS [UsageCount]
                         FROM [invoice].[InvoiceLine]
                         INNER JOIN [invoice].[Invoice]
                             ON [invoice].[InvoiceLine].[InvoiceId] = [invoice].[Invoice].[Id]
                         WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
                           AND [invoice].[InvoiceLine].[ProductCode] IS NOT NULL
                         GROUP BY [invoice].[InvoiceLine].[ProductCode]
                     ) AS InvoiceUsage ON [product].[Product].[ProductCode] = InvoiceUsage.[ProductCode]
                     LEFT JOIN (
                         SELECT [quotation].[QuotationLine].[ProductCode], COUNT(*) AS [UsageCount]
                         FROM [quotation].[QuotationLine]
                         INNER JOIN [quotation].[Quotation]
                             ON [quotation].[QuotationLine].[QuotationId] = [quotation].[Quotation].[Id]
                         WHERE [quotation].[Quotation].[BusinessId] = @BusinessId
                           AND [quotation].[QuotationLine].[ProductCode] IS NOT NULL
                         GROUP BY [quotation].[QuotationLine].[ProductCode]
                     ) AS QuotationUsage ON [product].[Product].[ProductCode] = QuotationUsage.[ProductCode]
                     WHERE [product].[Product].[BusinessId] = @BusinessId
                     ORDER BY (ISNULL(InvoiceUsage.[UsageCount], 0) + ISNULL(QuotationUsage.[UsageCount], 0)) DESC
                    ) AS [BestSellerUsageCount]";

            var kpi = new ProductKpiDto();
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    kpi.TotalProducts = reader.GetInt32(reader.GetOrdinal("TotalProducts"));
                    kpi.ActiveProducts = reader.GetInt32(reader.GetOrdinal("ActiveProducts"));
                    kpi.AverageSellingPrice = reader.GetDecimal(reader.GetOrdinal("AverageSellingPrice"));
                    kpi.BestSellerDescription = reader.IsDBNull(reader.GetOrdinal("BestSellerDescription"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("BestSellerDescription"));
                    kpi.BestSellerUsageCount = reader.IsDBNull(reader.GetOrdinal("BestSellerUsageCount"))
                        ? 0
                        : reader.GetInt32(reader.GetOrdinal("BestSellerUsageCount"));
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return kpi;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task<List<ProductUsageDto>> GetTopByUsageAsync(int businessId, int top)
    {
        try
        {
            const string query = @"
                SELECT TOP (@Top)
                    [product].[Product].[Description],
                    (ISNULL(InvoiceUsage.[UsageCount], 0) + ISNULL(QuotationUsage.[UsageCount], 0)) AS [UsageCount]
                FROM [product].[Product]
                LEFT JOIN (
                    SELECT [invoice].[InvoiceLine].[ProductCode], COUNT(*) AS [UsageCount]
                    FROM [invoice].[InvoiceLine]
                    INNER JOIN [invoice].[Invoice]
                        ON [invoice].[InvoiceLine].[InvoiceId] = [invoice].[Invoice].[Id]
                    WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
                      AND [invoice].[InvoiceLine].[ProductCode] IS NOT NULL
                    GROUP BY [invoice].[InvoiceLine].[ProductCode]
                ) AS InvoiceUsage ON [product].[Product].[ProductCode] = InvoiceUsage.[ProductCode]
                LEFT JOIN (
                    SELECT [quotation].[QuotationLine].[ProductCode], COUNT(*) AS [UsageCount]
                    FROM [quotation].[QuotationLine]
                    INNER JOIN [quotation].[Quotation]
                        ON [quotation].[QuotationLine].[QuotationId] = [quotation].[Quotation].[Id]
                    WHERE [quotation].[Quotation].[BusinessId] = @BusinessId
                      AND [quotation].[QuotationLine].[ProductCode] IS NOT NULL
                    GROUP BY [quotation].[QuotationLine].[ProductCode]
                ) AS QuotationUsage ON [product].[Product].[ProductCode] = QuotationUsage.[ProductCode]
                WHERE [product].[Product].[BusinessId] = @BusinessId
                ORDER BY (ISNULL(InvoiceUsage.[UsageCount], 0) + ISNULL(QuotationUsage.[UsageCount], 0)) DESC";

            var results = new List<ProductUsageDto>();
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@Top", top));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new ProductUsageDto
                    {
                        Description = reader.GetString(reader.GetOrdinal("Description")),
                        UsageCount = reader.GetInt32(reader.GetOrdinal("UsageCount"))
                    });
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return results;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task<List<Product>> SearchForAutocompleteAsync(int businessId, string query, int maxResults)
    {
        try
        {
            const string sql = @"
                SELECT TOP (@MaxResults)
                    [Id], [BusinessId], [ProductCode], [Description],
                    [DefaultSellingPrice], [DefaultCostPrice], [DefaultVatRate],
                    [SupplierId], [IsActive], [LastUsedDate], [CreatedAtUtc],
                    [ProductTypeId]
                FROM [product].[Product]
                WHERE [product].[Product].[BusinessId] = @BusinessId
                  AND [product].[Product].[IsActive] = 1
                  AND ([product].[Product].[ProductCode] LIKE '%' + @Query + '%'
                       OR [product].[Product].[Description] LIKE '%' + @Query + '%')
                ORDER BY [product].[Product].[LastUsedDate] DESC";

            string escapedQuery = query.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");

            return await ExecuteStoredProcedure(sql,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@Query", escapedQuery),
                new SqlParameter("@MaxResults", maxResults));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Top products by a performance measure over a date range, from issued, non-deleted invoices.
    /// The <paramref name="measure"/> is whitelisted to "revenue" (SUM of line totals) or
    /// "units" (SUM of quantity). The date range is [dateFrom, dateTo) on the invoice date;
    /// a null bound means unbounded on that side (used for "All Time").
    /// </summary>
    public virtual async Task<List<ProductPerformancePoint>> GetTopPerformanceAsync(
        int businessId,
        string measure,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        int top)
    {
        try
        {
            // Whitelist the measured expression — no user input reaches the SQL text.
            string valueExpr = measure == "units"
                ? "SUM([invoice].[InvoiceLine].[Quantity])"
                : "SUM([invoice].[InvoiceLine].[LineTotal])";   // "revenue" (default)

            string query = $@"
                SELECT TOP (@Top)
                    [invoice].[InvoiceLine].[Description] AS [Description],
                    {valueExpr} AS [Value]
                FROM [invoice].[InvoiceLine]
                INNER JOIN [invoice].[Invoice]
                    ON [invoice].[InvoiceLine].[InvoiceId] = [invoice].[Invoice].[Id]
                WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
                  AND [invoice].[Invoice].[InvoiceStatusTypeId] = 2
                  AND [invoice].[Invoice].[IsDeleted] = 0
                  AND [invoice].[InvoiceLine].[IsAdjustmentLine] = 0
                  AND (@DateFrom IS NULL OR [invoice].[Invoice].[InvoiceDate] >= @DateFrom)
                  AND (@DateTo IS NULL OR [invoice].[Invoice].[InvoiceDate] < @DateTo)
                GROUP BY [invoice].[InvoiceLine].[Description]
                HAVING {valueExpr} > 0
                ORDER BY {valueExpr} DESC";

            var results = new List<ProductPerformancePoint>();
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@Top", top));
                command.Parameters.Add(new SqlParameter("@DateFrom", (object?)dateFrom ?? DBNull.Value) { SqlDbType = SqlDbType.Date });
                command.Parameters.Add(new SqlParameter("@DateTo", (object?)dateTo ?? DBNull.Value) { SqlDbType = SqlDbType.Date });

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new ProductPerformancePoint
                    {
                        Description = reader.GetString(reader.GetOrdinal("Description")),
                        Value = reader.GetDecimal(reader.GetOrdinal("Value"))
                    });
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return results;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task UpdateDefaultPricesAsync(int productId, decimal sellingPrice, decimal costPrice)
    {
        try
        {
            const string query = @"
                UPDATE [product].[Product]
                SET
                    [DefaultSellingPrice] = @DefaultSellingPrice,
                    [DefaultCostPrice] = @DefaultCostPrice
                WHERE [product].[Product].[Id] = @ProductId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@ProductId", productId),
                new SqlParameter("@DefaultSellingPrice", sellingPrice),
                new SqlParameter("@DefaultCostPrice", costPrice)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
