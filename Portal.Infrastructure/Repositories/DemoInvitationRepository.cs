using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for DemoInvitation entity operations against the [portal].[DemoInvitation] table.
/// </summary>
public class DemoInvitationRepository : GenericStoredProcedureRepository<DemoInvitation>
{
    public DemoInvitationRepository(PortalDbContext context) : base(context) { }

    /// <summary>
    /// Gets a demo invitation by its unique token value.
    /// Returns null if no matching token exists.
    /// </summary>
    public virtual async Task<DemoInvitation?> GetByTokenAsync(string token)
    {
        try
        {
            const string query = @"
                SELECT [portal].[DemoInvitation].[Id],
                       [portal].[DemoInvitation].[BusinessId],
                       [portal].[DemoInvitation].[Token],
                       [portal].[DemoInvitation].[RecipientEmail],
                       [portal].[DemoInvitation].[RecipientName],
                       [portal].[DemoInvitation].[ExpiresAtUtc],
                       [portal].[DemoInvitation].[Status],
                       [portal].[DemoInvitation].[CreatedByUserId],
                       [portal].[DemoInvitation].[FirstAccessedAtUtc],
                       [portal].[DemoInvitation].[LastAccessedAtUtc],
                       [portal].[DemoInvitation].[AccessCount],
                       [portal].[DemoInvitation].[RevokedAtUtc],
                       [portal].[DemoInvitation].[CreatedAtUtc]
                FROM [portal].[DemoInvitation]
                WHERE [portal].[DemoInvitation].[Token] = @Token";

            return await ExecuteSingleRecordStoredProcedureUnfiltered(query,
                new SqlParameter("@Token", token));
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets all demo invitations ordered by CreatedAtUtc descending (newest first).
    /// </summary>
    public virtual async Task<List<DemoInvitation>> GetAllAsync()
    {
        try
        {
            const string query = @"
                SELECT [portal].[DemoInvitation].[Id],
                       [portal].[DemoInvitation].[BusinessId],
                       [portal].[DemoInvitation].[Token],
                       [portal].[DemoInvitation].[RecipientEmail],
                       [portal].[DemoInvitation].[RecipientName],
                       [portal].[DemoInvitation].[ExpiresAtUtc],
                       [portal].[DemoInvitation].[Status],
                       [portal].[DemoInvitation].[CreatedByUserId],
                       [portal].[DemoInvitation].[FirstAccessedAtUtc],
                       [portal].[DemoInvitation].[LastAccessedAtUtc],
                       [portal].[DemoInvitation].[AccessCount],
                       [portal].[DemoInvitation].[RevokedAtUtc],
                       [portal].[DemoInvitation].[CreatedAtUtc]
                FROM [portal].[DemoInvitation]
                ORDER BY [portal].[DemoInvitation].[CreatedAtUtc] DESC";

            return await ExecuteStoredProcedureUnfiltered(query);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets a paginated list of demo invitations ordered by CreatedAtUtc descending.
    /// Uses OFFSET/FETCH for pagination.
    /// </summary>
    public virtual async Task<List<DemoInvitation>> GetPagedAsync(int page, int pageSize)
    {
        try
        {
            int offset = (page - 1) * pageSize;

            const string query = @"
                SELECT [portal].[DemoInvitation].[Id],
                       [portal].[DemoInvitation].[BusinessId],
                       [portal].[DemoInvitation].[Token],
                       [portal].[DemoInvitation].[RecipientEmail],
                       [portal].[DemoInvitation].[RecipientName],
                       [portal].[DemoInvitation].[ExpiresAtUtc],
                       [portal].[DemoInvitation].[Status],
                       [portal].[DemoInvitation].[CreatedByUserId],
                       [portal].[DemoInvitation].[FirstAccessedAtUtc],
                       [portal].[DemoInvitation].[LastAccessedAtUtc],
                       [portal].[DemoInvitation].[AccessCount],
                       [portal].[DemoInvitation].[RevokedAtUtc],
                       [portal].[DemoInvitation].[CreatedAtUtc]
                FROM [portal].[DemoInvitation]
                ORDER BY [portal].[DemoInvitation].[CreatedAtUtc] DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            return await ExecuteStoredProcedureUnfiltered(query,
                new SqlParameter("@Offset", offset),
                new SqlParameter("@PageSize", pageSize));
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets the total count of demo invitations.
    /// </summary>
    public virtual async Task<int> GetTotalCountAsync()
    {
        try
        {
            const string query = @"
                SELECT COUNT(1)
                FROM [portal].[DemoInvitation]";

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

                var result = await command.ExecuteScalarAsync();
                return result != null && result != DBNull.Value ? (int)result : 0;
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Inserts a new demo invitation and its associated permissions within a transaction.
    /// Uses OUTPUT INSERTED.Id to retrieve the generated identity value.
    /// </summary>
    public virtual async Task InsertAsync(DemoInvitation invitation, List<DemoInvitationPermission> permissions)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var transaction = await connection.BeginTransactionAsync();

                try
                {
                    // Insert invitation and get generated Id
                    int invitationId;
                    using (var insertCommand = connection.CreateCommand())
                    {
                        insertCommand.Transaction = transaction;
                        insertCommand.CommandText = @"
                            INSERT INTO [portal].[DemoInvitation]
                                ([BusinessId], [Token], [RecipientEmail], [RecipientName],
                                 [ExpiresAtUtc], [Status], [CreatedByUserId], [AccessCount], [CreatedAtUtc])
                            OUTPUT INSERTED.Id
                            VALUES
                                (@BusinessId, @Token, @RecipientEmail, @RecipientName,
                                 @ExpiresAtUtc, @Status, @CreatedByUserId, @AccessCount, @CreatedAtUtc)";

                        insertCommand.Parameters.Add(new SqlParameter("@BusinessId", invitation.BusinessId));
                        insertCommand.Parameters.Add(new SqlParameter("@Token", invitation.Token));
                        insertCommand.Parameters.Add(new SqlParameter("@RecipientEmail", invitation.RecipientEmail));
                        insertCommand.Parameters.Add(new SqlParameter("@RecipientName", invitation.RecipientName ?? (object)DBNull.Value));
                        insertCommand.Parameters.Add(new SqlParameter("@ExpiresAtUtc", invitation.ExpiresAtUtc));
                        insertCommand.Parameters.Add(new SqlParameter("@Status", invitation.Status));
                        insertCommand.Parameters.Add(new SqlParameter("@CreatedByUserId", invitation.CreatedByUserId));
                        insertCommand.Parameters.Add(new SqlParameter("@AccessCount", invitation.AccessCount));
                        insertCommand.Parameters.Add(new SqlParameter("@CreatedAtUtc", invitation.CreatedAtUtc));

                        var result = await insertCommand.ExecuteScalarAsync();
                        invitationId = (int)result!;
                    }

                    // Insert permissions
                    foreach (var permission in permissions)
                    {
                        using var permCommand = connection.CreateCommand();
                        permCommand.Transaction = transaction;
                        permCommand.CommandText = @"
                            INSERT INTO [portal].[DemoInvitationPermission]
                                ([DemoInvitationId], [Module], [AccessLevel], [CreatedAtUtc])
                            VALUES
                                (@DemoInvitationId, @Module, @AccessLevel, @CreatedAtUtc)";

                        permCommand.Parameters.Add(new SqlParameter("@DemoInvitationId", invitationId));
                        permCommand.Parameters.Add(new SqlParameter("@Module", permission.Module));
                        permCommand.Parameters.Add(new SqlParameter("@AccessLevel", permission.AccessLevel));
                        permCommand.Parameters.Add(new SqlParameter("@CreatedAtUtc", permission.CreatedAtUtc));

                        await permCommand.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();

                    invitation.Id = invitationId;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates the status of a demo invitation. Optionally sets RevokedAtUtc when revoking.
    /// </summary>
    public virtual async Task UpdateStatusAsync(int id, string status, DateTime? revokedAtUtc = null)
    {
        try
        {
            const string query = @"
                UPDATE [portal].[DemoInvitation]
                SET [Status] = @Status,
                    [RevokedAtUtc] = @RevokedAtUtc
                WHERE [portal].[DemoInvitation].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@Status", status),
                new SqlParameter("@RevokedAtUtc", revokedAtUtc ?? (object)DBNull.Value));
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates access tracking fields: increments AccessCount, sets LastAccessedAtUtc,
    /// and conditionally sets FirstAccessedAtUtc and Status='accessed' on first access.
    /// </summary>
    public virtual async Task UpdateAccessTrackingAsync(int id, DateTime accessedAtUtc, bool isFirstAccess)
    {
        try
        {
            string query;

            if (isFirstAccess)
            {
                query = @"
                    UPDATE [portal].[DemoInvitation]
                    SET [AccessCount] = [AccessCount] + 1,
                        [LastAccessedAtUtc] = @AccessedAtUtc,
                        [FirstAccessedAtUtc] = @AccessedAtUtc,
                        [Status] = 'accessed'
                    WHERE [portal].[DemoInvitation].[Id] = @Id";
            }
            else
            {
                query = @"
                    UPDATE [portal].[DemoInvitation]
                    SET [AccessCount] = [AccessCount] + 1,
                        [LastAccessedAtUtc] = @AccessedAtUtc
                    WHERE [portal].[DemoInvitation].[Id] = @Id";
            }

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@AccessedAtUtc", accessedAtUtc));
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets all permissions for a given demo invitation.
    /// </summary>
    public virtual async Task<List<DemoInvitationPermission>> GetPermissionsByInvitationIdAsync(int invitationId)
    {
        try
        {
            const string query = @"
                SELECT [portal].[DemoInvitationPermission].[Id],
                       [portal].[DemoInvitationPermission].[DemoInvitationId],
                       [portal].[DemoInvitationPermission].[Module],
                       [portal].[DemoInvitationPermission].[AccessLevel],
                       [portal].[DemoInvitationPermission].[CreatedAtUtc]
                FROM [portal].[DemoInvitationPermission]
                WHERE [portal].[DemoInvitationPermission].[DemoInvitationId] = @InvitationId";

            return await _context.Set<DemoInvitationPermission>()
                .FromSqlRaw(query, new SqlParameter("@InvitationId", invitationId))
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets all businesses flagged as demo accounts (IsDemoAccount = 1).
    /// </summary>
    public virtual async Task<List<Business>> GetDemoBusinessesAsync()
    {
        try
        {
            const string query = @"
                SELECT [portal].[Business].[Id],
                       [portal].[Business].[Name],
                       [portal].[Business].[IsActive],
                       [portal].[Business].[IsDemoAccount],
                       [portal].[Business].[IsPaymentInstructionsEnabled],
                       [portal].[Business].[IsAutoReceiptEnabled],
                       [portal].[Business].[IsAutoInvoiceSignatureEnabled],
                       [portal].[Business].[CreatedAtUtc],
                       [portal].[Business].[UpdatedAtUtc]
                FROM [portal].[Business]
                WHERE [portal].[Business].[IsDemoAccount] = 1";

            return await _context.Set<Business>()
                .FromSqlRaw(query)
                .IgnoreQueryFilters()
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Deletes all permissions for a given invitation.
    /// </summary>
    public async Task DeletePermissionsByInvitationIdAsync(int invitationId)
    {
        try
        {
            const string query = @"
                DELETE FROM [portal].[DemoInvitationPermission]
                WHERE [portal].[DemoInvitationPermission].[DemoInvitationId] = @InvitationId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@InvitationId", invitationId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Inserts a list of permission entities for a given invitation.
    /// </summary>
    public async Task InsertPermissionsAsync(int invitationId, List<DemoInvitationPermission> permissions)
    {
        try
        {
            foreach (var perm in permissions)
            {
                const string query = @"
                    INSERT INTO [portal].[DemoInvitationPermission]
                        ([DemoInvitationId], [Module], [AccessLevel], [CreatedAtUtc])
                    VALUES
                        (@DemoInvitationId, @Module, @AccessLevel, @CreatedAtUtc)";

                await _context.Database.ExecuteSqlRawAsync(query,
                    new SqlParameter("@DemoInvitationId", invitationId),
                    new SqlParameter("@Module", perm.Module),
                    new SqlParameter("@AccessLevel", perm.AccessLevel),
                    new SqlParameter("@CreatedAtUtc", perm.CreatedAtUtc));
            }
        }
        catch (Exception)
        {
            throw;
        }
    }
}
