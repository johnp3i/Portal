using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for ExpenseCategory entity CRUD operations against the [purchase].[ExpenseCategory] table.
/// </summary>
public class ExpenseCategoryRepository : GenericStoredProcedureRepository<ExpenseCategory>
{
    public ExpenseCategoryRepository(DbContext context) : base(context) { }

    public async Task<List<ExpenseCategory>> GetAllByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [Name], [IsActive], [ExpenseTypeId], [CreatedAtUtc]
                FROM [purchase].[ExpenseCategory]
                WHERE ExpenseCategory.BusinessId = @BusinessId";

            return await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task<ExpenseCategory?> GetByIdAndBusinessIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [Name], [IsActive], [ExpenseTypeId], [CreatedAtUtc]
                FROM [purchase].[ExpenseCategory]
                WHERE ExpenseCategory.Id = @Id AND ExpenseCategory.BusinessId = @BusinessId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task<int> InsertAsync(ExpenseCategory entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [purchase].[ExpenseCategory]
                    ([BusinessId], [Name], [IsActive], [ExpenseTypeId])
                VALUES
                    (@BusinessId, @Name, @IsActive, @ExpenseTypeId);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var idParam = new SqlParameter("@BusinessId", entity.BusinessId);
            var nameParam = new SqlParameter("@Name", entity.Name ?? (object)DBNull.Value);
            var isActiveParam = new SqlParameter("@IsActive", entity.IsActive);
            var expenseTypeIdParam = new SqlParameter("@ExpenseTypeId", entity.ExpenseTypeId ?? (object)DBNull.Value);

            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = query;
            command.Parameters.Add(idParam);
            command.Parameters.Add(nameParam);
            command.Parameters.Add(isActiveParam);
            command.Parameters.Add(expenseTypeIdParam);

            if (command.Connection!.State != System.Data.ConnectionState.Open)
                await command.Connection.OpenAsync();

            var result = await command.ExecuteScalarAsync();
            var insertedId = result != null ? Convert.ToInt32(result) : 0;
            entity.Id = insertedId;
            return insertedId;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task UpdateAsync(ExpenseCategory entity)
    {
        try
        {
            const string query = @"
                UPDATE [purchase].[ExpenseCategory]
                SET
                    [Name] = @Name,
                    [ExpenseTypeId] = @ExpenseTypeId
                WHERE ExpenseCategory.Id = @Id AND ExpenseCategory.BusinessId = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@Name", entity.Name ?? (object)DBNull.Value),
                new SqlParameter("@ExpenseTypeId", entity.ExpenseTypeId ?? (object)DBNull.Value)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task DeactivateAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [purchase].[ExpenseCategory]
                SET
                    [IsActive] = 0
                WHERE ExpenseCategory.Id = @Id AND ExpenseCategory.BusinessId = @BusinessId";

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
}
