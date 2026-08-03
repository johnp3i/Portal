using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for ExpenseCategoryTemplate CRUD against [purchase].[ExpenseCategoryTemplate].
/// Platform-wide (no BusinessId scope).
/// </summary>
public class ExpenseCategoryTemplateRepository : GenericStoredProcedureRepository<ExpenseCategoryTemplate>
{
    public ExpenseCategoryTemplateRepository(DbContext context) : base(context) { }

    public virtual async Task<List<ExpenseCategoryTemplate>> GetAllActiveAsync()
    {
        try
        {
            const string query = @"
                SELECT [Id], [Name], [Description], [IsActive], [CreatedAtUtc]
                FROM [purchase].[ExpenseCategoryTemplate]
                WHERE [IsActive] = 1
                ORDER BY [Name]";

            return await ExecuteStoredProcedureUnfiltered(query);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task<List<ExpenseCategoryTemplate>> GetAllAsync()
    {
        try
        {
            const string query = @"
                SELECT [Id], [Name], [Description], [IsActive], [CreatedAtUtc]
                FROM [purchase].[ExpenseCategoryTemplate]
                ORDER BY [IsActive] DESC, [Name]";

            return await ExecuteStoredProcedureUnfiltered(query);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task<int> InsertAsync(ExpenseCategoryTemplate entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [purchase].[ExpenseCategoryTemplate]
                    ([Name], [Description], [IsActive], [CreatedAtUtc])
                VALUES
                    (@Name, @Description, 1, GETUTCDATE());
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var connection = _context.Database.GetDbConnection();
            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;
                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null) command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@Name", entity.Name));
                command.Parameters.Add(new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value));

                var result = await command.ExecuteScalarAsync();
                return (int)result!;
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

    public virtual async Task UpdateAsync(int id, string name, string? description)
    {
        try
        {
            const string query = @"
                UPDATE [purchase].[ExpenseCategoryTemplate]
                SET [Name] = @Name, [Description] = @Description
                WHERE [Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@Name", name),
                new SqlParameter("@Description", description ?? (object)DBNull.Value));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task DeactivateAsync(int id)
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync(
                "UPDATE [purchase].[ExpenseCategoryTemplate] SET [IsActive] = 0 WHERE [Id] = @Id",
                new SqlParameter("@Id", id));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task ReactivateAsync(int id)
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync(
                "UPDATE [purchase].[ExpenseCategoryTemplate] SET [IsActive] = 1 WHERE [Id] = @Id",
                new SqlParameter("@Id", id));
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
