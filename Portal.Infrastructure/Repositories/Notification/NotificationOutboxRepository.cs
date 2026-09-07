using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Entities.Notification;

namespace Portal.Infrastructure.Repositories.Notification;

/// <summary>
/// Raw-SQL repository for [notification].[OutboxMessage]. No tenant query filter — the
/// dispatcher reads across all businesses; all reads/writes are scoped by explicit BusinessId.
/// </summary>
public class NotificationOutboxRepository : GenericStoredProcedureRepository<OutboxMessage>
{
    public NotificationOutboxRepository(DbContext context) : base(context) { }

    public async Task<int> InsertAsync(OutboxMessage entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [notification].[OutboxMessage]
                    ([BusinessId], [AssistantTypeId], [RecipientEmail], [RecipientName], [ReplyToEmail],
                     [Subject], [BodyHtml], [OutboxMessageStatusTypeId], [RetryCount], [MaxRetries], [ScheduledForUtc],
                     [LastError], [SentAtUtc], [FailedAtUtc], [RelatedEntityType], [RelatedEntityId], [CycleKey], [CreatedAtUtc])
                OUTPUT INSERTED.[Id]
                VALUES
                    (@BusinessId, @AssistantTypeId, @RecipientEmail, @RecipientName, @ReplyToEmail,
                     @Subject, @BodyHtml, @OutboxMessageStatusTypeId, @RetryCount, @MaxRetries, @ScheduledForUtc,
                     @LastError, @SentAtUtc, @FailedAtUtc, @RelatedEntityType, @RelatedEntityId, @CycleKey, @CreatedAtUtc)";

            var connection = _context.Database.GetDbConnection();
            var shouldClose = connection.State != System.Data.ConnectionState.Open;
            if (shouldClose) await connection.OpenAsync();

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = query;

                // Honour any ambient transaction the caller opened (atomic with the business event).
                var currentTx = _context.Database.CurrentTransaction;
                if (currentTx != null)
                    command.Transaction = currentTx.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@BusinessId", entity.BusinessId));
                command.Parameters.Add(new SqlParameter("@AssistantTypeId", entity.AssistantTypeId));
                command.Parameters.Add(new SqlParameter("@RecipientEmail", entity.RecipientEmail));
                command.Parameters.Add(new SqlParameter("@RecipientName", entity.RecipientName ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@ReplyToEmail", entity.ReplyToEmail ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Subject", entity.Subject));
                command.Parameters.Add(new SqlParameter("@BodyHtml", entity.BodyHtml));
                command.Parameters.Add(new SqlParameter("@OutboxMessageStatusTypeId", entity.OutboxMessageStatusTypeId));
                command.Parameters.Add(new SqlParameter("@RetryCount", entity.RetryCount));
                command.Parameters.Add(new SqlParameter("@MaxRetries", entity.MaxRetries));
                command.Parameters.Add(new SqlParameter("@ScheduledForUtc", entity.ScheduledForUtc));
                command.Parameters.Add(new SqlParameter("@LastError", entity.LastError ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@SentAtUtc", entity.SentAtUtc ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@FailedAtUtc", entity.FailedAtUtc ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@RelatedEntityType", entity.RelatedEntityType ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@RelatedEntityId", entity.RelatedEntityId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@CycleKey", entity.CycleKey ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@CreatedAtUtc", entity.CreatedAtUtc == default ? DateTime.UtcNow : entity.CreatedAtUtc));

                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result);
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

