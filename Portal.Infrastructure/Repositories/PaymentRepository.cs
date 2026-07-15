using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for Payment entity CRUD operations against the [revenue].[Payment] table.
/// </summary>
public class PaymentRepository : GenericStoredProcedureRepository<Payment>
{
    public PaymentRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Standard column list for Payment SELECT queries.
    /// </summary>
    private const string PaymentColumns = @"
        [revenue].[Payment].[Id],
        [revenue].[Payment].[BusinessId],
        [revenue].[Payment].[InvoiceId],
        [revenue].[Payment].[ParentPaymentId],
        [revenue].[Payment].[IsAutoAllocated],
        [revenue].[Payment].[CustomerId],
        [revenue].[Payment].[CreditAmount],
        [revenue].[Payment].[PaymentMethodTypeId],
        [revenue].[Payment].[PaymentDateUtc],
        [revenue].[Payment].[Amount],
        [revenue].[Payment].[Reference],
        [revenue].[Payment].[Notes],
        [revenue].[Payment].[IsVoided],
        [revenue].[Payment].[CreatedAtUtc],
        [revenue].[Payment].[CreatedByUserId]";

    /// <summary>
    /// Inserts a new payment record and returns the new Payment.Id via OUTPUT INSERTED.Id.
    /// </summary>
    public virtual async Task<int> InsertAsync(Payment entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [revenue].[Payment]
                    ([BusinessId], [InvoiceId], [ParentPaymentId], [IsAutoAllocated],
                     [CustomerId], [CreditAmount], [PaymentMethodTypeId], [PaymentDateUtc],
                     [Amount], [Reference], [Notes], [IsVoided], [CreatedAtUtc], [CreatedByUserId])
                OUTPUT INSERTED.Id
                VALUES
                    (@BusinessId, @InvoiceId, @ParentPaymentId, @IsAutoAllocated,
                     @CustomerId, @CreditAmount, @PaymentMethodTypeId, @PaymentDateUtc,
                     @Amount, @Reference, @Notes, @IsVoided, @CreatedAtUtc, @CreatedByUserId)";

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
                command.Parameters.Add(new SqlParameter("@InvoiceId", entity.InvoiceId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@ParentPaymentId", entity.ParentPaymentId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@IsAutoAllocated", entity.IsAutoAllocated));
                command.Parameters.Add(new SqlParameter("@CustomerId", entity.CustomerId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@CreditAmount", entity.CreditAmount));
                command.Parameters.Add(new SqlParameter("@PaymentMethodTypeId", entity.PaymentMethodTypeId));
                command.Parameters.Add(new SqlParameter("@PaymentDateUtc", entity.PaymentDateUtc));
                command.Parameters.Add(new SqlParameter("@Amount", entity.Amount));
                command.Parameters.Add(new SqlParameter("@Reference", entity.Reference ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@IsVoided", entity.IsVoided));
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
    /// Gets a single payment by Id and BusinessId for tenant isolation.
    /// </summary>
    public virtual async Task<Payment?> GetByIdAndBusinessIdAsync(int id, int businessId)
    {
        try
        {
            string query = $@"
                SELECT {PaymentColumns}
                FROM [revenue].[Payment]
                WHERE [revenue].[Payment].[Id] = @Id
                  AND [revenue].[Payment].[BusinessId] = @BusinessId";

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
    /// Sets IsVoided = 1 on a payment record. Does not physically delete.
    /// </summary>
    public virtual async Task VoidAsync(int paymentId)
    {
        try
        {
            const string query = @"
                UPDATE [revenue].[Payment]
                SET [IsVoided] = 1
                WHERE [revenue].[Payment].[Id] = @PaymentId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@PaymentId", paymentId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets all non-voided payments for an invoice (for balance calculation).
    /// </summary>
    public virtual async Task<List<Payment>> GetValidPaymentsByInvoiceIdAsync(int invoiceId, int businessId)
    {
        try
        {
            string query = $@"
                SELECT {PaymentColumns}
                FROM [revenue].[Payment]
                WHERE [revenue].[Payment].[InvoiceId] = @InvoiceId
                  AND [revenue].[Payment].[BusinessId] = @BusinessId
                  AND [revenue].[Payment].[IsVoided] = 0";

            return await ExecuteStoredProcedure(query,
                new SqlParameter("@InvoiceId", invoiceId),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets all payments for an invoice including voided (for payment history display).
    /// </summary>
    public virtual async Task<List<Payment>> GetAllPaymentsByInvoiceIdAsync(int invoiceId, int businessId)
    {
        try
        {
            string query = $@"
                SELECT {PaymentColumns}
                FROM [revenue].[Payment]
                WHERE [revenue].[Payment].[InvoiceId] = @InvoiceId
                  AND [revenue].[Payment].[BusinessId] = @BusinessId";

            var results = await ExecuteStoredProcedure(query,
                new SqlParameter("@InvoiceId", invoiceId),
                new SqlParameter("@BusinessId", businessId));

            return results.OrderByDescending(p => p.PaymentDateUtc).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets the sum of valid (non-voided) payment amounts for an invoice.
    /// </summary>
    public virtual async Task<decimal> GetTotalPaidAsync(int invoiceId, int businessId)
    {
        try
        {
            const string query = @"
                SELECT ISNULL(SUM([revenue].[Payment].[Amount]), 0)
                FROM [revenue].[Payment]
                WHERE [revenue].[Payment].[InvoiceId] = @InvoiceId
                  AND [revenue].[Payment].[BusinessId] = @BusinessId
                  AND [revenue].[Payment].[IsVoided] = 0";

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

                command.Parameters.Add(new SqlParameter("@InvoiceId", invoiceId));
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));

                var result = await command.ExecuteScalarAsync();
                return result != null && result != DBNull.Value ? (decimal)result : 0m;
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
    /// Gets the sum of valid (non-voided) payment amounts within a date range for a business.
    /// </summary>
    public virtual async Task<decimal> GetPaidInPeriodAsync(int businessId, DateTime fromUtc, DateTime toUtc)
    {
        try
        {
            const string query = @"
                SELECT ISNULL(SUM([revenue].[Payment].[Amount]), 0)
                FROM [revenue].[Payment]
                WHERE [revenue].[Payment].[BusinessId] = @BusinessId
                  AND [revenue].[Payment].[IsVoided] = 0
                  AND [revenue].[Payment].[PaymentDateUtc] >= @FromUtc
                  AND [revenue].[Payment].[PaymentDateUtc] < @ToUtc";

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

                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@FromUtc", fromUtc));
                command.Parameters.Add(new SqlParameter("@ToUtc", toUtc));

                var result = await command.ExecuteScalarAsync();
                return result != null && result != DBNull.Value ? (decimal)result : 0m;
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
    /// Gets monthly payment totals grouped by Year/Month from the specified date onwards.
    /// </summary>
    public virtual async Task<List<MonthlyRevenueDto>> GetMonthlyTotalsAsync(int businessId, DateTime fromUtc)
    {
        try
        {
            const string query = @"
                SELECT YEAR([revenue].[Payment].[PaymentDateUtc]) AS [Year],
                       MONTH([revenue].[Payment].[PaymentDateUtc]) AS [Month],
                       SUM([revenue].[Payment].[Amount]) AS [Amount]
                FROM [revenue].[Payment]
                WHERE [revenue].[Payment].[BusinessId] = @BusinessId
                  AND [revenue].[Payment].[IsVoided] = 0
                  AND [revenue].[Payment].[PaymentDateUtc] >= @FromUtc
                GROUP BY YEAR([revenue].[Payment].[PaymentDateUtc]),
                         MONTH([revenue].[Payment].[PaymentDateUtc])
                ORDER BY [Year], [Month]";

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

                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@FromUtc", fromUtc));

                var results = new List<MonthlyRevenueDto>();

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var year = reader.GetInt32(0);
                    var month = reader.GetInt32(1);
                    results.Add(new MonthlyRevenueDto
                    {
                        Year = year,
                        Month = month,
                        Label = new DateTime(year, month, 1).ToString("MMM"),
                        Amount = reader.GetDecimal(2)
                    });
                }

                return results;
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
    /// Gets recent payments (including voided) with Invoice, Customer, and PaymentMethodType joins.
    /// NOTE: Parent payments (InvoiceId = NULL) are excluded by design — the INNER JOIN on Invoice
    /// filters them out. The dashboard shows allocation-level payments only. Pure credit payments
    /// and unallocated parents are managed via the Statement view.
    /// </summary>
    public virtual async Task<(List<RecentPaymentDto> Items, int TotalCount)> GetRecentPaymentsPagedAsync(
        int businessId, string? searchTerm, int offset, int pageSize)
    {
        try
        {
            var searchFilter = string.IsNullOrWhiteSpace(searchTerm)
                ? ""
                : @" AND ([invoice].[Invoice].[InvoiceNumber] LIKE @SearchTerm
                       OR [customer].[Customer].[Name] LIKE @SearchTerm)";

            var countQuery = $@"
                SELECT COUNT(*)
                FROM [revenue].[Payment]
                INNER JOIN [invoice].[Invoice]
                    ON [revenue].[Payment].[InvoiceId] = [invoice].[Invoice].[Id]
                INNER JOIN [customer].[Customer]
                    ON [invoice].[Invoice].[CustomerId] = [customer].[Customer].[Id]
                INNER JOIN [revenue].[PaymentMethodType]
                    ON [revenue].[Payment].[PaymentMethodTypeId] = [revenue].[PaymentMethodType].[Id]
                WHERE [revenue].[Payment].[BusinessId] = @BusinessId{searchFilter}";

            var dataQuery = $@"
                SELECT [revenue].[Payment].[Id],
                       [revenue].[Payment].[PaymentDateUtc],
                       [invoice].[Invoice].[InvoiceNumber],
                       [customer].[Customer].[Name],
                       [revenue].[PaymentMethodType].[Name],
                       [revenue].[Payment].[Amount],
                       CASE WHEN [invoice].[Invoice].[InvoiceFinancialStatusTypeId] = 3
                            THEN CAST(1 AS BIT)
                            ELSE CAST(0 AS BIT)
                       END AS [IsFullPayment],
                       [revenue].[Payment].[IsVoided]
                FROM [revenue].[Payment]
                INNER JOIN [invoice].[Invoice]
                    ON [revenue].[Payment].[InvoiceId] = [invoice].[Invoice].[Id]
                INNER JOIN [customer].[Customer]
                    ON [invoice].[Invoice].[CustomerId] = [customer].[Customer].[Id]
                INNER JOIN [revenue].[PaymentMethodType]
                    ON [revenue].[Payment].[PaymentMethodTypeId] = [revenue].[PaymentMethodType].[Id]
                WHERE [revenue].[Payment].[BusinessId] = @BusinessId{searchFilter}
                ORDER BY [revenue].[Payment].[PaymentDateUtc] DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                var transaction = _context.Database.CurrentTransaction;

                int totalCount;
                using (var countCommand = connection.CreateCommand())
                {
                    countCommand.CommandText = countQuery;
                    if (transaction != null)
                        countCommand.Transaction = transaction.GetDbTransaction();
                    countCommand.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                    if (!string.IsNullOrWhiteSpace(searchTerm))
                        countCommand.Parameters.Add(new SqlParameter("@SearchTerm", $"%{searchTerm}%"));
                    var countResult = await countCommand.ExecuteScalarAsync();
                    totalCount = (int)countResult!;
                }

                var items = new List<RecentPaymentDto>();
                using (var dataCommand = connection.CreateCommand())
                {
                    dataCommand.CommandText = dataQuery;
                    if (transaction != null)
                        dataCommand.Transaction = transaction.GetDbTransaction();
                    dataCommand.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                    dataCommand.Parameters.Add(new SqlParameter("@Offset", offset));
                    dataCommand.Parameters.Add(new SqlParameter("@PageSize", pageSize));
                    if (!string.IsNullOrWhiteSpace(searchTerm))
                        dataCommand.Parameters.Add(new SqlParameter("@SearchTerm", $"%{searchTerm}%"));

                    using var reader = await dataCommand.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        items.Add(new RecentPaymentDto
                        {
                            Id = reader.GetInt32(0),
                            PaymentDateUtc = reader.GetDateTime(1),
                            InvoiceNumber = reader.GetString(2),
                            CustomerName = reader.GetString(3),
                            PaymentMethodName = reader.GetString(4),
                            Amount = reader.GetDecimal(5),
                            IsFullPayment = reader.GetBoolean(6),
                            IsVoided = reader.GetBoolean(7)
                        });
                    }
                }

                return (items, totalCount);
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
    /// Gets all child allocations for a parent payment.
    /// </summary>
    public virtual async Task<List<Payment>> GetChildAllocationsAsync(int parentPaymentId, int businessId)
    {
        try
        {
            string query = $@"
                SELECT {PaymentColumns}
                FROM [revenue].[Payment]
                WHERE [revenue].[Payment].[ParentPaymentId] = @ParentPaymentId
                  AND [revenue].[Payment].[BusinessId] = @BusinessId";

            return await ExecuteStoredProcedure(query,
                new SqlParameter("@ParentPaymentId", parentPaymentId),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Bulk-voids all non-voided children of a parent payment.
    /// </summary>
    public virtual async Task<int> VoidChildrenAsync(int parentPaymentId, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [revenue].[Payment]
                SET [IsVoided] = 1
                WHERE [revenue].[Payment].[ParentPaymentId] = @ParentPaymentId
                  AND [revenue].[Payment].[BusinessId] = @BusinessId
                  AND [revenue].[Payment].[IsVoided] = 0";

            return await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@ParentPaymentId", parentPaymentId),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets outstanding invoices for a customer in FIFO order (InvoiceDate ASC, Id ASC).
    /// </summary>
    public virtual async Task<List<OutstandingInvoiceDto>> GetOutstandingInvoicesForCustomerAsync(int customerId, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [invoice].[Invoice].[Id],
                       [invoice].[Invoice].[InvoiceNumber],
                       [invoice].[Invoice].[InvoiceDate],
                       [invoice].[Invoice].[TotalAmount],
                       [invoice].[Invoice].[TotalAmount] - ISNULL(
                           (SELECT SUM([revenue].[Payment].[Amount])
                            FROM [revenue].[Payment]
                            WHERE [revenue].[Payment].[InvoiceId] = [invoice].[Invoice].[Id]
                              AND [revenue].[Payment].[IsVoided] = 0
                              AND [revenue].[Payment].[BusinessId] = @BusinessId), 0
                       ) - ISNULL(
                           (SELECT SUM([credit].[CreditNoteApplication].[AmountApplied])
                            FROM [credit].[CreditNoteApplication]
                            WHERE [credit].[CreditNoteApplication].[InvoiceId] = [invoice].[Invoice].[Id]
                              AND [credit].[CreditNoteApplication].[IsVoided] = 0), 0
                       ) AS [OutstandingBalance]
                FROM [invoice].[Invoice]
                WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
                  AND [invoice].[Invoice].[CustomerId] = @CustomerId
                  AND [invoice].[Invoice].[InvoiceStatusTypeId] = 2
                  AND [invoice].[Invoice].[IsDeleted] = 0
                  AND ([invoice].[Invoice].[TotalAmount] - ISNULL(
                           (SELECT SUM([revenue].[Payment].[Amount])
                            FROM [revenue].[Payment]
                            WHERE [revenue].[Payment].[InvoiceId] = [invoice].[Invoice].[Id]
                              AND [revenue].[Payment].[IsVoided] = 0
                              AND [revenue].[Payment].[BusinessId] = @BusinessId), 0
                       ) - ISNULL(
                           (SELECT SUM([credit].[CreditNoteApplication].[AmountApplied])
                            FROM [credit].[CreditNoteApplication]
                            WHERE [credit].[CreditNoteApplication].[InvoiceId] = [invoice].[Invoice].[Id]
                              AND [credit].[CreditNoteApplication].[IsVoided] = 0), 0
                       )) > 0
                ORDER BY [invoice].[Invoice].[InvoiceDate] ASC, [invoice].[Invoice].[Id] ASC";

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

                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@CustomerId", customerId));

                var results = new List<OutstandingInvoiceDto>();

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new OutstandingInvoiceDto
                    {
                        InvoiceId = reader.GetInt32(0),
                        InvoiceNumber = reader.GetString(1),
                        InvoiceDate = reader.GetDateTime(2),
                        TotalAmount = reader.GetDecimal(3),
                        OutstandingBalance = reader.GetDecimal(4)
                    });
                }

                return results;
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
    /// Updates the CreditAmount on a parent payment.
    /// </summary>
    public virtual async Task UpdateCreditAmountAsync(int paymentId, decimal creditAmount)
    {
        try
        {
            const string query = @"
                UPDATE [revenue].[Payment]
                SET [CreditAmount] = @CreditAmount
                WHERE [revenue].[Payment].[Id] = @PaymentId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@PaymentId", paymentId),
                new SqlParameter("@CreditAmount", creditAmount));
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
