using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities.Import;

namespace Portal.Infrastructure.Repositories.Import;

/// <summary>
/// Repository for SupplierImportProfile CRUD — one profile per supplier per business.
/// </summary>
public class SupplierImportProfileRepository : GenericStoredProcedureRepository<SupplierImportProfile>
{
    public SupplierImportProfileRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Gets the import profile for a specific supplier within a business.
    /// </summary>
    public async Task<SupplierImportProfile?> GetBySupplierAsync(int supplierId, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [SupplierId], [DefaultExpenseCategoryId],
                       [DefaultPurchaseOriginTypeId], [DefaultCountry], [CreatedAtUtc], [UpdatedAtUtc]
                FROM [import].[SupplierImportProfile]
                WHERE SupplierImportProfile.SupplierId = @SupplierId
                  AND SupplierImportProfile.BusinessId = @BusinessId";

            return await ExecuteSingleRecordStoredProcedureUnfiltered(query,
                new SqlParameter("@SupplierId", supplierId),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Inserts or updates a supplier import profile (upsert pattern).
    /// </summary>
    public async Task UpsertAsync(SupplierImportProfile entity)
    {
        try
        {
            const string query = @"
                IF EXISTS (
                    SELECT 1 FROM [import].[SupplierImportProfile]
                    WHERE SupplierImportProfile.BusinessId = @BusinessId
                      AND SupplierImportProfile.SupplierId = @SupplierId
                )
                BEGIN
                    UPDATE [import].[SupplierImportProfile]
                    SET [DefaultExpenseCategoryId] = @DefaultExpenseCategoryId,
                        [DefaultPurchaseOriginTypeId] = @DefaultPurchaseOriginTypeId,
                        [DefaultCountry] = @DefaultCountry,
                        [UpdatedAtUtc] = GETUTCDATE()
                    WHERE SupplierImportProfile.BusinessId = @BusinessId
                      AND SupplierImportProfile.SupplierId = @SupplierId
                END
                ELSE
                BEGIN
                    INSERT INTO [import].[SupplierImportProfile]
                        ([BusinessId], [SupplierId], [DefaultExpenseCategoryId],
                         [DefaultPurchaseOriginTypeId], [DefaultCountry])
                    VALUES
                        (@BusinessId, @SupplierId, @DefaultExpenseCategoryId,
                         @DefaultPurchaseOriginTypeId, @DefaultCountry)
                END";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@SupplierId", entity.SupplierId),
                new SqlParameter("@DefaultExpenseCategoryId", entity.DefaultExpenseCategoryId ?? (object)DBNull.Value),
                new SqlParameter("@DefaultPurchaseOriginTypeId", entity.DefaultPurchaseOriginTypeId ?? (object)DBNull.Value),
                new SqlParameter("@DefaultCountry", entity.DefaultCountry ?? (object)DBNull.Value));
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