    /// <summary>Messages due to send now (Pending and ScheduledForUtc &lt;= now), oldest first, bounded.</summary>
    public async Task<List<OutboxMessage>> GetDuePendingAsync(int batchSize, DateTime nowUtc)
    {
        try
        {
            const string query = @"
                SELECT TOP (@BatchSize)
                       [Id], [BusinessId], [AssistantTypeId], [RecipientEmail], [RecipientName], [ReplyToEmail],
                       [Subject], [BodyHtml], [OutboxMessageStatusTypeId], [RetryCount], [MaxRetries], [ScheduledForUtc],
                       [LastError], [SentAtUtc], [FailedAtUtc], [RelatedEntityType], [RelatedEntityId], [CycleKey], [CreatedAtUtc]
                FROM [notification].[OutboxMessage]
                WHERE [notification].[OutboxMessage].[OutboxMessageStatusTypeId] = @PendingStatus
                  AND [notification].[OutboxMessage].[ScheduledForUtc] <= @NowUtc
                ORDER BY [notification].[OutboxMessage].[ScheduledForUtc] ASC";

            return await ExecuteStoredProcedure(query,
                new SqlParameter("@BatchSize", batchSize),
                new SqlParameter("@PendingStatus", OutboxMessageStatusTypes.Pending),
                new SqlParameter("@NowUtc", nowUtc));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task MarkSentAsync(int id)
    {
        try
        {
            const string query = @"
                UPDATE [notification].[OutboxMessage]
                SET [OutboxMessageStatusTypeId] = @SentStatus, [SentAtUtc] = GETUTCDATE()
                WHERE [notification].[OutboxMessage].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@SentStatus", OutboxMessageStatusTypes.Sent));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>Records a failed attempt but leaves the message Pending for a later retry.</summary>
    public async Task MarkRetryAsync(int id, string error)
    {
        try
        {
            const string query = @"
                UPDATE [notification].[OutboxMessage]
                SET [RetryCount] = [RetryCount] + 1,
                    [LastError] = @Error
                WHERE [notification].[OutboxMessage].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@Error", (object?)error ?? DBNull.Value));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>Marks the message permanently failed after exhausting retries.</summary>
    public async Task MarkFailedAsync(int id, string error)
    {
        try
        {
            const string query = @"
                UPDATE [notification].[OutboxMessage]
                SET [OutboxMessageStatusTypeId] = @FailedStatus,
                    [RetryCount] = [RetryCount] + 1,
                    [LastError] = @Error,
                    [FailedAtUtc] = GETUTCDATE()
                WHERE [notification].[OutboxMessage].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@FailedStatus", OutboxMessageStatusTypes.Failed),
                new SqlParameter("@Error", (object?)error ?? DBNull.Value));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns true if a non-failed outbox message already exists for the given assistant and
    /// related entity (e.g. a Thank-You for a specific invoice/payment). Used to guard against
    /// duplicate enqueues from double-submits or retried Stripe webhooks. Failed messages do
    /// NOT block a re-enqueue (a genuine retry after fixing delivery is allowed).
    /// </summary>
    public async Task<bool> ExistsForRelatedEntityAsync(
        int businessId, int assistantTypeId, string relatedEntityType, int relatedEntityId)
    {
        try
        {
            const string query = @"
                SELECT COUNT(*)
                FROM [notification].[OutboxMessage]
                WHERE [notification].[OutboxMessage].[BusinessId] = @BusinessId
                  AND [notification].[OutboxMessage].[AssistantTypeId] = @AssistantTypeId
                  AND [notification].[OutboxMessage].[RelatedEntityType] = @RelatedEntityType
                  AND [notification].[OutboxMessage].[RelatedEntityId] = @RelatedEntityId
                  AND [notification].[OutboxMessage].[OutboxMessageStatusTypeId] <> @FailedStatus";

            var connection = _context.Database.GetDbConnection();
            var shouldClose = connection.State != System.Data.ConnectionState.Open;
            if (shouldClose) await connection.OpenAsync();
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = query;

                var currentTx = _context.Database.CurrentTransaction;
                if (currentTx != null)
                    command.Transaction = currentTx.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@AssistantTypeId", assistantTypeId));
                command.Parameters.Add(new SqlParameter("@RelatedEntityType", relatedEntityType));
                command.Parameters.Add(new SqlParameter("@RelatedEntityId", relatedEntityId));
                command.Parameters.Add(new SqlParameter("@FailedStatus", OutboxMessageStatusTypes.Failed));
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

    /// <summary>
    /// Returns true if a non-failed outbox message already exists for the given assistant and
    /// exact cycle key (e.g. a weekly digest for a specific ISO week). Used by scheduled/
    /// cycle-based producers to guard one-per-cycle. Failed messages do NOT block a re-enqueue
    /// (a genuine retry after fixing delivery is allowed), mirroring ExistsForRelatedEntityAsync.
    /// </summary>
    public async Task<bool> ExistsForCycleAsync(int businessId, int assistantTypeId, string cycleKey)
    {
        try
        {
            const string query = @"
                SELECT COUNT(*)
                FROM [notification].[OutboxMessage]
                WHERE [notification].[OutboxMessage].[BusinessId] = @BusinessId
                  AND [notification].[OutboxMessage].[AssistantTypeId] = @AssistantTypeId
                  AND [notification].[OutboxMessage].[CycleKey] = @CycleKey
                  AND [notification].[OutboxMessage].[OutboxMessageStatusTypeId] <> @FailedStatus";

            var connection = _context.Database.GetDbConnection();
            var shouldClose = connection.State != System.Data.ConnectionState.Open;
            if (shouldClose) await connection.OpenAsync();
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = query;

                var currentTx = _context.Database.CurrentTransaction;
                if (currentTx != null)
                    command.Transaction = currentTx.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@AssistantTypeId", assistantTypeId));
                command.Parameters.Add(new SqlParameter("@CycleKey", cycleKey));
                command.Parameters.Add(new SqlParameter("@FailedStatus", OutboxMessageStatusTypes.Failed));
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

    /// <summary>Count of messages that became Failed since the given time (for admin threshold alerting).</summary>
    public async Task<int> CountFailedSinceAsync(DateTime sinceUtc)
    {
        try
        {
            const string query = @"
                SELECT COUNT(*)
                FROM [notification].[OutboxMessage]
                WHERE [notification].[OutboxMessage].[OutboxMessageStatusTypeId] = @FailedStatus
                  AND [notification].[OutboxMessage].[FailedAtUtc] >= @SinceUtc";

            var connection = _context.Database.GetDbConnection();
            var shouldClose = connection.State != System.Data.ConnectionState.Open;
            if (shouldClose) await connection.OpenAsync();
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = query;
                command.Parameters.Add(new SqlParameter("@FailedStatus", OutboxMessageStatusTypes.Failed));
                command.Parameters.Add(new SqlParameter("@SinceUtc", sinceUtc));
                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result);
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

    /// <summary>Paged activity log for one assistant within a business (newest first).</summary>
    public async Task<List<OutboxMessage>> GetByBusinessAndAssistantPagedAsync(
        int businessId, int assistantTypeId, int page, int pageSize)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [AssistantTypeId], [RecipientEmail], [RecipientName], [ReplyToEmail],
                       [Subject], [BodyHtml], [OutboxMessageStatusTypeId], [RetryCount], [MaxRetries], [ScheduledForUtc],
                       [LastError], [SentAtUtc], [FailedAtUtc], [RelatedEntityType], [RelatedEntityId], [CycleKey], [CreatedAtUtc]
                FROM [notification].[OutboxMessage]
                WHERE [notification].[OutboxMessage].[BusinessId] = @BusinessId
                  AND [notification].[OutboxMessage].[AssistantTypeId] = @AssistantTypeId
                ORDER BY [notification].[OutboxMessage].[CreatedAtUtc] DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            return await ExecuteStoredProcedure(query,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@AssistantTypeId", assistantTypeId),
                new SqlParameter("@Offset", (page - 1) * pageSize),
                new SqlParameter("@PageSize", pageSize));
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
