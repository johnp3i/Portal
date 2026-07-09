using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for PaymentScheduleInstalment entity CRUD operations against the [revenue].[PaymentScheduleInstalment] table.
/// </summary>
public class PaymentScheduleInstalmentRepository : GenericStoredProcedureRepository<PaymentScheduleInstalment>
{
    public PaymentScheduleInstalmentRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Inserts a new instalment record and returns the new PaymentScheduleInstalment.Id via OUTPUT INSERTED.Id.
    /// </summary>
    public virtual async Task<int> InsertAsync(PaymentScheduleInstalment entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [revenue].[PaymentScheduleInstalment]
                    ([PaymentScheduleId], [SequenceNumber], [Amount], [MatchedAmount],
                     [DueDate], [PaymentId], [ParentInstalmentId], [IsRemainder], [CreatedAtUtc])
                OUTPUT INSERTED.Id
                VALUES
                    (@PaymentScheduleId, @SequenceNumber, @Amount, @MatchedAmount,
                     @DueDate, @PaymentId, @ParentInstalmentId, @IsRemainder, @CreatedAtUtc)";

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

                command.Parameters.Add(new SqlParameter("@PaymentScheduleId", entity.PaymentScheduleId));
                command.Parameters.Add(new SqlParameter("@SequenceNumber", entity.SequenceNumber));
                command.Parameters.Add(new SqlParameter("@Amount", entity.Amount));
                command.Parameters.Add(new SqlParameter("@MatchedAmount", entity.MatchedAmount));
                command.Parameters.Add(new SqlParameter("@DueDate", entity.DueDate.HasValue ? entity.DueDate.Value : (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@PaymentId", entity.PaymentId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@ParentInstalmentId", entity.ParentInstalmentId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@IsRemainder", entity.IsRemainder));
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
    /// Gets all instalments belonging to a payment schedule, ordered by SequenceNumber.
    /// </summary>
    public virtual async Task<List<PaymentScheduleInstalment>> GetByScheduleIdAsync(int scheduleId)
    {
        try
        {
            const string query = @"
                SELECT [revenue].[PaymentScheduleInstalment].[Id],
                       [revenue].[PaymentScheduleInstalment].[PaymentScheduleId],
                       [revenue].[PaymentScheduleInstalment].[SequenceNumber],
                       [revenue].[PaymentScheduleInstalment].[Amount],
                       [revenue].[PaymentScheduleInstalment].[MatchedAmount],
                       [revenue].[PaymentScheduleInstalment].[DueDate],
                       [revenue].[PaymentScheduleInstalment].[PaymentId],
                       [revenue].[PaymentScheduleInstalment].[ParentInstalmentId],
                       [revenue].[PaymentScheduleInstalment].[IsRemainder],
                       [revenue].[PaymentScheduleInstalment].[CreatedAtUtc]
                FROM [revenue].[PaymentScheduleInstalment]
                WHERE [revenue].[PaymentScheduleInstalment].[PaymentScheduleId] = @PaymentScheduleId
                ORDER BY [revenue].[PaymentScheduleInstalment].[SequenceNumber]";

            return await ExecuteStoredProcedure(query,
                new SqlParameter("@PaymentScheduleId", scheduleId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates the matched amount and optional payment reference on an instalment after payment matching.
    /// </summary>
    public virtual async Task UpdateMatchedAmountAsync(int instalmentId, decimal newMatchedAmount, int? paymentId)
    {
        try
        {
            const string query = @"
                UPDATE [revenue].[PaymentScheduleInstalment]
                SET [MatchedAmount] = @MatchedAmount,
                    [PaymentId] = @PaymentId
                WHERE [revenue].[PaymentScheduleInstalment].[Id] = @InstalmentId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@MatchedAmount", newMatchedAmount),
                new SqlParameter("@PaymentId", paymentId ?? (object)DBNull.Value),
                new SqlParameter("@InstalmentId", instalmentId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates the amount of an instalment (for schedule modification).
    /// </summary>
    public virtual async Task UpdateAmountAsync(int instalmentId, decimal newAmount)
    {
        try
        {
            const string query = @"
                UPDATE [revenue].[PaymentScheduleInstalment]
                SET [Amount] = @Amount
                WHERE [revenue].[PaymentScheduleInstalment].[Id] = @InstalmentId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Amount", newAmount),
                new SqlParameter("@InstalmentId", instalmentId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates the due date of an instalment (for schedule modification).
    /// </summary>
    public virtual async Task UpdateDueDateAsync(int instalmentId, DateOnly? newDueDate)
    {
        try
        {
            const string query = @"
                UPDATE [revenue].[PaymentScheduleInstalment]
                SET [DueDate] = @DueDate
                WHERE [revenue].[PaymentScheduleInstalment].[Id] = @InstalmentId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@DueDate", newDueDate.HasValue ? newDueDate.Value : (object)DBNull.Value),
                new SqlParameter("@InstalmentId", instalmentId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Deletes a single instalment by its Id.
    /// </summary>
    public virtual async Task DeleteAsync(int instalmentId)
    {
        try
        {
            const string query = @"
                DELETE FROM [revenue].[PaymentScheduleInstalment]
                WHERE [revenue].[PaymentScheduleInstalment].[Id] = @InstalmentId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@InstalmentId", instalmentId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Deletes all instalments belonging to a payment schedule (used when deleting the entire schedule).
    /// </summary>
    public virtual async Task DeleteByScheduleIdAsync(int scheduleId)
    {
        try
        {
            const string query = @"
                DELETE FROM [revenue].[PaymentScheduleInstalment]
                WHERE [revenue].[PaymentScheduleInstalment].[PaymentScheduleId] = @PaymentScheduleId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@PaymentScheduleId", scheduleId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets a single instalment by its Id.
    /// </summary>
    public virtual async Task<PaymentScheduleInstalment?> GetByIdAsync(int id)
    {
        try
        {
            const string query = @"
                SELECT [revenue].[PaymentScheduleInstalment].[Id],
                       [revenue].[PaymentScheduleInstalment].[PaymentScheduleId],
                       [revenue].[PaymentScheduleInstalment].[SequenceNumber],
                       [revenue].[PaymentScheduleInstalment].[Amount],
                       [revenue].[PaymentScheduleInstalment].[MatchedAmount],
                       [revenue].[PaymentScheduleInstalment].[DueDate],
                       [revenue].[PaymentScheduleInstalment].[PaymentId],
                       [revenue].[PaymentScheduleInstalment].[ParentInstalmentId],
                       [revenue].[PaymentScheduleInstalment].[IsRemainder],
                       [revenue].[PaymentScheduleInstalment].[CreatedAtUtc]
                FROM [revenue].[PaymentScheduleInstalment]
                WHERE [revenue].[PaymentScheduleInstalment].[Id] = @Id";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Id", id));
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
