using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for ProductPriceTier entity CRUD operations against the [product].[ProductPriceTier] table.
/// </summary>
public class ProductPriceTierRepository : GenericStoredProcedureRepository<ProductPriceTier>
{
    public ProductPriceTierRepository(DbContext context) : base(context) { }

    public virtual async Task<int> InsertAsync(ProductPriceTier tier)
    {
        try
        {
            const string query = @"
                INSERT INTO [product].[ProductPriceTier]
                    ([ProductId], [TierName], [SellingPrice], [CostPrice],
                     [IsDefault], [IsActive], [CreatedAtUtc], [UpdatedAtUtc])
                OUTPUT INSERTED.[Id]
                VALUES
                    (@ProductId, @TierName, @SellingPrice, @CostPrice,
                     @IsDefault, @IsActive, @CreatedAtUtc, @UpdatedAtUtc)";

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

                command.Parameters.Add(new SqlParameter("@ProductId", tier.ProductId));
                command.Parameters.Add(new SqlParameter("@TierName", tier.TierName));
                command.Parameters.Add(new SqlParameter("@SellingPrice", tier.SellingPrice));
                command.Parameters.Add(new SqlParameter("@CostPrice", tier.CostPrice));
                command.Parameters.Add(new SqlParameter("@IsDefault", tier.IsDefault));
                command.Parameters.Add(new SqlParameter("@IsActive", tier.IsActive));
                command.Parameters.Add(new SqlParameter("@CreatedAtUtc", tier.CreatedAtUtc));
                command.Parameters.Add(new SqlParameter("@UpdatedAtUtc", tier.UpdatedAtUtc));

                var result = await command.ExecuteScalarAsync();
                var insertedId = result != null ? Convert.ToInt32(result) : 0;
                tier.Id = insertedId;
                return insertedId;
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task UpdateAsync(ProductPriceTier tier)
    {
        try
        {
            const string query = @"
                UPDATE [product].[ProductPriceTier]
                SET
                    [TierName] = @TierName,
                    [SellingPrice] = @SellingPrice,
                    [CostPrice] = @CostPrice,
                    [UpdatedAtUtc] = @UpdatedAtUtc
                WHERE [product].[ProductPriceTier].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", tier.Id),
                new SqlParameter("@TierName", tier.TierName),
                new SqlParameter("@SellingPrice", tier.SellingPrice),
                new SqlParameter("@CostPrice", tier.CostPrice),
                new SqlParameter("@UpdatedAtUtc", tier.UpdatedAtUtc)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task SetDefaultFlagAsync(int tierId, bool isDefault)
    {
        try
        {
            const string query = @"
                UPDATE [product].[ProductPriceTier]
                SET
                    [IsDefault] = @IsDefault,
                    [UpdatedAtUtc] = @UpdatedAtUtc
                WHERE [product].[ProductPriceTier].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", tierId),
                new SqlParameter("@IsDefault", isDefault),
                new SqlParameter("@UpdatedAtUtc", DateTime.UtcNow)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task DeactivateAsync(int tierId)
    {
        try
        {
            const string query = @"
                UPDATE [product].[ProductPriceTier]
                SET
                    [IsActive] = 0,
                    [UpdatedAtUtc] = @UpdatedAtUtc
                WHERE [product].[ProductPriceTier].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", tierId),
                new SqlParameter("@UpdatedAtUtc", DateTime.UtcNow)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task ReactivateAsync(int tierId)
    {
        try
        {
            const string query = @"
                UPDATE [product].[ProductPriceTier]
                SET
                    [IsActive] = 1,
                    [UpdatedAtUtc] = @UpdatedAtUtc
                WHERE [product].[ProductPriceTier].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", tierId),
                new SqlParameter("@UpdatedAtUtc", DateTime.UtcNow)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task<List<ProductPriceTier>> GetByProductIdAsync(int productId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [ProductId], [TierName], [SellingPrice], [CostPrice],
                       [IsDefault], [IsActive], [CreatedAtUtc], [UpdatedAtUtc]
                FROM [product].[ProductPriceTier]
                WHERE [product].[ProductPriceTier].[ProductId] = @ProductId";

            return await ExecuteStoredProcedure(query,
                new SqlParameter("@ProductId", productId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task<List<ProductPriceTier>> GetActiveByProductIdAsync(int productId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [ProductId], [TierName], [SellingPrice], [CostPrice],
                       [IsDefault], [IsActive], [CreatedAtUtc], [UpdatedAtUtc]
                FROM [product].[ProductPriceTier]
                WHERE [product].[ProductPriceTier].[ProductId] = @ProductId
                  AND [product].[ProductPriceTier].[IsActive] = 1";

            return await ExecuteStoredProcedure(query,
                new SqlParameter("@ProductId", productId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task<ProductPriceTier?> GetByIdAsync(int tierId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [ProductId], [TierName], [SellingPrice], [CostPrice],
                       [IsDefault], [IsActive], [CreatedAtUtc], [UpdatedAtUtc]
                FROM [product].[ProductPriceTier]
                WHERE [product].[ProductPriceTier].[Id] = @Id";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Id", tierId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns a map of ProductId -> active tier count for the given product ids (single query, no N+1).
    /// Products with no active tiers are omitted from the result.
    /// </summary>
    public virtual async Task<Dictionary<int, int>> GetActiveCountsByProductIdsAsync(IEnumerable<int> productIds)
    {
        var result = new Dictionary<int, int>();
        var ids = productIds?.Distinct().ToList() ?? new List<int>();
        if (ids.Count == 0)
            return result;

        try
        {
            // Build a parameterised IN clause
            var paramNames = ids.Select((_, i) => "@p" + i).ToList();
            var query = $@"
                SELECT [ProductId], COUNT(*) AS [Cnt]
                FROM [product].[ProductPriceTier]
                WHERE [product].[ProductPriceTier].[IsActive] = 1
                  AND [product].[ProductPriceTier].[ProductId] IN ({string.Join(", ", paramNames)})
                GROUP BY [ProductId]";

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

                for (int i = 0; i < ids.Count; i++)
                    command.Parameters.Add(new SqlParameter("@p" + i, ids[i]));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var productId = Convert.ToInt32(reader["ProductId"]);
                    var count = Convert.ToInt32(reader["Cnt"]);
                    result[productId] = count;
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return result;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task<int> GetActiveCountAsync(int productId)
    {
        try
        {
            const string query = @"
                SELECT COUNT(*)
                FROM [product].[ProductPriceTier]
                WHERE [product].[ProductPriceTier].[ProductId] = @ProductId
                  AND [product].[ProductPriceTier].[IsActive] = 1";

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

                command.Parameters.Add(new SqlParameter("@ProductId", productId));

                var result = await command.ExecuteScalarAsync();
                return result != null ? Convert.ToInt32(result) : 0;
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
