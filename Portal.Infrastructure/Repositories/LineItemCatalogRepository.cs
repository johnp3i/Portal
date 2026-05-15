using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for LineItemCatalog entity CRUD operations against the [quotation].[LineItemCatalog] table.
/// Provides search, upsert, and management operations for the per-business line item catalog.
/// </summary>
public class LineItemCatalogRepository : GenericStoredProcedureRepository<LineItemCatalog>
{
    public LineItemCatalogRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Searches catalog entries by description using LIKE-based matching, ordered by most recently updated first.
    /// </summary>
    public async Task<List<LineItemCatalog>> SearchByDescriptionAsync(int businessId, string query)
    {
        try
        {
            const string sql = @"
                SELECT [Id], [BusinessId], [Description], [UnitPrice], [VatRate], [ReferenceUrl], [Discount], [DiscountType], [UpdatedAtUtc]
                FROM [quotation].[LineItemCatalog]
                WHERE [quotation].[LineItemCatalog].[BusinessId] = @BusinessId
                  AND [quotation].[LineItemCatalog].[Description] LIKE @Query
                ORDER BY [quotation].[LineItemCatalog].[UpdatedAtUtc] DESC";

            return await ExecuteStoredProcedure(sql,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@Query", $"%{query}%")
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Inserts a new catalog entry or updates an existing one matched by BusinessId + Description.
    /// Uses a conditional INSERT/UPDATE pattern to handle the upsert logic.
    /// </summary>
    public async Task UpsertAsync(LineItemCatalog entity)
    {
        try
        {
            const string sql = @"
                IF EXISTS (
                    SELECT 1
                    FROM [quotation].[LineItemCatalog]
                    WHERE [quotation].[LineItemCatalog].[BusinessId] = @BusinessId
                      AND [quotation].[LineItemCatalog].[Description] = @Description
                )
                BEGIN
                    UPDATE [quotation].[LineItemCatalog]
                    SET
                        [UnitPrice] = @UnitPrice,
                        [VatRate] = @VatRate,
                        [ReferenceUrl] = @ReferenceUrl,
                        [Discount] = @Discount,
                        [DiscountType] = @DiscountType,
                        [UpdatedAtUtc] = @UpdatedAtUtc
                    WHERE [quotation].[LineItemCatalog].[BusinessId] = @BusinessId
                      AND [quotation].[LineItemCatalog].[Description] = @Description
                END
                ELSE
                BEGIN
                    INSERT INTO [quotation].[LineItemCatalog]
                        ([BusinessId], [Description], [UnitPrice], [VatRate], [ReferenceUrl], [Discount], [DiscountType], [UpdatedAtUtc])
                    VALUES
                        (@BusinessId, @Description, @UnitPrice, @VatRate, @ReferenceUrl, @Discount, @DiscountType, @UpdatedAtUtc)
                END";

            await _context.Database.ExecuteSqlRawAsync(sql,
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@Description", entity.Description),
                new SqlParameter("@UnitPrice", entity.UnitPrice),
                new SqlParameter("@VatRate", entity.VatRate),
                new SqlParameter("@ReferenceUrl", entity.ReferenceUrl ?? (object)DBNull.Value),
                new SqlParameter("@Discount", entity.Discount),
                new SqlParameter("@DiscountType", entity.DiscountType),
                new SqlParameter("@UpdatedAtUtc", entity.UpdatedAtUtc)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns all catalog entries for a given business, ordered by most recently updated first.
    /// </summary>
    public async Task<List<LineItemCatalog>> GetAllByBusinessIdAsync(int businessId)
    {
        try
        {
            const string sql = @"
                SELECT [Id], [BusinessId], [Description], [UnitPrice], [VatRate], [ReferenceUrl], [Discount], [DiscountType], [UpdatedAtUtc]
                FROM [quotation].[LineItemCatalog]
                WHERE [quotation].[LineItemCatalog].[BusinessId] = @BusinessId
                ORDER BY [quotation].[LineItemCatalog].[UpdatedAtUtc] DESC";

            return await ExecuteStoredProcedure(sql, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns a single catalog entry by its ID.
    /// </summary>
    public async Task<LineItemCatalog?> GetByIdAsync(int id)
    {
        try
        {
            const string sql = @"
                SELECT [Id], [BusinessId], [Description], [UnitPrice], [VatRate], [ReferenceUrl], [Discount], [DiscountType], [UpdatedAtUtc]
                FROM [quotation].[LineItemCatalog]
                WHERE [quotation].[LineItemCatalog].[Id] = @Id";

            return await ExecuteSingleRecordStoredProcedure(sql, new SqlParameter("@Id", id));
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Deletes a catalog entry by its ID.
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        try
        {
            const string sql = @"
                DELETE FROM [quotation].[LineItemCatalog]
                WHERE [quotation].[LineItemCatalog].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(sql, new SqlParameter("@Id", id));
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates an existing catalog entry's fields.
    /// </summary>
    public async Task UpdateAsync(LineItemCatalog entity)
    {
        try
        {
            const string sql = @"
                UPDATE [quotation].[LineItemCatalog]
                SET
                    [Description] = @Description,
                    [UnitPrice] = @UnitPrice,
                    [VatRate] = @VatRate,
                    [ReferenceUrl] = @ReferenceUrl,
                    [Discount] = @Discount,
                    [DiscountType] = @DiscountType,
                    [UpdatedAtUtc] = @UpdatedAtUtc
                WHERE [quotation].[LineItemCatalog].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(sql,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@Description", entity.Description),
                new SqlParameter("@UnitPrice", entity.UnitPrice),
                new SqlParameter("@VatRate", entity.VatRate),
                new SqlParameter("@ReferenceUrl", entity.ReferenceUrl ?? (object)DBNull.Value),
                new SqlParameter("@Discount", entity.Discount),
                new SqlParameter("@DiscountType", entity.DiscountType),
                new SqlParameter("@UpdatedAtUtc", entity.UpdatedAtUtc)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }
}
