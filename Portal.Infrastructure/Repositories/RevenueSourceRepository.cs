using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for RevenueSource entity CRUD operations against the [revenue].[RevenueSource] table.
/// </summary>
public class RevenueSourceRepository : GenericStoredProcedureRepository<RevenueSource>
{
    public RevenueSourceRepository(PortalDbContext context) : base(context) { }

    /// <summary>
    /// Gets all revenue sources for a business, ordered by Name.
    /// </summary>
    public async Task<List<RevenueSource>> GetAllByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [Name], [Description], [IsActive], [CreatedAtUtc]
                FROM [revenue].[RevenueSource]
                WHERE RevenueSource.BusinessId = @BusinessId
                ORDER BY RevenueSource.Name
                OFFSET 0 ROWS";

            return await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets all active revenue sources for a business, ordered by Name.
    /// </summary>
    public async Task<List<RevenueSource>> GetActiveByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [Name], [Description], [IsActive], [CreatedAtUtc]
                FROM [revenue].[RevenueSource]
                WHERE RevenueSource.BusinessId = @BusinessId AND RevenueSource.IsActive = 1
                ORDER BY RevenueSource.Name
                OFFSET 0 ROWS";

            return await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets a single revenue source by Id and BusinessId.
    /// </summary>
    public virtual async Task<RevenueSource?> GetByIdAndBusinessIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [Name], [Description], [IsActive], [CreatedAtUtc]
                FROM [revenue].[RevenueSource]
                WHERE RevenueSource.Id = @Id AND RevenueSource.BusinessId = @BusinessId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Inserts a new revenue source and returns the new Id via OUTPUT INSERTED.Id.
    /// </summary>
    public virtual async Task<int> InsertAsync(RevenueSource entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [revenue].[RevenueSource]
                    ([BusinessId], [Name], [Description], [IsActive], [CreatedAtUtc])
                OUTPUT INSERTED.Id
                VALUES
                    (@BusinessId, @Name, @Description, @IsActive, @CreatedAtUtc)";

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

                command.Parameters.Add(new SqlParameter("@BusinessId", entity.BusinessId));
                command.Parameters.Add(new SqlParameter("@Name", entity.Name ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@IsActive", entity.IsActive));
                command.Parameters.Add(new SqlParameter("@CreatedAtUtc", entity.CreatedAtUtc));

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

    /// <summary>
    /// Updates Name and Description for a revenue source.
    /// </summary>
    public async Task UpdateAsync(RevenueSource entity)
    {
        try
        {
            const string query = @"
                UPDATE [revenue].[RevenueSource]
                SET
                    [Name] = @Name,
                    [Description] = @Description
                WHERE RevenueSource.Id = @Id AND RevenueSource.BusinessId = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@Name", entity.Name ?? (object)DBNull.Value),
                new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Sets the IsActive flag for a revenue source.
    /// </summary>
    public async Task SetIsActiveAsync(int id, int businessId, bool isActive)
    {
        try
        {
            const string query = @"
                UPDATE [revenue].[RevenueSource]
                SET
                    [IsActive] = @IsActive
                WHERE RevenueSource.Id = @Id AND RevenueSource.BusinessId = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@IsActive", isActive)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Checks whether a revenue source has any associated revenue summaries.
    /// </summary>
    public async Task<bool> HasSummariesAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT COUNT(1)
                FROM [revenue].[RevenueSummary]
                WHERE RevenueSummary.RevenueSourceId = @Id AND RevenueSummary.BusinessId = @BusinessId";

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

                command.Parameters.Add(new SqlParameter("@Id", id));
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));

                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result) > 0;
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
