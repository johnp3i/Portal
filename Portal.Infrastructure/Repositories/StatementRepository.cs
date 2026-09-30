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
            // Sum every non-voided payment that belongs to this customer BEFORE the period,
            // counting the exact same set of rows the in-period line query renders
            // (see GetPaymentsInPeriodAsync + StatementService payment-line logic) so the
            // opening balance reconciles with the running balance across every period.
            //
            // A customer's payment is either:
            //   - linked to one of the customer's invoices (Payment.InvoiceId -> Invoice.CustomerId), or
            //   - a direct/standalone credit on the account (Payment.CustomerId), which has InvoiceId = NULL.
            // The old query used an INNER JOIN on Payment.InvoiceId, which silently dropped
            // standalone credits (NULL InvoiceId never matches), so the opening balance ignored
            // unallocated credits while the line query included them — the source of the mismatch.
            //
            // Parent/child de-dup: an allocated global payment is stored as one parent row
            // (InvoiceId NULL, ParentPaymentId NULL, Amount = total) plus child rows
            // (InvoiceId set, ParentPaymentId set). Summing all rows would double-count the
            // parent total against its children. The statement renders children always and the
            // parent only when it has NO non-voided children (a genuine unallocated credit), so
            // we exclude any parent row that has at least one non-voided child.
            const string query = @"
                SELECT ISNULL(SUM([revenue].[Payment].[Amount]), 0)
                FROM [revenue].[Payment]
                LEFT JOIN [invoice].[Invoice]
                    ON [revenue].[Payment].[InvoiceId] = [invoice].[Invoice].[Id]
                WHERE [revenue].[Payment].[BusinessId] = @BusinessId
                  AND [revenue].[Payment].[IsVoided] = 0
                  AND [revenue].[Payment].[PaymentDateUtc] < @BeforeDate
                  AND (
                      [invoice].[Invoice].[CustomerId] = @CustomerId
                      OR [revenue].[Payment].[CustomerId] = @CustomerId
                  )
                  AND NOT (
                      [revenue].[Payment].[InvoiceId] IS NULL
                      AND [revenue].[Payment].[ParentPaymentId] IS NULL
                      AND EXISTS (
                          SELECT 1 FROM [revenue].[Payment] AS [ChildPayment]
                          WHERE [ChildPayment].[ParentPaymentId] = [revenue].[Payment].[Id]
                            AND [ChildPayment].[IsVoided] = 0
                      )
                  )";

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
    /// Gets the total non-voided credit applied against a customer's invoices BEFORE the period start.
    /// Feeds the opening balance so that credits applied earlier reduce the brought-forward balance,
    /// exactly as payments do. Returns 0 when no applied credit exists.
    ///
    /// Mirrors the authoritative join pattern in CreditNoteRepository.GetTotalAppliedCreditAsync and
    /// DashboardService: applied credit = SUM(CreditNoteApplication.AmountApplied) for non-voided
    /// applications whose parent CreditNote belongs to the business, scoped to this customer.
    /// </summary>
    public virtual async Task<decimal> GetAppliedCreditTotalBeforeDateAsync(int customerId, int businessId, DateOnly beforeDate)
    {
        try
        {
            const string query = @"
                SELECT ISNULL(SUM([credit].[CreditNoteApplication].[AmountApplied]), 0)
                FROM [credit].[CreditNoteApplication]
                INNER JOIN [credit].[CreditNote]
                    ON [credit].[CreditNoteApplication].[CreditNoteId] = [credit].[CreditNote].[Id]
                WHERE [credit].[CreditNote].[CustomerId] = @CustomerId
                  AND [credit].[CreditNote].[BusinessId] = @BusinessId
                  AND [credit].[CreditNoteApplication].[IsVoided] = 0
                  AND [credit].[CreditNoteApplication].[AppliedAtUtc] < @BeforeDate";

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
    /// Gets all non-voided credit-note applications against a customer's invoices WITHIN the date range,
    /// ordered by the date the credit was applied. Each application becomes a credit line on the statement
    /// (it reduces what the customer owes, same side as a payment). Uses the same authoritative join as
    /// CreditNoteRepository.GetTotalAppliedCreditAsync.
    /// </summary>
    public virtual async Task<List<StatementCreditNoteDto>> GetAppliedCreditsInPeriodAsync(int customerId, int businessId, DateOnly fromDate, DateOnly toDate)
    {
        try
        {
            const string query = @"
                SELECT [credit].[CreditNoteApplication].[Id],
                       [credit].[CreditNoteApplication].[AppliedAtUtc],
                       [credit].[CreditNoteApplication].[AmountApplied],
                       [credit].[CreditNote].[CreditNoteNumber],
                       [invoice].[Invoice].[InvoiceNumber]
                FROM [credit].[CreditNoteApplication]
                INNER JOIN [credit].[CreditNote]
                    ON [credit].[CreditNoteApplication].[CreditNoteId] = [credit].[CreditNote].[Id]
                LEFT JOIN [invoice].[Invoice]
                    ON [credit].[CreditNoteApplication].[InvoiceId] = [invoice].[Invoice].[Id]
                WHERE [credit].[CreditNote].[CustomerId] = @CustomerId
                  AND [credit].[CreditNote].[BusinessId] = @BusinessId
                  AND [credit].[CreditNoteApplication].[IsVoided] = 0
                  AND [credit].[CreditNoteApplication].[AppliedAtUtc] >= @FromDate
                  AND [credit].[CreditNoteApplication].[AppliedAtUtc] < @ToDateExclusive
                ORDER BY [credit].[CreditNoteApplication].[AppliedAtUtc]";

            var results = new List<StatementCreditNoteDto>();
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
                // AppliedAtUtc is a full timestamp; use an exclusive upper bound of the day after
                // toDate so applications timestamped later on the final day are still included.
                command.Parameters.Add(new SqlParameter("@ToDateExclusive", toDate.AddDays(1).ToDateTime(TimeOnly.MinValue)));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new StatementCreditNoteDto
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                        AppliedDate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("AppliedAtUtc"))),
                        AmountApplied = reader.GetDecimal(reader.GetOrdinal("AmountApplied")),
                        CreditNoteNumber = reader.GetString(reader.GetOrdinal("CreditNoteNumber")),
                        InvoiceNumber = reader.IsDBNull(reader.GetOrdinal("InvoiceNumber")) ? null : reader.GetString(reader.GetOrdinal("InvoiceNumber"))
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
    /// Gets the date the customer account was created ([customer].[Customer].[CreatedAtUtc]),
    /// used to date the statement's Opening ("Balance brought forward") line. Returns null when
    /// the customer is not found for this business. This is display-only and does not affect any
    /// balance computation.
    /// </summary>
    public virtual async Task<DateOnly?> GetCustomerCreatedDateAsync(int customerId, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [customer].[Customer].[CreatedAtUtc]
                FROM [customer].[Customer]
                WHERE [customer].[Customer].[Id] = @CustomerId
                  AND [customer].[Customer].[BusinessId] = @BusinessId";

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

                var result = await command.ExecuteScalarAsync();
                if (result == null || result == DBNull.Value)
                    return null;

                return DateOnly.FromDateTime((DateTime)result);
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
