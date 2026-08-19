using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for ProductPriceHistory entity operations against the [product].[ProductPriceHistory] table.
/// Append-only — no UPDATE or DELETE operations.
/// </summary>
public class ProductPriceHistoryRepository : GenericStoredProcedureRepository<ProductPriceHistory>
{
    public ProductPriceHistoryRepository(DbContext context) : base(context) { }

    public virtual async Task InsertAsync(ProductPriceHistory entry)
    {
        try
        {
            const string query = @"
                INSERT INTO [product].[ProductPriceHistory]
                    ([ProductId], [SellingPrice], [CostPrice], [EffectiveFromUtc], [ChangedByUserId])
                VALUES
                    (@ProductId, @SellingPrice, @CostPrice, @EffectiveFromUtc, @ChangedByUserId)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@ProductId", entry.ProductId),
                new SqlParameter("@SellingPrice", entry.SellingPrice),
                new SqlParameter("@CostPrice", entry.CostPrice),
                new SqlParameter("@EffectiveFromUtc", entry.EffectiveFromUtc),
                new SqlParameter("@ChangedByUserId", entry.ChangedByUserId)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task InsertTierPriceHistoryAsync(int productId, int productPriceTierId, decimal sellingPrice, decimal costPrice, string userId)
    {
        try
        {
            const string query = @"
                INSERT INTO [product].[ProductPriceHistory]
                    ([ProductId], [ProductPriceTierId], [SellingPrice], [CostPrice], [EffectiveFromUtc], [ChangedByUserId])
                VALUES
                    (@ProductId, @ProductPriceTierId, @SellingPrice, @CostPrice, @EffectiveFromUtc, @ChangedByUserId)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@ProductId", productId),
                new SqlParameter("@ProductPriceTierId", productPriceTierId),
                new SqlParameter("@SellingPrice", sellingPrice),
                new SqlParameter("@CostPrice", costPrice),
                new SqlParameter("@EffectiveFromUtc", DateTime.UtcNow),
                new SqlParameter("@ChangedByUserId", userId)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task<List<ProductPriceHistory>> GetByProductIdAsync(int productId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [ProductId], [ProductPriceTierId], [SellingPrice], [CostPrice], [EffectiveFromUtc], [ChangedByUserId], [CreatedAtUtc]
                FROM [product].[ProductPriceHistory]
                WHERE [ProductId] = @ProductId
                ORDER BY [EffectiveFromUtc] DESC";

            return await ExecuteStoredProcedure(query, new SqlParameter("@ProductId", productId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
