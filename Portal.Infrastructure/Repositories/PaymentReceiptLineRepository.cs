using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for PaymentReceiptLine entity operations against the [revenue].[PaymentReceiptLine] table.
/// </summary>
public class PaymentReceiptLineRepository : GenericStoredProcedureRepository<PaymentReceiptLine>
{
    public PaymentReceiptLineRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Inserts a single receipt line and returns the new Id.
    /// </summary>
    public virtual async Task<int> InsertAsync(PaymentReceiptLine entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [revenue].[PaymentReceiptLine]
                    ([PaymentReceiptId], [PaymentId], [InvoiceId], [InvoiceNumber],
                     [Amount], [InvoiceTotal], [InvoiceOutstandingBefore], [InvoiceOutstandingAfter])
                OUTPUT INSERTED.Id
                VALUES
                    (@PaymentReceiptId, @PaymentId, @InvoiceId, @InvoiceNumber,
                     @Amount, @InvoiceTotal, @InvoiceOutstandingBefore, @InvoiceOutstandingAfter)";

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
                command.Parameters.Add(new SqlParameter("@PaymentId", entity.PaymentId));
                command.Parameters.Add(new SqlParameter("@InvoiceId", entity.InvoiceId));
                command.Parameters.Add(new SqlParameter("@InvoiceNumber", entity.InvoiceNumber));
                command.Parameters.Add(new SqlParameter("@Amount", entity.Amount));
                command.Parameters.Add(new SqlParameter("@InvoiceTotal", entity.InvoiceTotal));
                command.Parameters.Add(new SqlParameter("@InvoiceOutstandingBefore", entity.InvoiceOutstandingBefore));
                command.Parameters.Add(new SqlParameter("@InvoiceOutstandingAfter", entity.InvoiceOutstandingAfter));

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
    /// Gets all lines for a receipt.
    /// </summary>
    public virtual async Task<List<PaymentReceiptLine>> GetByReceiptIdAsync(int receiptId)
    {
        try
        {
            const string query = @"
                SELECT [revenue].[PaymentReceiptLine].[Id],
                       [revenue].[PaymentReceiptLine].[PaymentReceiptId],
                       [revenue].[PaymentReceiptLine].[PaymentId],
                       [revenue].[PaymentReceiptLine].[InvoiceId],
                       [revenue].[PaymentReceiptLine].[InvoiceNumber],
                       [revenue].[PaymentReceiptLine].[Amount],
                       [revenue].[PaymentReceiptLine].[InvoiceTotal],
                       [revenue].[PaymentReceiptLine].[InvoiceOutstandingBefore],
                       [revenue].[PaymentReceiptLine].[InvoiceOutstandingAfter]
                FROM [revenue].[PaymentReceiptLine]
                WHERE [revenue].[PaymentReceiptLine].[PaymentReceiptId] = @ReceiptId
                ORDER BY [revenue].[PaymentReceiptLine].[Id]";

            return await ExecuteStoredProcedureUnfiltered(query,
                new SqlParameter("@ReceiptId", receiptId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
