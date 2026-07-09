using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for PaymentScheduleHistory entity operations against the [revenue].[PaymentScheduleHistory] table.
/// </summary>
public class PaymentScheduleHistoryRepository : GenericStoredProcedureRepository<PaymentScheduleHistory>
{
    public PaymentScheduleHistoryRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Inserts a new history entry recording a modification to a payment schedule.
    /// </summary>
    public virtual async Task InsertAsync(PaymentScheduleHistory entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [revenue].[PaymentScheduleHistory]
                    ([PaymentScheduleId], [FieldChanged], [OldValue], [NewValue], [ChangedByUserId], [ChangedAtUtc])
                VALUES
                    (@PaymentScheduleId, @FieldChanged, @OldValue, @NewValue, @ChangedByUserId, @ChangedAtUtc)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@PaymentScheduleId", entity.PaymentScheduleId),
                new SqlParameter("@FieldChanged", entity.FieldChanged),
                new SqlParameter("@OldValue", entity.OldValue ?? (object)DBNull.Value),
                new SqlParameter("@NewValue", entity.NewValue ?? (object)DBNull.Value),
                new SqlParameter("@ChangedByUserId", entity.ChangedByUserId),
                new SqlParameter("@ChangedAtUtc", entity.ChangedAtUtc)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets all history entries for a payment schedule, ordered by most recent first.
    /// </summary>
    public virtual async Task<List<PaymentScheduleHistory>> GetByScheduleIdAsync(int scheduleId)
    {
        try
        {
            const string query = @"
                SELECT [revenue].[PaymentScheduleHistory].[Id],
                       [revenue].[PaymentScheduleHistory].[PaymentScheduleId],
                       [revenue].[PaymentScheduleHistory].[FieldChanged],
                       [revenue].[PaymentScheduleHistory].[OldValue],
                       [revenue].[PaymentScheduleHistory].[NewValue],
                       [revenue].[PaymentScheduleHistory].[ChangedByUserId],
                       [revenue].[PaymentScheduleHistory].[ChangedAtUtc]
                FROM [revenue].[PaymentScheduleHistory]
                WHERE [revenue].[PaymentScheduleHistory].[PaymentScheduleId] = @ScheduleId
                ORDER BY [revenue].[PaymentScheduleHistory].[ChangedAtUtc] DESC";

            return await ExecuteStoredProcedure(query,
                new SqlParameter("@ScheduleId", scheduleId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
