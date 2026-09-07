using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities.Notification;

namespace Portal.Infrastructure.Repositories.Notification;

/// <summary>
/// Raw-SQL repository for [notification].[AssistantOptOut] — per-service, per-recipient opt-out.
/// </summary>
public class AssistantOptOutRepository : GenericStoredProcedureRepository<AssistantOptOut>
{
    public AssistantOptOutRepository(DbContext context) : base(context) { }

    public async Task<bool> ExistsAsync(int businessId, int assistantTypeId, string recipientEmail)
    {
        try
        {
            const string query = @"
                SELECT COUNT(*)
                FROM [notification].[AssistantOptOut]
                WHERE [notification].[AssistantOptOut].[BusinessId] = @BusinessId
                  AND [notification].[AssistantOptOut].[AssistantTypeId] = @AssistantTypeId
                  AND [notification].[AssistantOptOut].[RecipientEmail] = @RecipientEmail";

            var connection = _context.Database.GetDbConnection();
            var shouldClose = connection.State != System.Data.ConnectionState.Open;
            if (shouldClose) await connection.OpenAsync();
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = query;
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@AssistantTypeId", assistantTypeId));
                command.Parameters.Add(new SqlParameter("@RecipientEmail", recipientEmail));
                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result) > 0;
            }
            finally
            {
                if (shouldClose) await connection.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task InsertAsync(AssistantOptOut entity)
    {
        try
        {
            const string query = @"
                IF NOT EXISTS (
                    SELECT 1 FROM [notification].[AssistantOptOut]
                    WHERE [BusinessId] = @BusinessId AND [AssistantTypeId] = @AssistantTypeId AND [RecipientEmail] = @RecipientEmail
                )
                INSERT INTO [notification].[AssistantOptOut]
                    ([BusinessId], [AssistantTypeId], [CustomerId], [RecipientEmail], [OptedOutAtUtc], [CreatedAtUtc])
                VALUES
                    (@BusinessId, @AssistantTypeId, @CustomerId, @RecipientEmail, GETUTCDATE(), GETUTCDATE())";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@AssistantTypeId", entity.AssistantTypeId),
                new SqlParameter("@CustomerId", (object?)entity.CustomerId ?? DBNull.Value),
                new SqlParameter("@RecipientEmail", entity.RecipientEmail));
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
