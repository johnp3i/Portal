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
                       [portal].[DemoInvitation].[ConvertedAtUtc],
                       [portal].[DemoInvitation].[CreatedAtUtc]
                FROM [portal].[DemoInvitation]
                WHERE [portal].[DemoInvitation].[Token] = @Token";

            return await ExecuteSingleRecordStoredProcedureUnfiltered(query,
                new SqlParameter("@Token", token));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets a demo invitation by its Id.
    /// Returns null if no matching record exists.
    /// </summary>
    public virtual async Task<DemoInvitation?> GetByIdAsync(int id)
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
                       [portal].[DemoInvitation].[ConvertedAtUtc],
                       [portal].[DemoInvitation].[CreatedAtUtc]
                FROM [portal].[DemoInvitation]
                WHERE [portal].[DemoInvitation].[Id] = @Id";

            return await ExecuteSingleRecordStoredProcedureUnfiltered(query,
                new SqlParameter("@Id", id));
        }
        catch (Exception ex)
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
                       [portal].[DemoInvitation].[ConvertedAtUtc],
                       [portal].[DemoInvitation].[CreatedAtUtc]
                FROM [portal].[DemoInvitation]
                ORDER BY [portal].[DemoInvitation].[CreatedAtUtc] DESC";

            return await ExecuteStoredProcedureUnfiltered(query);
        }
        catch (Exception ex)
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
                       [portal].[DemoInvitation].[ConvertedAtUtc],
                       [portal].[DemoInvitation].[CreatedAtUtc]
                FROM [portal].[DemoInvitation]
                ORDER BY [portal].[DemoInvitation].[CreatedAtUtc] DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            return await ExecuteStoredProcedureUnfiltered(query,
                new SqlParameter("@Offset", offset),
                new SqlParameter("@PageSize", pageSize));
        }
        catch (Exception ex)
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
        catch (Exception ex)
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
        catch (Exception ex)
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
        catch (Exception ex)
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
        catch (Exception ex)
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
        catch (Exception ex)
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
                       [portal].[Business].[IsOnboardingDismissed],
                       [portal].[Business].[IsReminderSystemEnabled],
                       [portal].[Business].[CreatedAtUtc],
                       [portal].[Business].[UpdatedAtUtc]
                FROM [portal].[Business]
                WHERE [portal].[Business].[IsDemoAccount] = 1";

            return await _context.Set<Business>()
                .FromSqlRaw(query)
                .IgnoreQueryFilters()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Deletes all permissions for a given invitation and reinserts the new set within a transaction.
    /// Ensures atomicity — if any insert fails, the delete is rolled back.
    /// </summary>
    public async Task ReplacePermissionsAsync(int invitationId, List<DemoInvitationPermission> permissions)
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
                    // Delete existing permissions
                    using (var deleteCommand = connection.CreateCommand())
                    {
                        deleteCommand.Transaction = transaction;
                        deleteCommand.CommandText = @"
                            DELETE FROM [portal].[DemoInvitationPermission]
                            WHERE [portal].[DemoInvitationPermission].[DemoInvitationId] = @InvitationId";
                        deleteCommand.Parameters.Add(new SqlParameter("@InvitationId", invitationId));
                        await deleteCommand.ExecuteNonQueryAsync();
                    }

                    // Insert new permissions
                    foreach (var perm in permissions)
                    {
                        using var insertCommand = connection.CreateCommand();
                        insertCommand.Transaction = transaction;
                        insertCommand.CommandText = @"
                            INSERT INTO [portal].[DemoInvitationPermission]
                                ([DemoInvitationId], [Module], [AccessLevel], [CreatedAtUtc])
                            VALUES
                                (@DemoInvitationId, @Module, @AccessLevel, @CreatedAtUtc)";
                        insertCommand.Parameters.Add(new SqlParameter("@DemoInvitationId", invitationId));
                        insertCommand.Parameters.Add(new SqlParameter("@Module", perm.Module));
                        insertCommand.Parameters.Add(new SqlParameter("@AccessLevel", perm.AccessLevel));
                        insertCommand.Parameters.Add(new SqlParameter("@CreatedAtUtc", perm.CreatedAtUtc));
                        await insertCommand.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();
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
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Marks the most recent demo invitation for the given email as converted.
    /// </summary>
    public async Task MarkConvertedByEmailAsync(string recipientEmail)
    {
        try
        {
            const string query = @"
                UPDATE [portal].[DemoInvitation]
                SET [ConvertedAtUtc] = GETUTCDATE()
                WHERE [Id] = (
                    SELECT TOP 1 [Id]
                    FROM [portal].[DemoInvitation]
                    WHERE [RecipientEmail] = @RecipientEmail
                      AND [ConvertedAtUtc] IS NULL
                    ORDER BY [CreatedAtUtc] DESC
                )";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@RecipientEmail", recipientEmail));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Checks whether the given email address belongs to an existing customer in any business.
    /// Used to prevent sending demo invitations to current customers.
    /// </summary>
    public async Task<bool> IsCustomerEmailAsync(string email)
    {
        try
        {
            const string query = @"
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM [customer].[Customer]
                    WHERE [customer].[Customer].[Email] = @Email
                ) THEN 1 ELSE 0 END";

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

                command.Parameters.Add(new SqlParameter("@Email", email));

                var result = await command.ExecuteScalarAsync();
                return result != null && (int)result == 1;
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
    /// Searches Sales contacts across all businesses by name, email, or company.
    /// Returns only contacts that have an email address (max 20 results).
    /// </summary>
    public async Task<List<Portal.Infrastructure.Models.SalesContactBriefItem>> SearchSalesContactsAsync(string? search)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                var searchParam = string.IsNullOrWhiteSpace(search) ? (object)DBNull.Value : search;
                var patternParam = string.IsNullOrWhiteSpace(search) ? (object)DBNull.Value : $"%{search}%";

                const string query = @"
                    SELECT TOP 20
                           [sales].[Contact].[Id],
                           [sales].[Contact].[FirstName],
                           [sales].[Contact].[LastName],
                           [sales].[Contact].[Email],
                           [sales].[Contact].[CompanyName]
                    FROM [sales].[Contact]
                    WHERE [sales].[Contact].[Email] IS NOT NULL
                      AND [sales].[Contact].[Email] <> ''
                      AND [sales].[Contact].[IsActive] = 1
                      AND (@Search IS NULL
                           OR [sales].[Contact].[FirstName] LIKE @Pattern
                           OR [sales].[Contact].[LastName] LIKE @Pattern
                           OR [sales].[Contact].[Email] LIKE @Pattern
                           OR [sales].[Contact].[CompanyName] LIKE @Pattern)
                    ORDER BY [sales].[Contact].[FirstName], [sales].[Contact].[LastName]";

                using var command = connection.CreateCommand();
                command.CommandText = query;

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@Search", searchParam));
                command.Parameters.Add(new SqlParameter("@Pattern", patternParam));

                var results = new List<Portal.Infrastructure.Models.SalesContactBriefItem>();
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var firstName = reader.GetString(1);
                    var lastName = reader.IsDBNull(2) ? null : reader.GetString(2);
                    results.Add(new Portal.Infrastructure.Models.SalesContactBriefItem
                    {
                        Id = reader.GetInt32(0),
                        FullName = string.IsNullOrWhiteSpace(lastName) ? firstName : $"{firstName} {lastName}",
                        Email = reader.GetString(3),
                        CompanyName = reader.IsDBNull(4) ? null : reader.GetString(4)
                    });
                }

                return results;
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
