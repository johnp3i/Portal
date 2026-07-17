using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for Signature entity CRUD operations against the [portal].[Signature] table.
/// </summary>
public class SignatureRepository : GenericStoredProcedureRepository<Signature>
{
    public SignatureRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Inserts a new signature record and returns the new Id.
    /// </summary>
    public virtual async Task<int> InsertAsync(Signature entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [portal].[Signature]
                    ([BusinessId], [Label], [Position], [FileName], [ContentType], [FilePath],
                     [IsDefault], [IsActive], [UploadedByUserId], [CreatedAtUtc])
                OUTPUT INSERTED.Id
                VALUES
                    (@BusinessId, @Label, @Position, @FileName, @ContentType, @FilePath,
                     @IsDefault, @IsActive, @UploadedByUserId, @CreatedAtUtc)";

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
                command.Parameters.Add(new SqlParameter("@Label", entity.Label));
                command.Parameters.Add(new SqlParameter("@Position", entity.Position ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@FileName", entity.FileName));
                command.Parameters.Add(new SqlParameter("@ContentType", entity.ContentType));
                command.Parameters.Add(new SqlParameter("@FilePath", entity.FilePath));
                command.Parameters.Add(new SqlParameter("@IsDefault", entity.IsDefault));
                command.Parameters.Add(new SqlParameter("@IsActive", entity.IsActive));
                command.Parameters.Add(new SqlParameter("@UploadedByUserId", entity.UploadedByUserId));
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
    /// Gets all active signatures for a business.
    /// </summary>
    public virtual async Task<List<Signature>> GetByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [portal].[Signature].[Id],
                       [portal].[Signature].[BusinessId],
                       [portal].[Signature].[Label],
                       [portal].[Signature].[Position],
                       [portal].[Signature].[FileName],
                       [portal].[Signature].[ContentType],
                       [portal].[Signature].[FilePath],
                       [portal].[Signature].[IsDefault],
                       [portal].[Signature].[IsActive],
                       [portal].[Signature].[UploadedByUserId],
                       [portal].[Signature].[CreatedAtUtc]
                FROM [portal].[Signature]
                WHERE [portal].[Signature].[BusinessId] = @BusinessId
                  AND [portal].[Signature].[IsActive] = 1
                ORDER BY [portal].[Signature].[IsDefault] DESC, [portal].[Signature].[Label] ASC";

            return await ExecuteStoredProcedureUnfiltered(query,
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets all signatures for a business (including inactive).
    /// </summary>
    public virtual async Task<List<Signature>> GetAllByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [portal].[Signature].[Id],
                       [portal].[Signature].[BusinessId],
                       [portal].[Signature].[Label],
                       [portal].[Signature].[Position],
                       [portal].[Signature].[FileName],
                       [portal].[Signature].[ContentType],
                       [portal].[Signature].[FilePath],
                       [portal].[Signature].[IsDefault],
                       [portal].[Signature].[IsActive],
                       [portal].[Signature].[UploadedByUserId],
                       [portal].[Signature].[CreatedAtUtc]
                FROM [portal].[Signature]
                WHERE [portal].[Signature].[BusinessId] = @BusinessId
                ORDER BY [portal].[Signature].[IsActive] DESC, [portal].[Signature].[IsDefault] DESC, [portal].[Signature].[Label] ASC";

            return await ExecuteStoredProcedureUnfiltered(query,
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets the default signature for a business (if one is set).
    /// </summary>
    public virtual async Task<Signature?> GetDefaultAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [portal].[Signature].[Id],
                       [portal].[Signature].[BusinessId],
                       [portal].[Signature].[Label],
                       [portal].[Signature].[Position],
                       [portal].[Signature].[FileName],
                       [portal].[Signature].[ContentType],
                       [portal].[Signature].[FilePath],
                       [portal].[Signature].[IsDefault],
                       [portal].[Signature].[IsActive],
                       [portal].[Signature].[UploadedByUserId],
                       [portal].[Signature].[CreatedAtUtc]
                FROM [portal].[Signature]
                WHERE [portal].[Signature].[BusinessId] = @BusinessId
                  AND [portal].[Signature].[IsDefault] = 1
                  AND [portal].[Signature].[IsActive] = 1";

            return await ExecuteSingleRecordStoredProcedureUnfiltered(query,
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets a single signature by Id and BusinessId for tenant isolation.
    /// </summary>
    public virtual async Task<Signature?> GetByIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [portal].[Signature].[Id],
                       [portal].[Signature].[BusinessId],
                       [portal].[Signature].[Label],
                       [portal].[Signature].[Position],
                       [portal].[Signature].[FileName],
                       [portal].[Signature].[ContentType],
                       [portal].[Signature].[FilePath],
                       [portal].[Signature].[IsDefault],
                       [portal].[Signature].[IsActive],
                       [portal].[Signature].[UploadedByUserId],
                       [portal].[Signature].[CreatedAtUtc]
                FROM [portal].[Signature]
                WHERE [portal].[Signature].[Id] = @Id
                  AND [portal].[Signature].[BusinessId] = @BusinessId";

            return await ExecuteSingleRecordStoredProcedureUnfiltered(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Sets a signature as the default (clears previous default first).
    /// </summary>
    public virtual async Task SetDefaultAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [portal].[Signature]
                SET [IsDefault] = 0
                WHERE [portal].[Signature].[BusinessId] = @BusinessId
                  AND [portal].[Signature].[IsDefault] = 1;

                UPDATE [portal].[Signature]
                SET [IsDefault] = 1
                WHERE [portal].[Signature].[Id] = @Id
                  AND [portal].[Signature].[BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Deactivates a signature (soft-delete).
    /// </summary>
    public virtual async Task DeactivateAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [portal].[Signature]
                SET [IsActive] = 0, [IsDefault] = 0
                WHERE [portal].[Signature].[Id] = @Id
                  AND [portal].[Signature].[BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Reactivates a previously deactivated signature.
    /// </summary>
    public virtual async Task ReactivateAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [portal].[Signature]
                SET [IsActive] = 1
                WHERE [portal].[Signature].[Id] = @Id
                  AND [portal].[Signature].[BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates the label of a signature.
    /// </summary>
    public virtual async Task UpdateLabelAsync(int id, int businessId, string label, string? position = null)
    {
        try
        {
            const string query = @"
                UPDATE [portal].[Signature]
                SET [Label] = @Label, [Position] = @Position
                WHERE [portal].[Signature].[Id] = @Id
                  AND [portal].[Signature].[BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@Label", label),
                new SqlParameter("@Position", position ?? (object)DBNull.Value));
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
