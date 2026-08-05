using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for PayslipEmailLog entity operations against the [payroll].[PayslipEmailLog] table.
/// Handles insert and query operations for payslip email send tracking.
/// </summary>
public class PayslipEmailLogRepository : GenericStoredProcedureRepository<PayslipEmailLog>
{
    public PayslipEmailLogRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Inserts a new payslip email log entry.
    /// </summary>
    public virtual async Task InsertAsync(PayslipEmailLog entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [payroll].[PayslipEmailLog]
                    ([PayslipId], [SentByUserId], [SentToEmail], [IsSuccess], [FailureReason])
                VALUES
                    (@PayslipId, @SentByUserId, @SentToEmail, @IsSuccess, @FailureReason)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@PayslipId", entity.PayslipId),
                new SqlParameter("@SentByUserId", entity.SentByUserId),
                new SqlParameter("@SentToEmail", entity.SentToEmail),
                new SqlParameter("@IsSuccess", entity.IsSuccess),
                new SqlParameter("@FailureReason", entity.FailureReason ?? (object)DBNull.Value)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets all email log entries for a payslip, ordered by SentAtUtc descending (newest first).
    /// </summary>
    public virtual async Task<List<PayslipEmailLog>> GetByPayslipIdAsync(int payslipId)
    {
        try
        {
            const string query = @"
                SELECT PayslipEmailLog.Id, PayslipEmailLog.PayslipId, PayslipEmailLog.SentByUserId,
                       PayslipEmailLog.SentToEmail, PayslipEmailLog.SentAtUtc, PayslipEmailLog.IsSuccess,
                       PayslipEmailLog.FailureReason, PayslipEmailLog.CreatedAtUtc
                FROM [payroll].[PayslipEmailLog]
                WHERE PayslipEmailLog.PayslipId = @PayslipId
                ORDER BY PayslipEmailLog.SentAtUtc DESC";

            return await _context.Set<PayslipEmailLog>()
                .FromSqlRaw(query, new SqlParameter("@PayslipId", payslipId))
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets the last successful email send for a payslip (for duplicate detection).
    /// Returns null if no successful send exists.
    /// </summary>
    public virtual async Task<PayslipEmailLog?> GetLastByPayslipIdAsync(int payslipId)
    {
        try
        {
            const string query = @"
                SELECT TOP 1 PayslipEmailLog.Id, PayslipEmailLog.PayslipId, PayslipEmailLog.SentByUserId,
                       PayslipEmailLog.SentToEmail, PayslipEmailLog.SentAtUtc, PayslipEmailLog.IsSuccess,
                       PayslipEmailLog.FailureReason, PayslipEmailLog.CreatedAtUtc
                FROM [payroll].[PayslipEmailLog]
                WHERE PayslipEmailLog.PayslipId = @PayslipId
                  AND PayslipEmailLog.IsSuccess = 1
                ORDER BY PayslipEmailLog.SentAtUtc DESC";

            return (await _context.Set<PayslipEmailLog>()
                .FromSqlRaw(query, new SqlParameter("@PayslipId", payslipId))
                .ToListAsync()).FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
