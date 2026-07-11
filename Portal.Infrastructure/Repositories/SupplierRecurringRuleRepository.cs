using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for SupplierRecurringRule entity CRUD operations against the [billing].[SupplierRecurringRule] table.
/// </summary>
public class SupplierRecurringRuleRepository : GenericStoredProcedureRepository<SupplierRecurringRule>
{
    public SupplierRecurringRuleRepository(DbContext context) : base(context) { }

    public async Task<List<SupplierRecurringRule>> GetActiveByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [SupplierId], [ExpenseCategoryId], [FrequencyMonths],
                       [ExpectedAmount], [AmountTolerancePercent], [GracePeriodDays],
                       [Description], [IsActive], [IsDeleted], [CreatedAtUtc]
                FROM [billing].[SupplierRecurringRule]
                WHERE SupplierRecurringRule.IsActive = 1
                  AND SupplierRecurringRule.IsDeleted = 0
                  AND SupplierRecurringRule.BusinessId = @BusinessId";

            return await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<SupplierRecurringRule>> GetAllByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [SupplierId], [ExpenseCategoryId], [FrequencyMonths],
                       [ExpectedAmount], [AmountTolerancePercent], [GracePeriodDays],
                       [Description], [IsActive], [IsDeleted], [CreatedAtUtc]
                FROM [billing].[SupplierRecurringRule]
                WHERE SupplierRecurringRule.IsDeleted = 0
                  AND SupplierRecurringRule.BusinessId = @BusinessId";

            return await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<SupplierRecurringRule?> GetByIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [SupplierId], [ExpenseCategoryId], [FrequencyMonths],
                       [ExpectedAmount], [AmountTolerancePercent], [GracePeriodDays],
                       [Description], [IsActive], [IsDeleted], [CreatedAtUtc]
                FROM [billing].[SupplierRecurringRule]
                WHERE SupplierRecurringRule.Id = @Id
                  AND SupplierRecurringRule.BusinessId = @BusinessId
                  AND SupplierRecurringRule.IsDeleted = 0";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task InsertAsync(SupplierRecurringRule entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [billing].[SupplierRecurringRule]
                    ([BusinessId], [SupplierId], [ExpenseCategoryId], [FrequencyMonths],
                     [ExpectedAmount], [AmountTolerancePercent], [GracePeriodDays],
                     [Description], [IsActive], [IsDeleted])
                VALUES
                    (@BusinessId, @SupplierId, @ExpenseCategoryId, @FrequencyMonths,
                     @ExpectedAmount, @AmountTolerancePercent, @GracePeriodDays,
                     @Description, @IsActive, @IsDeleted)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@SupplierId", entity.SupplierId),
                new SqlParameter("@ExpenseCategoryId", entity.ExpenseCategoryId ?? (object)DBNull.Value),
                new SqlParameter("@FrequencyMonths", entity.FrequencyMonths),
                new SqlParameter("@ExpectedAmount", entity.ExpectedAmount ?? (object)DBNull.Value),
                new SqlParameter("@AmountTolerancePercent", entity.AmountTolerancePercent ?? (object)DBNull.Value),
                new SqlParameter("@GracePeriodDays", entity.GracePeriodDays),
                new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value),
                new SqlParameter("@IsActive", entity.IsActive),
                new SqlParameter("@IsDeleted", entity.IsDeleted)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task UpdateAsync(SupplierRecurringRule entity)
    {
        try
        {
            const string query = @"
                UPDATE [billing].[SupplierRecurringRule]
                SET
                    [SupplierId] = @SupplierId,
                    [ExpenseCategoryId] = @ExpenseCategoryId,
                    [FrequencyMonths] = @FrequencyMonths,
                    [ExpectedAmount] = @ExpectedAmount,
                    [AmountTolerancePercent] = @AmountTolerancePercent,
                    [GracePeriodDays] = @GracePeriodDays,
                    [Description] = @Description
                WHERE SupplierRecurringRule.Id = @Id AND SupplierRecurringRule.BusinessId = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@SupplierId", entity.SupplierId),
                new SqlParameter("@ExpenseCategoryId", entity.ExpenseCategoryId ?? (object)DBNull.Value),
                new SqlParameter("@FrequencyMonths", entity.FrequencyMonths),
                new SqlParameter("@ExpectedAmount", entity.ExpectedAmount ?? (object)DBNull.Value),
                new SqlParameter("@AmountTolerancePercent", entity.AmountTolerancePercent ?? (object)DBNull.Value),
                new SqlParameter("@GracePeriodDays", entity.GracePeriodDays),
                new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task SoftDeleteAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [billing].[SupplierRecurringRule]
                SET [IsDeleted] = 1
                WHERE SupplierRecurringRule.Id = @Id AND SupplierRecurringRule.BusinessId = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task ToggleIsActiveAsync(int id, int businessId, bool isActive)
    {
        try
        {
            const string query = @"
                UPDATE [billing].[SupplierRecurringRule]
                SET [IsActive] = @IsActive
                WHERE SupplierRecurringRule.Id = @Id AND SupplierRecurringRule.BusinessId = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@IsActive", isActive));
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
