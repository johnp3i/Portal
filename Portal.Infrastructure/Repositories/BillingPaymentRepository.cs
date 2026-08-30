using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities.Billing;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for BillingPayment entity CRUD operations against the [billing].[Payment] table.
/// </summary>
public class BillingPaymentRepository : GenericStoredProcedureRepository<BillingPayment>
{
    public BillingPaymentRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Inserts a new billing payment record and returns the new BillingPayment.Id via OUTPUT INSERTED.Id.
    /// </summary>
    public virtual async Task<int> InsertAsync(BillingPayment entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [billing].[Payment]
                    ([InvoiceId], [AmountEur], [Method], [PaidAtUtc],
                     [StripePaymentIntentId], [Reference], [Notes], [RecordedByUserId], [CreatedAtUtc])
                OUTPUT INSERTED.Id
                VALUES
                    (@InvoiceId, @AmountEur, @Method, @PaidAtUtc,
                     @StripePaymentIntentId, @Reference, @Notes, @RecordedByUserId, @CreatedAtUtc)";

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

                command.Parameters.Add(new SqlParameter("@InvoiceId", entity.InvoiceId));
                command.Parameters.Add(new SqlParameter("@AmountEur", entity.AmountEur));
                command.Parameters.Add(new SqlParameter("@Method", entity.Method));
                command.Parameters.Add(new SqlParameter("@PaidAtUtc", entity.PaidAtUtc));
                command.Parameters.Add(new SqlParameter("@StripePaymentIntentId", entity.StripePaymentIntentId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Reference", entity.Reference ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@RecordedByUserId", entity.RecordedByUserId ?? (object)DBNull.Value));
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
    /// Gets all billing payments for a given invoice, ordered by PaidAtUtc descending.
    /// </summary>
    public virtual async Task<List<BillingPayment>> GetByInvoiceIdAsync(int invoiceId)
    {
        try
        {
            const string query = @"
                SELECT [billing].[Payment].[Id],
                       [billing].[Payment].[InvoiceId],
                       [billing].[Payment].[AmountEur],
                       [billing].[Payment].[Method],
                       [billing].[Payment].[PaidAtUtc],
                       [billing].[Payment].[StripePaymentIntentId],
                       [billing].[Payment].[Reference],
                       [billing].[Payment].[Notes],
                       [billing].[Payment].[RecordedByUserId],
                       [billing].[Payment].[CreatedAtUtc]
                FROM [billing].[Payment]
                WHERE [billing].[Payment].[InvoiceId] = @InvoiceId
                ORDER BY [billing].[Payment].[PaidAtUtc] DESC";

            return await ExecuteStoredProcedure(query,
                new SqlParameter("@InvoiceId", invoiceId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets the total amount already paid for a given invoice (sum of all linked payments).
    /// Returns 0 if no payments exist.
    /// </summary>
    public virtual async Task<decimal> GetTotalPaidByInvoiceIdAsync(int invoiceId)
    {
        try
        {
            const string query = @"
                SELECT ISNULL(SUM([billing].[Payment].[AmountEur]), 0)
                FROM [billing].[Payment]
                WHERE [billing].[Payment].[InvoiceId] = @InvoiceId";

            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;

                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@InvoiceId", invoiceId));

                var result = await command.ExecuteScalarAsync();
                return result != null && result != DBNull.Value ? (decimal)result : 0m;
            }
            finally
            {
                if (connection.State == System.Data.ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
