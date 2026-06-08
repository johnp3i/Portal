using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities.Billing;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for BillingInvoice entity CRUD operations against the [billing].[Invoice] table.
/// </summary>
public class BillingInvoiceRepository : GenericStoredProcedureRepository<BillingInvoice>
{
    public BillingInvoiceRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Gets a paginated list of billing invoices for a business, ordered by PaidAtUtc descending (most recent first).
    /// Returns items and total count for pagination metadata.
    /// </summary>
    public virtual async Task<(List<BillingInvoice> Items, int TotalCount)> GetByBusinessIdPagedAsync(int businessId, int page, int pageSize)
    {
        try
        {
            int offset = (page - 1) * pageSize;

            const string query = @"
                SELECT [billing].[Invoice].[Id],
                       [billing].[Invoice].[BusinessId],
                       [billing].[Invoice].[StripeInvoiceId],
                       [billing].[Invoice].[AmountEur],
                       [billing].[Invoice].[PeriodStart],
                       [billing].[Invoice].[PeriodEnd],
                       [billing].[Invoice].[Status],
                       [billing].[Invoice].[PaidAtUtc],
                       [billing].[Invoice].[CreatedAtUtc],
                       COUNT(*) OVER() AS [TotalCount]
                FROM [billing].[Invoice]
                WHERE [billing].[Invoice].[BusinessId] = @BusinessId
                ORDER BY [billing].[Invoice].[PaidAtUtc] DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var results = new List<BillingInvoice>();
            int totalCount = 0;
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
                command.Parameters.Add(new SqlParameter("@Offset", offset));
                command.Parameters.Add(new SqlParameter("@PageSize", pageSize));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    totalCount = reader.GetInt32(reader.GetOrdinal("TotalCount"));
                    results.Add(new BillingInvoice
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                        BusinessId = reader.GetInt32(reader.GetOrdinal("BusinessId")),
                        StripeInvoiceId = reader.IsDBNull(reader.GetOrdinal("StripeInvoiceId")) ? null : reader.GetString(reader.GetOrdinal("StripeInvoiceId")),
                        AmountEur = reader.GetDecimal(reader.GetOrdinal("AmountEur")),
                        PeriodStart = reader.GetDateTime(reader.GetOrdinal("PeriodStart")),
                        PeriodEnd = reader.GetDateTime(reader.GetOrdinal("PeriodEnd")),
                        Status = reader.GetString(reader.GetOrdinal("Status")),
                        PaidAtUtc = reader.IsDBNull(reader.GetOrdinal("PaidAtUtc")) ? null : reader.GetDateTime(reader.GetOrdinal("PaidAtUtc")),
                        CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"))
                    });
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            return (results, totalCount);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets a single billing invoice by Id and BusinessId for tenant isolation.
    /// </summary>
    public virtual async Task<BillingInvoice?> GetByIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [billing].[Invoice].[Id],
                       [billing].[Invoice].[BusinessId],
                       [billing].[Invoice].[StripeInvoiceId],
                       [billing].[Invoice].[AmountEur],
                       [billing].[Invoice].[PeriodStart],
                       [billing].[Invoice].[PeriodEnd],
                       [billing].[Invoice].[Status],
                       [billing].[Invoice].[PaidAtUtc],
                       [billing].[Invoice].[CreatedAtUtc]
                FROM [billing].[Invoice]
                WHERE [billing].[Invoice].[Id] = @Id
                  AND [billing].[Invoice].[BusinessId] = @BusinessId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Inserts a new billing invoice record and returns the new BillingInvoice.Id via OUTPUT INSERTED.Id.
    /// </summary>
    public virtual async Task<int> InsertAsync(BillingInvoice entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [billing].[Invoice]
                    ([BusinessId], [StripeInvoiceId], [AmountEur], [PeriodStart],
                     [PeriodEnd], [Status], [PaidAtUtc], [InvoiceNumber], [CreatedAtUtc])
                OUTPUT INSERTED.Id
                VALUES
                    (@BusinessId, @StripeInvoiceId, @AmountEur, @PeriodStart,
                     @PeriodEnd, @Status, @PaidAtUtc, @InvoiceNumber, @CreatedAtUtc)";

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
                command.Parameters.Add(new SqlParameter("@StripeInvoiceId", entity.StripeInvoiceId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@AmountEur", entity.AmountEur));
                command.Parameters.Add(new SqlParameter("@PeriodStart", entity.PeriodStart));
                command.Parameters.Add(new SqlParameter("@PeriodEnd", entity.PeriodEnd));
                command.Parameters.Add(new SqlParameter("@Status", entity.Status));
                command.Parameters.Add(new SqlParameter("@PaidAtUtc", entity.PaidAtUtc ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@InvoiceNumber", entity.InvoiceNumber ?? (object)DBNull.Value));
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
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets the total count of billing invoices for a business.
    /// </summary>
    public virtual async Task<int> GetCountByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT COUNT(*)
                FROM [billing].[Invoice]
                WHERE [billing].[Invoice].[BusinessId] = @BusinessId";

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

                var result = await command.ExecuteScalarAsync();
                return (int)result!;
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
    /// Gets a billing summary for a business: total paid amount, invoice count, and last payment date.
    /// </summary>
    public virtual async Task<BillingSummaryDto> GetSummaryByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT ISNULL(SUM(CASE WHEN [billing].[Invoice].[Status] = 'paid' THEN [billing].[Invoice].[AmountEur] ELSE 0 END), 0) AS [TotalPaid],
                       COUNT(*) AS [InvoiceCount],
                       MAX([billing].[Invoice].[PaidAtUtc]) AS [LastPaymentDate]
                FROM [billing].[Invoice]
                WHERE [billing].[Invoice].[BusinessId] = @BusinessId";

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

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new BillingSummaryDto
                    {
                        TotalPaid = reader.GetDecimal(reader.GetOrdinal("TotalPaid")),
                        InvoiceCount = reader.GetInt32(reader.GetOrdinal("InvoiceCount")),
                        LastPaymentDate = reader.IsDBNull(reader.GetOrdinal("LastPaymentDate")) ? null : reader.GetDateTime(reader.GetOrdinal("LastPaymentDate"))
                    };
                }

                return new BillingSummaryDto
                {
                    TotalPaid = 0,
                    InvoiceCount = 0,
                    LastPaymentDate = null
                };
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
}
