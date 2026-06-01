using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities.Stripe;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for WebhookEvent entity operations against the [stripe].[WebhookEvent] table.
/// Supports idempotency checks and event logging for Stripe webhook processing.
/// </summary>
public class WebhookEventRepository : GenericStoredProcedureRepository<WebhookEvent>
{
    public WebhookEventRepository(DbContext context) : base(context) { }

    public virtual async Task<bool> ExistsByEventIdAsync(string eventId)
    {
        try
        {
            const string query = @"
                SELECT COUNT(1)
                FROM [stripe].[WebhookEvent]
                WHERE [EventId] = @EventId";

            var connection = _context.Database.GetDbConnection();

            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = query;

            var transaction = _context.Database.CurrentTransaction;
            if (transaction != null)
                command.Transaction = transaction.GetDbTransaction();

            command.Parameters.Add(new SqlParameter("@EventId", eventId));

            var result = await command.ExecuteScalarAsync();
            return result != null && (int)result > 0;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task InsertAsync(WebhookEvent entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [stripe].[WebhookEvent]
                    ([EventId], [Type], [ProcessedAtUtc], [CreatedAtUtc])
                VALUES
                    (@EventId, @Type, @ProcessedAtUtc, @CreatedAtUtc)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@EventId", entity.EventId),
                new SqlParameter("@Type", entity.Type),
                new SqlParameter("@ProcessedAtUtc", entity.ProcessedAtUtc ?? (object)DBNull.Value),
                new SqlParameter("@CreatedAtUtc", entity.CreatedAtUtc)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }
}
