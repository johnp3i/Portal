using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for ExternalPlatform CRUD operations against the [revenue].[ExternalPlatform] table.
/// An ExternalPlatform represents an external system (e.g. another billing platform, an online store)
/// that a Business imports sales from, identified by its invoice PlatformCode.
/// </summary>
public class ExternalPlatformRepository : GenericStoredProcedureRepository<ExternalPlatform>
{
    public ExternalPlatformRepository(PortalDbContext context) : base(context) { }

    /// <summary>
    /// Gets external platforms for a business, ordered by Name. Optionally includes inactive ones.
    /// </summary>
    public async Task<List<ExternalPlatform>> GetByBusinessIdAsync(int businessId, bool includeInactive)
    {
        try
        {
            var activeFilter = includeInactive ? "" : "AND ExternalPlatform.IsActive = 1";

            string query = $@"
                SELECT [Id], [BusinessId], [Name], [PlatformCode], [Description], [IsActive], [CreatedAtUtc]
                FROM [revenue].[ExternalPlatform]
                WHERE ExternalPlatform.BusinessId = @BusinessId
                  {activeFilter}
                ORDER BY ExternalPlatform.Name
                OFFSET 0 ROWS";

            return await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets a single external platform by Id and BusinessId (tenant-scoped).
    /// </summary>
    public virtual async Task<ExternalPlatform?> GetByIdAndBusinessIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [Name], [PlatformCode], [Description], [IsActive], [CreatedAtUtc]
                FROM [revenue].[ExternalPlatform]
                WHERE ExternalPlatform.Id = @Id AND ExternalPlatform.BusinessId = @BusinessId";

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
    /// Gets a single external platform by PlatformCode and BusinessId — used for uniqueness checks.
    /// </summary>
    public virtual async Task<ExternalPlatform?> GetByCodeAndBusinessIdAsync(int businessId, string platformCode)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [Name], [PlatformCode], [Description], [IsActive], [CreatedAtUtc]
                FROM [revenue].[ExternalPlatform]
                WHERE ExternalPlatform.BusinessId = @BusinessId AND ExternalPlatform.PlatformCode = @PlatformCode";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@PlatformCode", platformCode));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Inserts a new external platform and returns the new Id.
    /// </summary>
    public virtual async Task<int> InsertAsync(ExternalPlatform entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [revenue].[ExternalPlatform]
                    ([BusinessId], [Name], [PlatformCode], [Description], [IsActive], [CreatedAtUtc])
                OUTPUT INSERTED.Id
                VALUES
                    (@BusinessId, @Name, @PlatformCode, @Description, @IsActive, @CreatedAtUtc)";

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
                command.Parameters.Add(new SqlParameter("@PlatformCode", entity.PlatformCode ?? (object)DBNull.Value));
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
    /// Updates Name, PlatformCode, and Description for an external platform.
    /// </summary>
    public async Task UpdateAsync(ExternalPlatform entity)
    {
        try
        {
            const string query = @"
                UPDATE [revenue].[ExternalPlatform]
                SET
                    [Name] = @Name,
                    [PlatformCode] = @PlatformCode,
                    [Description] = @Description
                WHERE ExternalPlatform.Id = @Id AND ExternalPlatform.BusinessId = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@Name", entity.Name ?? (object)DBNull.Value),
                new SqlParameter("@PlatformCode", entity.PlatformCode ?? (object)DBNull.Value),
                new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Sets the IsActive flag for an external platform.
    /// </summary>
    public async Task SetActiveAsync(int id, int businessId, bool isActive)
    {
        try
        {
            const string query = @"
                UPDATE [revenue].[ExternalPlatform]
                SET [IsActive] = @IsActive
                WHERE ExternalPlatform.Id = @Id AND ExternalPlatform.BusinessId = @BusinessId";

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
}
