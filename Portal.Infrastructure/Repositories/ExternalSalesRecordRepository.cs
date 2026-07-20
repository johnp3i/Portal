using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for ExternalSalesRecord CRUD operations against the [revenue].[ExternalSalesRecord] table.
/// </summary>
public class ExternalSalesRecordRepository : GenericStoredProcedureRepository<ExternalSalesRecord>
{
    public ExternalSalesRecordRepository(PortalDbContext context) : base(context) { }

    /// <summary>
    /// Gets paged records with optional filters.
    /// </summary>
    public async Task<(List<ExternalSalesRecord> Items, int TotalCount)> GetPagedAsync(
        int businessId, int? revenueSourceId, DateOnly? dateFrom, DateOnly? dateTo,
        int? customerId, int offset, int pageSize, bool includeInactive = false)
    {
        try
        {
            var activeFilter = includeInactive ? "" : "AND ExternalSalesRecord.IsActive = 1";

            string countQuery = $@"
                SELECT COUNT(1)
                FROM [revenue].[ExternalSalesRecord]
                WHERE ExternalSalesRecord.BusinessId = @BusinessId
                  {activeFilter}
                  AND (@RevenueSourceId IS NULL OR ExternalSalesRecord.RevenueSourceId = @RevenueSourceId)
                  AND (@DateFrom IS NULL OR ExternalSalesRecord.TransactionDate >= @DateFrom)
                  AND (@DateTo IS NULL OR ExternalSalesRecord.TransactionDate <= @DateTo)
                  AND (@CustomerId IS NULL OR ExternalSalesRecord.CustomerId = @CustomerId)";

            string dataQuery = $@"
                SELECT [Id], [BusinessId], [RevenueSourceId], [TransactionDate], [InvoiceNumber],
                       [CustomerId], [NetAmount], [VatAmount], [TotalAmount], [Description],
                       [PaymentMethod], [ImportSessionId], [VatSubmissionPeriodId], [IsActive], [CreatedAtUtc]
                FROM [revenue].[ExternalSalesRecord]
                WHERE ExternalSalesRecord.BusinessId = @BusinessId
                  {activeFilter}
                  AND (@RevenueSourceId IS NULL OR ExternalSalesRecord.RevenueSourceId = @RevenueSourceId)
                  AND (@DateFrom IS NULL OR ExternalSalesRecord.TransactionDate >= @DateFrom)
                  AND (@DateTo IS NULL OR ExternalSalesRecord.TransactionDate <= @DateTo)
                  AND (@CustomerId IS NULL OR ExternalSalesRecord.CustomerId = @CustomerId)
                ORDER BY ExternalSalesRecord.TransactionDate DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var connection = _context.Database.GetDbConnection();
            int totalCount;

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using (var countCmd = connection.CreateCommand())
                {
                    countCmd.CommandText = countQuery;
                    var transaction = _context.Database.CurrentTransaction;
                    if (transaction != null) countCmd.Transaction = transaction.GetDbTransaction();

                    countCmd.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                    countCmd.Parameters.Add(new SqlParameter("@RevenueSourceId", revenueSourceId ?? (object)DBNull.Value));
                    countCmd.Parameters.Add(new SqlParameter("@DateFrom", dateFrom.HasValue ? dateFrom.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value));
                    countCmd.Parameters.Add(new SqlParameter("@DateTo", dateTo.HasValue ? dateTo.Value.ToDateTime(TimeOnly.MaxValue) : DBNull.Value));
                    countCmd.Parameters.Add(new SqlParameter("@CustomerId", customerId ?? (object)DBNull.Value));

                    var result = await countCmd.ExecuteScalarAsync();
                    totalCount = Convert.ToInt32(result);
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            var parameters = new[]
            {
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@Offset", offset),
                new SqlParameter("@PageSize", pageSize),
                new SqlParameter("@RevenueSourceId", revenueSourceId ?? (object)DBNull.Value),
                new SqlParameter("@DateFrom", dateFrom.HasValue ? dateFrom.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value),
                new SqlParameter("@DateTo", dateTo.HasValue ? dateTo.Value.ToDateTime(TimeOnly.MaxValue) : DBNull.Value),
                new SqlParameter("@CustomerId", customerId ?? (object)DBNull.Value)
            };

            var items = await ExecuteStoredProcedure(dataQuery, parameters);
            return (items, totalCount);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Inserts a single record and returns the new Id.
    /// </summary>
    public async Task<int> InsertAsync(ExternalSalesRecord entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [revenue].[ExternalSalesRecord]
                    ([BusinessId], [RevenueSourceId], [TransactionDate], [InvoiceNumber],
                     [CustomerId], [NetAmount], [VatAmount], [TotalAmount], [Description],
                     [PaymentMethod], [ImportSessionId], [VatSubmissionPeriodId], [IsActive], [CreatedAtUtc])
                OUTPUT INSERTED.Id
                VALUES
                    (@BusinessId, @RevenueSourceId, @TransactionDate, @InvoiceNumber,
                     @CustomerId, @NetAmount, @VatAmount, @TotalAmount, @Description,
                     @PaymentMethod, @ImportSessionId, @VatSubmissionPeriodId, @IsActive, @CreatedAtUtc)";

            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;
                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null) command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@BusinessId", entity.BusinessId));
                command.Parameters.Add(new SqlParameter("@RevenueSourceId", entity.RevenueSourceId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@TransactionDate", entity.TransactionDate.ToDateTime(TimeOnly.MinValue)));
                command.Parameters.Add(new SqlParameter("@InvoiceNumber", entity.InvoiceNumber ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@CustomerId", entity.CustomerId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@NetAmount", entity.NetAmount));
                command.Parameters.Add(new SqlParameter("@VatAmount", entity.VatAmount));
                command.Parameters.Add(new SqlParameter("@TotalAmount", entity.TotalAmount));
                command.Parameters.Add(new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@PaymentMethod", entity.PaymentMethod ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@ImportSessionId", entity.ImportSessionId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@VatSubmissionPeriodId", entity.VatSubmissionPeriodId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@IsActive", entity.IsActive));
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
    /// Soft-deletes a record.
    /// </summary>
    public async Task SoftDeleteAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [revenue].[ExternalSalesRecord]
                SET [IsActive] = 0
                WHERE ExternalSalesRecord.Id = @Id AND ExternalSalesRecord.BusinessId = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Restores a soft-deleted record.
    /// </summary>
    public async Task RestoreAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [revenue].[ExternalSalesRecord]
                SET [IsActive] = 1
                WHERE ExternalSalesRecord.Id = @Id AND ExternalSalesRecord.BusinessId = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Checks for duplicate by InvoiceNumber + RevenueSourceId + TransactionDate.
    /// </summary>
    public async Task<bool> ExistsDuplicateAsync(int businessId, int? revenueSourceId, string invoiceNumber, DateOnly transactionDate)
    {
        try
        {
            const string query = @"
                SELECT COUNT(1)
                FROM [revenue].[ExternalSalesRecord]
                WHERE ExternalSalesRecord.BusinessId = @BusinessId
                  AND ExternalSalesRecord.IsActive = 1
                  AND ExternalSalesRecord.InvoiceNumber = @InvoiceNumber
                  AND ExternalSalesRecord.TransactionDate = @TransactionDate
                  AND (@RevenueSourceId IS NULL OR ExternalSalesRecord.RevenueSourceId = @RevenueSourceId)";

            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;
                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null) command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@RevenueSourceId", revenueSourceId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@InvoiceNumber", invoiceNumber));
                command.Parameters.Add(new SqlParameter("@TransactionDate", transactionDate.ToDateTime(TimeOnly.MinValue)));

                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result) > 0;
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
    /// Checks for cross-source duplicate: same InvoiceNumber + TransactionDate exists under ANY source for the business.
    /// Returns the source name if found, null otherwise.
    /// </summary>
    public async Task<string?> FindCrossSourceDuplicateAsync(int businessId, int? excludeSourceId, string invoiceNumber, DateOnly transactionDate)
    {
        try
        {
            const string query = @"
                SELECT TOP 1 RevenueSource.[Name]
                FROM [revenue].[ExternalSalesRecord]
                LEFT JOIN [revenue].[RevenueSource] ON ExternalSalesRecord.RevenueSourceId = RevenueSource.Id
                WHERE ExternalSalesRecord.BusinessId = @BusinessId
                  AND ExternalSalesRecord.IsActive = 1
                  AND ExternalSalesRecord.InvoiceNumber = @InvoiceNumber
                  AND ExternalSalesRecord.TransactionDate = @TransactionDate
                  AND (@ExcludeSourceId IS NULL OR ExternalSalesRecord.RevenueSourceId != @ExcludeSourceId)";

            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;
                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null) command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@ExcludeSourceId", excludeSourceId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@InvoiceNumber", invoiceNumber));
                command.Parameters.Add(new SqlParameter("@TransactionDate", transactionDate.ToDateTime(TimeOnly.MinValue)));

                var result = await command.ExecuteScalarAsync();
                return result as string;
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
}
