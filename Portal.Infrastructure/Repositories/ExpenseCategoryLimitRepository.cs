using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for ExpenseCategoryLimit entity CRUD operations against the [purchase].[ExpenseCategoryLimit] table.
/// </summary>
public class ExpenseCategoryLimitRepository : GenericStoredProcedureRepository<ExpenseCategoryLimit>
{
    public ExpenseCategoryLimitRepository(DbContext context) : base(context) { }

    public async Task<ExpenseCategoryLimit?> GetByBusinessAndCategoryAsync(int businessId, int expenseCategoryId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [ExpenseCategoryId], [AnnualLimitEur], [PeriodLimitEur], [CreatedAtUtc]
                FROM [purchase].[ExpenseCategoryLimit]
                WHERE ExpenseCategoryLimit.BusinessId = @BusinessId
                  AND ExpenseCategoryLimit.ExpenseCategoryId = @ExpenseCategoryId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@ExpenseCategoryId", expenseCategoryId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<List<ExpenseCategoryLimit>> GetAllByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [ExpenseCategoryId], [AnnualLimitEur], [PeriodLimitEur], [CreatedAtUtc]
                FROM [purchase].[ExpenseCategoryLimit]
                WHERE ExpenseCategoryLimit.BusinessId = @BusinessId";

            return await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task InsertAsync(ExpenseCategoryLimit entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [purchase].[ExpenseCategoryLimit]
                    ([BusinessId], [ExpenseCategoryId], [AnnualLimitEur], [PeriodLimitEur])
                VALUES
                    (@BusinessId, @ExpenseCategoryId, @AnnualLimitEur, @PeriodLimitEur)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@ExpenseCategoryId", entity.ExpenseCategoryId),
                new SqlParameter("@AnnualLimitEur", entity.AnnualLimitEur ?? (object)DBNull.Value),
                new SqlParameter("@PeriodLimitEur", entity.PeriodLimitEur ?? (object)DBNull.Value)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task UpdateAsync(ExpenseCategoryLimit entity)
    {
        try
        {
            const string query = @"
                UPDATE [purchase].[ExpenseCategoryLimit]
                SET
                    [AnnualLimitEur] = @AnnualLimitEur,
                    [PeriodLimitEur] = @PeriodLimitEur
                WHERE ExpenseCategoryLimit.Id = @Id
                  AND ExpenseCategoryLimit.BusinessId = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@AnnualLimitEur", entity.AnnualLimitEur ?? (object)DBNull.Value),
                new SqlParameter("@PeriodLimitEur", entity.PeriodLimitEur ?? (object)DBNull.Value)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task ClearLimitFieldAsync(int businessId, int expenseCategoryId, string fieldName)
    {
        try
        {
            // Validate fieldName against allowed values to prevent SQL injection
            var allowedFields = new[] { "AnnualLimitEur", "PeriodLimitEur" };
            if (!allowedFields.Contains(fieldName))
            {
                throw new ArgumentException($"Invalid field name: {fieldName}. Allowed values are: AnnualLimitEur, PeriodLimitEur.");
            }

            var query = $@"
                UPDATE [purchase].[ExpenseCategoryLimit]
                SET [{fieldName}] = NULL
                WHERE ExpenseCategoryLimit.BusinessId = @BusinessId
                  AND ExpenseCategoryLimit.ExpenseCategoryId = @ExpenseCategoryId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@ExpenseCategoryId", expenseCategoryId)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }
}
