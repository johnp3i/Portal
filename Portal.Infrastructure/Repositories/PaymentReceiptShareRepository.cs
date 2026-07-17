using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for PaymentReceiptShare operations against [revenue].[PaymentReceiptShare].
/// </summary>
public class PaymentReceiptShareRepository : GenericStoredProcedureRepository<PaymentReceiptShare>
{
    public PaymentReceiptShareRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Inserts a new receipt share and returns the new Id.
    /// </summary>
    public virtual async Task<int> InsertAsync(PaymentReceiptShare entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [revenue].[PaymentReceiptShare]
                    ([PaymentReceiptId], [BusinessId], [ShareToken], [SnapshotHtml],
                     [CustomerEmail], [ExpiresAtUtc], [IsActive], [CreatedAtUtc], [CreatedByUserId])
                OUTPUT INSERTED.Id
                VALUES
                    (@PaymentReceiptId, @BusinessId, @ShareToken, @SnapshotHtml,
                     @CustomerEmail, @ExpiresAtUtc, @IsActive, @CreatedAtUtc, @CreatedByUserId)";

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

                command.Parameters.Add(new SqlParameter("@PaymentReceiptId", entity.PaymentReceiptId));
                command.Parameters.Add(new SqlParameter("@BusinessId", entity.BusinessId));
                command.Parameters.Add(new SqlParameter("@ShareToken", entity.ShareToken));
                command.Parameters.Add(new SqlParameter("@SnapshotHtml", entity.SnapshotHtml));
                command.Parameters.Add(new SqlParameter("@CustomerEmail", entity.CustomerEmail));
                command.Parameters.Add(new SqlParameter("@ExpiresAtUtc", entity.ExpiresAtUtc));
                command.Parameters.Add(new SqlParameter("@IsActive", entity.IsActive));
                command.Parameters.Add(new SqlParameter("@CreatedAtUtc", entity.CreatedAtUtc));
                command.Parameters.Add(new SqlParameter("@CreatedByUserId", entity.CreatedByUserId));

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
    /// Gets a share by its token (public access — no business filter).
    /// </summary>
    public virtual async Task<PaymentReceiptShare?> GetByTokenAsync(string token)
    {
        try
        {
            const string query = @"
                SELECT [revenue].[PaymentReceiptShare].[Id],
                       [revenue].[PaymentReceiptShare].[PaymentReceiptId],
                       [revenue].[PaymentReceiptShare].[BusinessId],
                       [revenue].[PaymentReceiptShare].[ShareToken],
                       [revenue].[PaymentReceiptShare].[SnapshotHtml],
                       [revenue].[PaymentReceiptShare].[CustomerEmail],
                       [revenue].[PaymentReceiptShare].[ExpiresAtUtc],
                       [revenue].[PaymentReceiptShare].[IsActive],
                       [revenue].[PaymentReceiptShare].[CreatedAtUtc],
                       [revenue].[PaymentReceiptShare].[CreatedByUserId]
                FROM [revenue].[PaymentReceiptShare]
                WHERE [revenue].[PaymentReceiptShare].[ShareToken] = @Token
                  AND [revenue].[PaymentReceiptShare].[IsActive] = 1";

            return await ExecuteSingleRecordStoredProcedureUnfiltered(query,
                new SqlParameter("@Token", token));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Deactivates all active share links for a receipt (used when receipt is voided).
    /// </summary>
    public virtual async Task DeactivateByReceiptIdAsync(int receiptId, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [revenue].[PaymentReceiptShare]
                SET [IsActive] = 0
                WHERE [revenue].[PaymentReceiptShare].[PaymentReceiptId] = @ReceiptId
                  AND [revenue].[PaymentReceiptShare].[BusinessId] = @BusinessId
                  AND [revenue].[PaymentReceiptShare].[IsActive] = 1";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@ReceiptId", receiptId),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
