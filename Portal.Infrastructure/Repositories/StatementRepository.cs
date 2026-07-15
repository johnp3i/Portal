using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for statement-related queries against the Invoice and Payment tables.
/// Provides optimised queries for opening balance computation and in-period transaction retrieval.
/// </summary>
public class StatementRepository : GenericStoredProcedureRepository<Invoice>
{
    public StatementRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Gets the sum of TotalAmount for issued, non-deleted invoices before the period start date.
    /// Returns 0 when no invoices exist before the date.
    /// </summary>
    public virtual async Task<decimal> GetInvoicedTotalBeforeDateAsync(int customerId, int businessId, DateOnly beforeDate)
    {
        try
        {
            const string query = @"
                SELECT ISNULL(SUM([invoice].[Invoice].[TotalAmount]), 0)
                FROM [invoice].[Invoice]
                WHERE [invoice].[Invoice].[CustomerId] = @CustomerId
                  AND [invoice].[Invoice].[BusinessId] = @BusinessId
                  AND [invoice].[Invoice].[InvoiceStatusTypeId] = 2
                  AND [invoice].[Invoice].[IsDeleted] = 0
                  AND [invoice].[Invoice].[InvoiceDate] < @BeforeDate";

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

                command.Parameters.Add(new SqlParameter("@CustomerId", customerId));
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@BeforeDate", beforeDate.ToDateTime(TimeOnly.MinValue)));

                var result = await command.ExecuteScalarAsync();
                return result != null && result != DBNull.Value ? (decimal)result : 0m;
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets the sum of valid (non-voided) payment amounts for a customer's invoices before the period start date.
    /// Returns 0 when no payments exist before the date.
    /// </summary>
    public virtual async Task<decimal> GetPaidTotalBeforeDateAsync(int customerId, int businessId, DateOnly beforeDate)
    {
        try
        {
            const string query = @"
                SELECT ISNULL(SUM([revenue].[Payment].[Amount]), 0)
                FROM [revenue].[Payment]
                INNER JOIN [invoice].[Invoice]
                    ON [revenue].[Payment].[InvoiceId] = [invoice].[Invoice].[Id]
                WHERE [invoice].[Invoice].[CustomerId] = @CustomerId
                  AND [revenue].[Payment].[BusinessId] = @BusinessId
                  AND [revenue].[Payment].[IsVoided] = 0
                  AND [revenue].[Payment].[PaymentDateUtc] < @BeforeDate";

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

                command.Parameters.Add(new SqlParameter("@CustomerId", customerId));
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@BeforeDate", beforeDate.ToDateTime(TimeOnly.MinValue)));

                var result = await command.ExecuteScalarAsync();
                return result != null && result != DBNull.Value ? (decimal)result : 0m;
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets all issued, non-deleted invoices for a customer within the date range, ordered by InvoiceDate.
    /// </summary>
    public virtual async Task<List<StatementInvoiceDto>> GetInvoicesInPeriodAsync(int customerId, int businessId, DateOnly fromDate, DateOnly toDate)
    {
        try
        {
            const string query = @"
                SELECT [invoice].[Invoice].[Id],
                       [invoice].[Invoice].[InvoiceDate],
                       [invoice].[Invoice].[InvoiceNumber],
                       [invoice].[Invoice].[Notes],
                       [invoice].[Invoice].[TotalAmount]
                FROM [invoice].[Invoice]
                WHERE [invoice].[Invoice].[CustomerId] = @CustomerId
                  AND [invoice].[Invoice].[BusinessId] = @BusinessId
                  AND [invoice].[Invoice].[InvoiceStatusTypeId] = 2
                  AND [invoice].[Invoice].[IsDeleted] = 0
                  AND [invoice].[Invoice].[InvoiceDate] >= @FromDate
                  AND [invoice].[Invoice].[InvoiceDate] <= @ToDate
                ORDER BY [invoice].[Invoice].[InvoiceDate]";

            var results = new List<StatementInvoiceDto>();
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

                command.Parameters.Add(new SqlParameter("@CustomerId", customerId));
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@FromDate", fromDate.ToDateTime(TimeOnly.MinValue)));
                command.Parameters.Add(new SqlParameter("@ToDate", toDate.ToDateTime(TimeOnly.MinValue)));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new StatementInvoiceDto
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                        InvoiceDate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("InvoiceDate"))),
                        InvoiceNumber = reader.GetString(reader.GetOrdinal("InvoiceNumber")),
                        Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? null : reader.GetString(reader.GetOrdinal("Notes")),
                        TotalAmount = reader.GetDecimal(reader.GetOrdinal("TotalAmount"))
                    });
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return results;
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets all valid (non-voided) payments for a customer's invoices within the date range,
    /// including PaymentMethodType name. Ordered by PaymentDateUtc.
    /// </summary>
    public virtual async Task<List<StatementPaymentDto>> GetPaymentsInPeriodAsync(int customerId, int businessId, DateOnly fromDate, DateOnly toDate)
    {
        try
        {
            const string query = @"
                SELECT [revenue].[Payment].[Id],
                       [revenue].[Payment].[PaymentDateUtc],
                       [revenue].[Payment].[Amount],
                       [revenue].[Payment].[Reference],
                       [revenue].[Payment].[Notes],
                       [revenue].[Payment].[ParentPaymentId],
                       [revenue].[Payment].[IsAutoAllocated],
                       [revenue].[PaymentMethodType].[Name],
                       [invoice].[Invoice].[InvoiceNumber]
                FROM [revenue].[Payment]
                LEFT JOIN [invoice].[Invoice]
                    ON [revenue].[Payment].[InvoiceId] = [invoice].[Invoice].[Id]
                INNER JOIN [revenue].[PaymentMethodType]
                    ON [revenue].[Payment].[PaymentMethodTypeId] = [revenue].[PaymentMethodType].[Id]
                WHERE [revenue].[Payment].[BusinessId] = @BusinessId
                  AND [revenue].[Payment].[IsVoided] = 0
                  AND [revenue].[Payment].[PaymentDateUtc] >= @FromDate
                  AND [revenue].[Payment].[PaymentDateUtc] <= @ToDate
                  AND (
                      [invoice].[Invoice].[CustomerId] = @CustomerId
                      OR [revenue].[Payment].[CustomerId] = @CustomerId
                  )
                ORDER BY [revenue].[Payment].[PaymentDateUtc]";

            var results = new List<StatementPaymentDto>();
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

                command.Parameters.Add(new SqlParameter("@CustomerId", customerId));
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@FromDate", fromDate.ToDateTime(TimeOnly.MinValue)));
                command.Parameters.Add(new SqlParameter("@ToDate", toDate.ToDateTime(TimeOnly.MinValue)));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new StatementPaymentDto
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                        PaymentDateUtc = reader.GetDateTime(reader.GetOrdinal("PaymentDateUtc")),
                        Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
                        Reference = reader.IsDBNull(reader.GetOrdinal("Reference")) ? null : reader.GetString(reader.GetOrdinal("Reference")),
                        Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? null : reader.GetString(reader.GetOrdinal("Notes")),
                        PaymentMethodName = reader.GetString(reader.GetOrdinal("Name")),
                        ParentPaymentId = reader.IsDBNull(reader.GetOrdinal("ParentPaymentId")) ? null : reader.GetInt32(reader.GetOrdinal("ParentPaymentId")),
                        InvoiceNumber = reader.IsDBNull(reader.GetOrdinal("InvoiceNumber")) ? null : reader.GetString(reader.GetOrdinal("InvoiceNumber")),
                        IsAutoAllocated = reader.GetBoolean(reader.GetOrdinal("IsAutoAllocated"))
                    });
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return results;
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Persists an email history record when a statement is successfully emailed.
    /// </summary>
    public virtual async Task InsertEmailHistoryAsync(StatementEmailHistory entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [customer].[StatementEmailHistory]
                    ([BusinessId], [CustomerId], [FromDate], [ToDate], [RecipientEmail], [SentByUserId], [SentAtUtc])
                VALUES
                    (@BusinessId, @CustomerId, @FromDate, @ToDate, @RecipientEmail, @SentByUserId, @SentAtUtc)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@CustomerId", entity.CustomerId),
                new SqlParameter("@FromDate", entity.FromDate.ToDateTime(TimeOnly.MinValue)),
                new SqlParameter("@ToDate", entity.ToDate.ToDateTime(TimeOnly.MinValue)),
                new SqlParameter("@RecipientEmail", entity.RecipientEmail ?? (object)DBNull.Value),
                new SqlParameter("@SentByUserId", entity.SentByUserId ?? (object)DBNull.Value),
                new SqlParameter("@SentAtUtc", entity.SentAtUtc)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets all email history records for a customer, ordered by SentAtUtc descending.
    /// Joins with AspNetUsers to resolve the sender's display name.
    /// </summary>
    public virtual async Task<List<StatementEmailHistoryDto>> GetEmailHistoryByCustomerAsync(int customerId, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [customer].[StatementEmailHistory].[SentAtUtc],
                       [customer].[StatementEmailHistory].[FromDate],
                       [customer].[StatementEmailHistory].[ToDate],
                       [customer].[StatementEmailHistory].[RecipientEmail],
                       [customer].[StatementEmailHistory].[SentByUserId] AS [SentByDisplayName]
                FROM [customer].[StatementEmailHistory]
                WHERE [customer].[StatementEmailHistory].[CustomerId] = @CustomerId
                  AND [customer].[StatementEmailHistory].[BusinessId] = @BusinessId
                ORDER BY [customer].[StatementEmailHistory].[SentAtUtc] DESC";

            var results = new List<StatementEmailHistoryDto>();
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

                command.Parameters.Add(new SqlParameter("@CustomerId", customerId));
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new StatementEmailHistoryDto
                    {
                        SentAtUtc = reader.GetDateTime(reader.GetOrdinal("SentAtUtc")),
                        FromDate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("FromDate"))),
                        ToDate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("ToDate"))),
                        RecipientEmail = reader.GetString(reader.GetOrdinal("RecipientEmail")),
                        SentByDisplayName = reader.GetString(reader.GetOrdinal("SentByDisplayName"))
                    });
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return results;
        }
        catch (Exception)
        {
            throw;
        }
    }
}
