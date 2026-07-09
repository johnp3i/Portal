using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for PaymentSchedule entity CRUD operations against the [revenue].[PaymentSchedule] table.
/// </summary>
public class PaymentScheduleRepository : GenericStoredProcedureRepository<PaymentSchedule>
{
    public PaymentScheduleRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Inserts a new payment schedule record and returns the new PaymentSchedule.Id via OUTPUT INSERTED.Id.
    /// </summary>
    public virtual async Task<int> InsertAsync(PaymentSchedule entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [revenue].[PaymentSchedule]
                    ([BusinessId], [InvoiceId], [IsActive], [CreatedAtUtc], [CreatedByUserId])
                OUTPUT INSERTED.Id
                VALUES
                    (@BusinessId, @InvoiceId, @IsActive, @CreatedAtUtc, @CreatedByUserId)";

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
                command.Parameters.Add(new SqlParameter("@InvoiceId", entity.InvoiceId));
                command.Parameters.Add(new SqlParameter("@IsActive", entity.IsActive));
                command.Parameters.Add(new SqlParameter("@CreatedAtUtc", entity.CreatedAtUtc));
                command.Parameters.Add(new SqlParameter("@CreatedByUserId", entity.CreatedByUserId ?? (object)DBNull.Value));

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
    /// Gets the active payment schedule for a given invoice and business (tenant isolation).
    /// Returns null if no active schedule exists.
    /// </summary>
    public virtual async Task<PaymentSchedule?> GetByInvoiceIdAsync(int invoiceId, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [revenue].[PaymentSchedule].[Id],
                       [revenue].[PaymentSchedule].[BusinessId],
                       [revenue].[PaymentSchedule].[InvoiceId],
                       [revenue].[PaymentSchedule].[IsActive],
                       [revenue].[PaymentSchedule].[CreatedAtUtc],
                       [revenue].[PaymentSchedule].[CreatedByUserId]
                FROM [revenue].[PaymentSchedule]
                WHERE [revenue].[PaymentSchedule].[InvoiceId] = @InvoiceId
                  AND [revenue].[PaymentSchedule].[BusinessId] = @BusinessId
                  AND [revenue].[PaymentSchedule].[IsActive] = 1";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@InvoiceId", invoiceId),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets a single payment schedule by Id and BusinessId for tenant isolation.
    /// </summary>
    public virtual async Task<PaymentSchedule?> GetByIdAndBusinessIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [revenue].[PaymentSchedule].[Id],
                       [revenue].[PaymentSchedule].[BusinessId],
                       [revenue].[PaymentSchedule].[InvoiceId],
                       [revenue].[PaymentSchedule].[IsActive],
                       [revenue].[PaymentSchedule].[CreatedAtUtc],
                       [revenue].[PaymentSchedule].[CreatedByUserId]
                FROM [revenue].[PaymentSchedule]
                WHERE [revenue].[PaymentSchedule].[Id] = @Id
                  AND [revenue].[PaymentSchedule].[BusinessId] = @BusinessId";

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
    /// Deletes a payment schedule record by Id.
    /// </summary>
    public virtual async Task DeleteAsync(int scheduleId)
    {
        try
        {
            const string query = @"
                DELETE FROM [revenue].[PaymentSchedule]
                WHERE [revenue].[PaymentSchedule].[Id] = @ScheduleId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@ScheduleId", scheduleId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
