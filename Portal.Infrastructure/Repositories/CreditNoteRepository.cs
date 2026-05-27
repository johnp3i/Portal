using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for CreditNote entity CRUD operations against the [credit].[CreditNote] table.
/// </summary>
public class CreditNoteRepository : GenericStoredProcedureRepository<CreditNote>
{
    public CreditNoteRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Inserts a new credit note record and returns the new CreditNote.Id via OUTPUT INSERTED.Id.
    /// </summary>
    public virtual async Task<int> InsertAsync(CreditNote entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [credit].[CreditNote]
                    ([BusinessId], [InvoiceId], [CustomerId], [CreditNoteStatusTypeId],
                     [VatSubmissionPeriodId], [CreditNoteNumber], [IssueDate], [Reason],
                     [Subtotal], [TaxAmount], [TotalAmount], [IssuedAtUtc], [VoidedAtUtc],
                     [CreatedByUserId], [CreatedAtUtc])
                OUTPUT INSERTED.Id
                VALUES
                    (@BusinessId, @InvoiceId, @CustomerId, @CreditNoteStatusTypeId,
                     @VatSubmissionPeriodId, @CreditNoteNumber, @IssueDate, @Reason,
                     @Subtotal, @TaxAmount, @TotalAmount, @IssuedAtUtc, @VoidedAtUtc,
                     @CreatedByUserId, @CreatedAtUtc)";

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
                command.Parameters.Add(new SqlParameter("@CustomerId", entity.CustomerId));
                command.Parameters.Add(new SqlParameter("@CreditNoteStatusTypeId", entity.CreditNoteStatusTypeId));
                command.Parameters.Add(new SqlParameter("@VatSubmissionPeriodId", entity.VatSubmissionPeriodId));
                command.Parameters.Add(new SqlParameter("@CreditNoteNumber", entity.CreditNoteNumber));
                command.Parameters.Add(new SqlParameter("@IssueDate", entity.IssueDate.ToDateTime(TimeOnly.MinValue)));
                command.Parameters.Add(new SqlParameter("@Reason", entity.Reason));
                command.Parameters.Add(new SqlParameter("@Subtotal", entity.Subtotal));
                command.Parameters.Add(new SqlParameter("@TaxAmount", entity.TaxAmount));
                command.Parameters.Add(new SqlParameter("@TotalAmount", entity.TotalAmount));
                command.Parameters.Add(new SqlParameter("@IssuedAtUtc", entity.IssuedAtUtc ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@VoidedAtUtc", entity.VoidedAtUtc ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@CreatedByUserId", entity.CreatedByUserId ?? (object)DBNull.Value));
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
    /// Gets a single credit note by Id and BusinessId for tenant isolation.
    /// </summary>
    public virtual async Task<CreditNote?> GetByIdAndBusinessIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [credit].[CreditNote].[Id],
                       [credit].[CreditNote].[BusinessId],
                       [credit].[CreditNote].[InvoiceId],
                       [credit].[CreditNote].[CustomerId],
                       [credit].[CreditNote].[CreditNoteStatusTypeId],
                       [credit].[CreditNote].[VatSubmissionPeriodId],
                       [credit].[CreditNote].[CreditNoteNumber],
                       [credit].[CreditNote].[IssueDate],
                       [credit].[CreditNote].[Reason],
                       [credit].[CreditNote].[Subtotal],
                       [credit].[CreditNote].[TaxAmount],
                       [credit].[CreditNote].[TotalAmount],
                       [credit].[CreditNote].[IssuedAtUtc],
                       [credit].[CreditNote].[VoidedAtUtc],
                       [credit].[CreditNote].[CreatedByUserId],
                       [credit].[CreditNote].[CreatedAtUtc]
                FROM [credit].[CreditNote]
                WHERE [credit].[CreditNote].[Id] = @Id
                  AND [credit].[CreditNote].[BusinessId] = @BusinessId";

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
    /// Updates the status of a credit note, optionally setting IssuedAtUtc or VoidedAtUtc timestamps.
    /// </summary>
    public virtual async Task UpdateStatusAsync(int id, int newStatusId, DateTime? issuedAtUtc, DateTime? voidedAtUtc)
    {
        try
        {
            const string query = @"
                UPDATE [credit].[CreditNote]
                SET [CreditNoteStatusTypeId] = @NewStatusId,
                    [IssuedAtUtc] = @IssuedAtUtc,
                    [VoidedAtUtc] = @VoidedAtUtc
                WHERE [credit].[CreditNote].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@NewStatusId", newStatusId),
                new SqlParameter("@IssuedAtUtc", issuedAtUtc ?? (object)DBNull.Value),
                new SqlParameter("@VoidedAtUtc", voidedAtUtc ?? (object)DBNull.Value));
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets the highest sequential number from credit note numbers for a given business and year.
    /// Parses the NNNN portion from the CN-YYYY-NNNN format.
    /// Returns null if no credit notes exist for the given business and year.
    /// </summary>
    public virtual async Task<int?> GetHighestNumberForYearAsync(int businessId, int year)
    {
        try
        {
            const string query = @"
                SELECT MAX(TRY_CAST(RIGHT([credit].[CreditNote].[CreditNoteNumber], 4) AS INT))
                FROM [credit].[CreditNote]
                WHERE [credit].[CreditNote].[BusinessId] = @BusinessId
                  AND [credit].[CreditNote].[CreditNoteNumber] LIKE @Pattern";

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
                command.Parameters.Add(new SqlParameter("@Pattern", $"CN-{year}-%"));

                var result = await command.ExecuteScalarAsync();
                return result != null && result != DBNull.Value ? (int)result : null;
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
    /// Gets a paginated list of credit notes with filters for the list view.
    /// Supports filtering by status, customer, date range, and search term (credit note number or customer name).
    /// </summary>
    public virtual async Task<(List<CreditNoteListDto> Items, int TotalCount)> GetPagedAsync(
        int businessId, int? statusFilter, int? customerFilter, DateOnly? fromDate, DateOnly? toDate,
        string? searchTerm, int offset, int pageSize)
    {
        try
        {
            const string query = @"
                SELECT [credit].[CreditNote].[Id],
                       [credit].[CreditNote].[CreditNoteNumber],
                       [customer].[Customer].[Name] AS [CustomerName],
                       [invoice].[Invoice].[InvoiceNumber],
                       [credit].[CreditNote].[IssueDate],
                       [credit].[CreditNote].[TotalAmount],
                       [credit].[CreditNote].[CreditNoteStatusTypeId],
                       [credit].[CreditNoteStatusType].[Name] AS [StatusName],
                       [credit].[CreditNote].[Reason],
                       COUNT(*) OVER() AS [TotalCount]
                FROM [credit].[CreditNote]
                INNER JOIN [customer].[Customer] ON [credit].[CreditNote].[CustomerId] = [customer].[Customer].[Id]
                INNER JOIN [invoice].[Invoice] ON [credit].[CreditNote].[InvoiceId] = [invoice].[Invoice].[Id]
                INNER JOIN [credit].[CreditNoteStatusType] ON [credit].[CreditNote].[CreditNoteStatusTypeId] = [credit].[CreditNoteStatusType].[Id]
                WHERE [credit].[CreditNote].[BusinessId] = @BusinessId
                  AND (@StatusFilter IS NULL OR [credit].[CreditNote].[CreditNoteStatusTypeId] = @StatusFilter)
                  AND (@CustomerFilter IS NULL OR [credit].[CreditNote].[CustomerId] = @CustomerFilter)
                  AND (@FromDate IS NULL OR [credit].[CreditNote].[IssueDate] >= @FromDate)
                  AND (@ToDate IS NULL OR [credit].[CreditNote].[IssueDate] <= @ToDate)
                  AND (@SearchTerm IS NULL OR (
                      [credit].[CreditNote].[CreditNoteNumber] LIKE '%' + @SearchTerm + '%'
                      OR [customer].[Customer].[Name] LIKE '%' + @SearchTerm + '%'
                  ))
                ORDER BY [credit].[CreditNote].[IssueDate] DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var results = new List<CreditNoteListDto>();
            int totalCount = 0;
            var connection = _context.Database.GetDbConnection();

            // Escape SQL wildcards in search term
            string? escapedSearchTerm = searchTerm != null
                ? searchTerm.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]")
                : null;

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
                command.Parameters.Add(new SqlParameter("@StatusFilter", statusFilter ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@CustomerFilter", customerFilter ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@FromDate", fromDate.HasValue ? fromDate.Value.ToDateTime(TimeOnly.MinValue) : (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@ToDate", toDate.HasValue ? toDate.Value.ToDateTime(TimeOnly.MinValue) : (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@SearchTerm", (object?)escapedSearchTerm ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Offset", offset));
                command.Parameters.Add(new SqlParameter("@PageSize", pageSize));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    totalCount = reader.GetInt32(reader.GetOrdinal("TotalCount"));
                    results.Add(new CreditNoteListDto
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                        CreditNoteNumber = reader.GetString(reader.GetOrdinal("CreditNoteNumber")),
                        CustomerName = reader.GetString(reader.GetOrdinal("CustomerName")),
                        InvoiceNumber = reader.GetString(reader.GetOrdinal("InvoiceNumber")),
                        IssueDate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("IssueDate"))),
                        TotalAmount = reader.GetDecimal(reader.GetOrdinal("TotalAmount")),
                        CreditNoteStatusTypeId = reader.GetInt32(reader.GetOrdinal("CreditNoteStatusTypeId")),
                        StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
                        Reason = reader.GetString(reader.GetOrdinal("Reason"))
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
    /// Gets KPI data for credit note summary cards.
    /// Returns: count of Issued/Applied credit notes this month, sum of their TotalAmount, and count of Issued (pending) credit notes.
    /// </summary>
    public virtual async Task<CreditNoteKpiDto> GetKpiDataAsync(int businessId, DateTime monthStart)
    {
        try
        {
            const string query = @"
                SELECT
                    (SELECT COUNT(*)
                     FROM [credit].[CreditNote]
                     WHERE [credit].[CreditNote].[BusinessId] = @BusinessId
                       AND [credit].[CreditNote].[CreditNoteStatusTypeId] IN (2, 3)
                       AND [credit].[CreditNote].[CreatedAtUtc] >= @MonthStart) AS [TotalIssuedCount],
                    (SELECT ISNULL(SUM([credit].[CreditNote].[TotalAmount]), 0)
                     FROM [credit].[CreditNote]
                     WHERE [credit].[CreditNote].[BusinessId] = @BusinessId
                       AND [credit].[CreditNote].[CreditNoteStatusTypeId] IN (2, 3)
                       AND [credit].[CreditNote].[CreatedAtUtc] >= @MonthStart) AS [TotalValue],
                    (SELECT COUNT(*)
                     FROM [credit].[CreditNote]
                     WHERE [credit].[CreditNote].[BusinessId] = @BusinessId
                       AND [credit].[CreditNote].[CreditNoteStatusTypeId] = 2) AS [PendingApplicationCount]";

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
                command.Parameters.Add(new SqlParameter("@MonthStart", monthStart));

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new CreditNoteKpiDto
                    {
                        TotalIssuedCount = reader.GetInt32(reader.GetOrdinal("TotalIssuedCount")),
                        TotalValue = reader.GetDecimal(reader.GetOrdinal("TotalValue")),
                        PendingApplicationCount = reader.GetInt32(reader.GetOrdinal("PendingApplicationCount"))
                    };
                }

                return new CreditNoteKpiDto();
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
    /// Gets the total applied credit amount for an invoice (sum of TotalAmount from non-voided Applied credit notes).
    /// Returns 0 when no applied credit notes exist.
    /// </summary>
    public virtual async Task<decimal> GetTotalAppliedCreditAsync(int invoiceId, int businessId)
    {
        try
        {
            const string query = @"
                SELECT ISNULL(SUM([credit].[CreditNoteApplication].[AmountApplied]), 0)
                FROM [credit].[CreditNoteApplication]
                INNER JOIN [credit].[CreditNote] ON [credit].[CreditNoteApplication].[CreditNoteId] = [credit].[CreditNote].[Id]
                WHERE [credit].[CreditNoteApplication].[InvoiceId] = @InvoiceId
                  AND [credit].[CreditNote].[BusinessId] = @BusinessId
                  AND [credit].[CreditNoteApplication].[IsVoided] = 0";

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
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates an existing credit note's editable fields (used during Draft editing).
    /// </summary>
    public virtual async Task UpdateAsync(CreditNote entity)
    {
        try
        {
            const string query = @"
                UPDATE [credit].[CreditNote]
                SET [IssueDate] = @IssueDate,
                    [Reason] = @Reason,
                    [VatSubmissionPeriodId] = @VatSubmissionPeriodId,
                    [Subtotal] = @Subtotal,
                    [TaxAmount] = @TaxAmount,
                    [TotalAmount] = @TotalAmount
                WHERE [credit].[CreditNote].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@IssueDate", entity.IssueDate.ToDateTime(TimeOnly.MinValue)),
                new SqlParameter("@Reason", entity.Reason),
                new SqlParameter("@VatSubmissionPeriodId", entity.VatSubmissionPeriodId),
                new SqlParameter("@Subtotal", entity.Subtotal),
                new SqlParameter("@TaxAmount", entity.TaxAmount),
                new SqlParameter("@TotalAmount", entity.TotalAmount));
        }
        catch (Exception)
        {
            throw;
        }
    }
}
