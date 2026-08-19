using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for Invoice entity CRUD operations against the [invoice].[Invoice] table.
/// </summary>
public class InvoiceRepository : GenericStoredProcedureRepository<Invoice>
{
    public InvoiceRepository(DbContext context) : base(context) { }

    public async Task<List<InvoiceListDto>> GetAllByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [invoice].[Invoice].[Id],
                       [invoice].[Invoice].[InvoiceNumber],
                       [invoice].[Invoice].[CustomerId],
                       [customer].[Customer].[Name] AS [CustomerName],
                       [invoice].[Invoice].[InvoiceDate],
                       [invoice].[Invoice].[DueDate],
                       [invoice].[Invoice].[TotalAmount],
                       [invoice].[InvoiceStatusType].[Name] AS [StatusName],
                       [invoice].[InvoiceFinancialStatusType].[Name] AS [FinancialStatusName],
                       [invoice].[Invoice].[InvoiceStatusTypeId],
                       [invoice].[Invoice].[InvoiceFinancialStatusTypeId]
                FROM [invoice].[Invoice]
                INNER JOIN [customer].[Customer] ON [invoice].[Invoice].[CustomerId] = [customer].[Customer].[Id]
                INNER JOIN [invoice].[InvoiceStatusType] ON [invoice].[Invoice].[InvoiceStatusTypeId] = [invoice].[InvoiceStatusType].[Id]
                INNER JOIN [invoice].[InvoiceFinancialStatusType] ON [invoice].[Invoice].[InvoiceFinancialStatusTypeId] = [invoice].[InvoiceFinancialStatusType].[Id]
                WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
                  AND [invoice].[Invoice].[IsDeleted] = 0
                ORDER BY [invoice].[Invoice].[InvoiceDate] DESC, [invoice].[Invoice].[Id] DESC";

            var results = new List<InvoiceListDto>();
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new InvoiceListDto
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                        InvoiceNumber = reader.GetString(reader.GetOrdinal("InvoiceNumber")),
                        CustomerId = reader.GetInt32(reader.GetOrdinal("CustomerId")),
                        CustomerName = reader.GetString(reader.GetOrdinal("CustomerName")),
                        InvoiceDate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("InvoiceDate"))),
                        DueDate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("DueDate"))),
                        TotalAmount = reader.GetDecimal(reader.GetOrdinal("TotalAmount")),
                        StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
                        FinancialStatusName = reader.GetString(reader.GetOrdinal("FinancialStatusName")),
                        InvoiceStatusTypeId = reader.GetInt32(reader.GetOrdinal("InvoiceStatusTypeId")),
                        InvoiceFinancialStatusTypeId = reader.GetInt32(reader.GetOrdinal("InvoiceFinancialStatusTypeId"))
                    });
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }

            return results;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task<Invoice?> GetByIdAndBusinessIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [invoice].[Invoice].[Id], [invoice].[Invoice].[BusinessId], [invoice].[Invoice].[CustomerId],
                       [invoice].[Invoice].[QuotationId], [invoice].[Invoice].[InvoiceStatusTypeId],
                       [invoice].[Invoice].[InvoiceFinancialStatusTypeId], [invoice].[Invoice].[InvoiceNumber],
                       [invoice].[Invoice].[InvoiceDate], [invoice].[Invoice].[DueDate],
                       [invoice].[Invoice].[Subtotal], [invoice].[Invoice].[TaxAmount],
                       [invoice].[Invoice].[TotalAmount], [invoice].[Invoice].[CurrencyCode],
                       [invoice].[Invoice].[Notes], [invoice].[Invoice].[IsGrandTotalShown],
                       [invoice].[Invoice].[IsQuotationReferenceShown],
                       [invoice].[Invoice].[VatSubmissionPeriodId],
                       [invoice].[Invoice].[IsDisputed],
                       [invoice].[Invoice].[PaymentInstructionsOverride],
                       [invoice].[Invoice].[CreatedAtUtc], [invoice].[Invoice].[UpdatedAtUtc],
                       [invoice].[Invoice].[IsDeleted], [invoice].[Invoice].[DeletedAtUtc],
                       [invoice].[Invoice].[LeadRequestId]
                FROM [invoice].[Invoice]
                WHERE [invoice].[Invoice].[Id] = @Id AND [invoice].[Invoice].[BusinessId] = @BusinessId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task<Invoice?> GetByIdAndBusinessIdUnfilteredAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [invoice].[Invoice].[Id], [invoice].[Invoice].[BusinessId], [invoice].[Invoice].[CustomerId],
                       [invoice].[Invoice].[QuotationId], [invoice].[Invoice].[InvoiceStatusTypeId],
                       [invoice].[Invoice].[InvoiceFinancialStatusTypeId], [invoice].[Invoice].[InvoiceNumber],
                       [invoice].[Invoice].[InvoiceDate], [invoice].[Invoice].[DueDate],
                       [invoice].[Invoice].[Subtotal], [invoice].[Invoice].[TaxAmount],
                       [invoice].[Invoice].[TotalAmount], [invoice].[Invoice].[CurrencyCode],
                       [invoice].[Invoice].[Notes], [invoice].[Invoice].[IsGrandTotalShown],
                       [invoice].[Invoice].[IsQuotationReferenceShown],
                       [invoice].[Invoice].[VatSubmissionPeriodId],
                       [invoice].[Invoice].[IsDisputed],
                       [invoice].[Invoice].[PaymentInstructionsOverride],
                       [invoice].[Invoice].[CreatedAtUtc], [invoice].[Invoice].[UpdatedAtUtc],
                       [invoice].[Invoice].[IsDeleted], [invoice].[Invoice].[DeletedAtUtc],
                       [invoice].[Invoice].[LeadRequestId]
                FROM [invoice].[Invoice]
                WHERE [invoice].[Invoice].[Id] = @Id AND [invoice].[Invoice].[BusinessId] = @BusinessId";

            return await ExecuteSingleRecordStoredProcedureUnfiltered(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task<int> InsertAsync(Invoice entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [invoice].[Invoice]
                    ([BusinessId], [CustomerId], [QuotationId], [InvoiceStatusTypeId],
                     [InvoiceFinancialStatusTypeId], [InvoiceNumber], [InvoiceDate], [DueDate],
                     [Subtotal], [TaxAmount], [TotalAmount], [CurrencyCode], [Notes],
                     [IsGrandTotalShown], [IsQuotationReferenceShown], [VatSubmissionPeriodId],
                     [CreatedAtUtc], [UpdatedAtUtc])
                OUTPUT INSERTED.Id
                VALUES
                    (@BusinessId, @CustomerId, @QuotationId, @InvoiceStatusTypeId,
                     @InvoiceFinancialStatusTypeId, @InvoiceNumber, @InvoiceDate, @DueDate,
                     @Subtotal, @TaxAmount, @TotalAmount, @CurrencyCode, @Notes,
                     @IsGrandTotalShown, @IsQuotationReferenceShown, @VatSubmissionPeriodId,
                     @CreatedAtUtc, @UpdatedAtUtc)";

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
                command.Parameters.Add(new SqlParameter("@CustomerId", entity.CustomerId));
                command.Parameters.Add(new SqlParameter("@QuotationId", entity.QuotationId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@InvoiceStatusTypeId", entity.InvoiceStatusTypeId));
                command.Parameters.Add(new SqlParameter("@InvoiceFinancialStatusTypeId", entity.InvoiceFinancialStatusTypeId));
                command.Parameters.Add(new SqlParameter("@InvoiceNumber", entity.InvoiceNumber));
                command.Parameters.Add(new SqlParameter("@InvoiceDate", entity.InvoiceDate));
                command.Parameters.Add(new SqlParameter("@DueDate", entity.DueDate));
                command.Parameters.Add(new SqlParameter("@Subtotal", entity.Subtotal));
                command.Parameters.Add(new SqlParameter("@TaxAmount", entity.TaxAmount));
                command.Parameters.Add(new SqlParameter("@TotalAmount", entity.TotalAmount));
                command.Parameters.Add(new SqlParameter("@CurrencyCode", entity.CurrencyCode));
                command.Parameters.Add(new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@IsGrandTotalShown", entity.IsGrandTotalShown));
                command.Parameters.Add(new SqlParameter("@IsQuotationReferenceShown", entity.IsQuotationReferenceShown));
                command.Parameters.Add(new SqlParameter("@VatSubmissionPeriodId", (object?)entity.VatSubmissionPeriodId ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@CreatedAtUtc", entity.CreatedAtUtc));
                command.Parameters.Add(new SqlParameter("@UpdatedAtUtc", entity.UpdatedAtUtc));

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

    public virtual async Task UpdateAsync(Invoice entity)
    {
        try
        {
            const string query = @"
                UPDATE [invoice].[Invoice]
                SET
                    [CustomerId] = @CustomerId,
                    [InvoiceStatusTypeId] = @InvoiceStatusTypeId,
                    [InvoiceFinancialStatusTypeId] = @InvoiceFinancialStatusTypeId,
                    [InvoiceDate] = @InvoiceDate,
                    [DueDate] = @DueDate,
                    [Subtotal] = @Subtotal,
                    [TaxAmount] = @TaxAmount,
                    [TotalAmount] = @TotalAmount,
                    [Notes] = @Notes,
                    [IsGrandTotalShown] = @IsGrandTotalShown,
                    [IsQuotationReferenceShown] = @IsQuotationReferenceShown,
                    [UpdatedAtUtc] = @UpdatedAtUtc
                WHERE [Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@CustomerId", entity.CustomerId),
                new SqlParameter("@InvoiceStatusTypeId", entity.InvoiceStatusTypeId),
                new SqlParameter("@InvoiceFinancialStatusTypeId", entity.InvoiceFinancialStatusTypeId),
                new SqlParameter("@InvoiceDate", entity.InvoiceDate),
                new SqlParameter("@DueDate", entity.DueDate),
                new SqlParameter("@Subtotal", entity.Subtotal),
                new SqlParameter("@TaxAmount", entity.TaxAmount),
                new SqlParameter("@TotalAmount", entity.TotalAmount),
                new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value),
                new SqlParameter("@IsGrandTotalShown", entity.IsGrandTotalShown),
                new SqlParameter("@IsQuotationReferenceShown", entity.IsQuotationReferenceShown),
                new SqlParameter("@UpdatedAtUtc", entity.UpdatedAtUtc)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task<int> GetNextSequentialNumberAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT ISNULL(MAX(TRY_CAST(RIGHT([InvoiceNumber], 5) AS INT)), 0) + 1
                FROM [invoice].[Invoice]
                WHERE [BusinessId] = @BusinessId";

            var result = await _context.Database
                .SqlQueryRaw<int>(query, new SqlParameter("@BusinessId", businessId))
                .ToListAsync();

            return result.FirstOrDefault();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<Invoice?> GetByQuotationIdAsync(int quotationId)
    {
        try
        {
            const string query = @"
                SELECT [invoice].[Invoice].[Id], [invoice].[Invoice].[BusinessId], [invoice].[Invoice].[CustomerId],
                       [invoice].[Invoice].[QuotationId], [invoice].[Invoice].[InvoiceStatusTypeId],
                       [invoice].[Invoice].[InvoiceFinancialStatusTypeId], [invoice].[Invoice].[InvoiceNumber],
                       [invoice].[Invoice].[InvoiceDate], [invoice].[Invoice].[DueDate],
                       [invoice].[Invoice].[Subtotal], [invoice].[Invoice].[TaxAmount],
                       [invoice].[Invoice].[TotalAmount], [invoice].[Invoice].[CurrencyCode],
                       [invoice].[Invoice].[Notes], [invoice].[Invoice].[IsGrandTotalShown],
                       [invoice].[Invoice].[IsQuotationReferenceShown],
                       [invoice].[Invoice].[VatSubmissionPeriodId],
                       [invoice].[Invoice].[IsDisputed],
                       [invoice].[Invoice].[PaymentInstructionsOverride],
                       [invoice].[Invoice].[CreatedAtUtc], [invoice].[Invoice].[UpdatedAtUtc],
                       [invoice].[Invoice].[IsDeleted], [invoice].[Invoice].[DeletedAtUtc],
                       [invoice].[Invoice].[LeadRequestId]
                FROM [invoice].[Invoice]
                WHERE [invoice].[Invoice].[QuotationId] = @QuotationId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@QuotationId", quotationId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task UpdateVatPeriodAsync(int invoiceId, int? vatSubmissionPeriodId)
    {
        try
        {
            const string query = @"
                UPDATE [invoice].[Invoice]
                SET [VatSubmissionPeriodId] = @VatSubmissionPeriodId,
                    [UpdatedAtUtc] = @UpdatedAtUtc
                WHERE [Id] = @InvoiceId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@InvoiceId", invoiceId),
                new SqlParameter("@VatSubmissionPeriodId", vatSubmissionPeriodId.HasValue ? vatSubmissionPeriodId.Value : (object)DBNull.Value),
                new SqlParameter("@UpdatedAtUtc", DateTime.UtcNow)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task SoftDeleteAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [invoice].[Invoice]
                SET [invoice].[Invoice].[IsDeleted] = 1,
                    [invoice].[Invoice].[DeletedAtUtc] = GETUTCDATE(),
                    [invoice].[Invoice].[UpdatedAtUtc] = GETUTCDATE()
                WHERE [invoice].[Invoice].[Id] = @Id
                  AND [invoice].[Invoice].[BusinessId] = @BusinessId
                  AND [invoice].[Invoice].[IsDeleted] = 0";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(List<InvoiceListDto> Items, int TotalCount)> GetPagedByBusinessIdAsync(
        int businessId,
        int? statusFilter,
        int? financialStatusFilter,
        int? customerFilter,
        string? searchTerm,
        int offset,
        int pageSize,
        int? vatPeriodId = null)
    {
        try
        {
            const string query = @"
                SELECT [invoice].[Invoice].[Id],
                       [invoice].[Invoice].[InvoiceNumber],
                       [invoice].[Invoice].[CustomerId],
                       [customer].[Customer].[Name] AS [CustomerName],
                       [invoice].[Invoice].[InvoiceDate],
                       [invoice].[Invoice].[DueDate],
                       [invoice].[Invoice].[TotalAmount],
                       [invoice].[InvoiceStatusType].[Name] AS [StatusName],
                       [invoice].[InvoiceFinancialStatusType].[Name] AS [FinancialStatusName],
                       [invoice].[Invoice].[InvoiceStatusTypeId],
                       [invoice].[Invoice].[InvoiceFinancialStatusTypeId],
                       COUNT(*) OVER() AS [TotalCount]
                FROM [invoice].[Invoice]
                INNER JOIN [customer].[Customer] ON [invoice].[Invoice].[CustomerId] = [customer].[Customer].[Id]
                INNER JOIN [invoice].[InvoiceStatusType] ON [invoice].[Invoice].[InvoiceStatusTypeId] = [invoice].[InvoiceStatusType].[Id]
                INNER JOIN [invoice].[InvoiceFinancialStatusType] ON [invoice].[Invoice].[InvoiceFinancialStatusTypeId] = [invoice].[InvoiceFinancialStatusType].[Id]
                WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
                  AND [invoice].[Invoice].[IsDeleted] = 0
                  AND (@StatusFilter IS NULL OR [invoice].[Invoice].[InvoiceStatusTypeId] = @StatusFilter)
                  AND (@FinancialStatusFilter IS NULL OR [invoice].[Invoice].[InvoiceFinancialStatusTypeId] = @FinancialStatusFilter)
                  AND (@CustomerFilter IS NULL OR [invoice].[Invoice].[CustomerId] = @CustomerFilter)
                  AND (@VatPeriodId IS NULL OR [invoice].[Invoice].[VatSubmissionPeriodId] = @VatPeriodId)
                  AND (@SearchTerm IS NULL OR (
                      [invoice].[Invoice].[InvoiceNumber] LIKE '%' + @SearchTerm + '%'
                      OR [customer].[Customer].[Name] LIKE '%' + @SearchTerm + '%'
                  ))
                ORDER BY [invoice].[Invoice].[InvoiceDate] DESC, [invoice].[Invoice].[Id] DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var results = new List<InvoiceListDto>();
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
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@StatusFilter", statusFilter ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@FinancialStatusFilter", financialStatusFilter ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@CustomerFilter", customerFilter ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@VatPeriodId", vatPeriodId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@SearchTerm", (object?)escapedSearchTerm ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Offset", offset));
                command.Parameters.Add(new SqlParameter("@PageSize", pageSize));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    totalCount = reader.GetInt32(reader.GetOrdinal("TotalCount"));
                    results.Add(new InvoiceListDto
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                        InvoiceNumber = reader.GetString(reader.GetOrdinal("InvoiceNumber")),
                        CustomerId = reader.GetInt32(reader.GetOrdinal("CustomerId")),
                        CustomerName = reader.GetString(reader.GetOrdinal("CustomerName")),
                        InvoiceDate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("InvoiceDate"))),
                        DueDate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("DueDate"))),
                        TotalAmount = reader.GetDecimal(reader.GetOrdinal("TotalAmount")),
                        StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
                        FinancialStatusName = reader.GetString(reader.GetOrdinal("FinancialStatusName")),
                        InvoiceStatusTypeId = reader.GetInt32(reader.GetOrdinal("InvoiceStatusTypeId")),
                        InvoiceFinancialStatusTypeId = reader.GetInt32(reader.GetOrdinal("InvoiceFinancialStatusTypeId"))
                    });
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
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
    /// Returns all invoices matching the given filters without pagination.
    /// Used for CSV/PDF export operations.
    /// </summary>
    public async Task<List<InvoiceListDto>> GetAllFilteredByBusinessIdAsync(
        int businessId,
        int? statusFilter,
        int? financialStatusFilter,
        int? customerFilter,
        string? searchTerm,
        int? vatPeriodId = null)
    {
        try
        {
            const string query = @"
                SELECT [invoice].[Invoice].[Id],
                       [invoice].[Invoice].[InvoiceNumber],
                       [invoice].[Invoice].[CustomerId],
                       [customer].[Customer].[Name] AS [CustomerName],
                       [invoice].[Invoice].[InvoiceDate],
                       [invoice].[Invoice].[DueDate],
                       [invoice].[Invoice].[TotalAmount],
                       [invoice].[InvoiceStatusType].[Name] AS [StatusName],
                       [invoice].[InvoiceFinancialStatusType].[Name] AS [FinancialStatusName],
                       [invoice].[Invoice].[InvoiceStatusTypeId],
                       [invoice].[Invoice].[InvoiceFinancialStatusTypeId]
                FROM [invoice].[Invoice]
                INNER JOIN [customer].[Customer] ON [invoice].[Invoice].[CustomerId] = [customer].[Customer].[Id]
                INNER JOIN [invoice].[InvoiceStatusType] ON [invoice].[Invoice].[InvoiceStatusTypeId] = [invoice].[InvoiceStatusType].[Id]
                INNER JOIN [invoice].[InvoiceFinancialStatusType] ON [invoice].[Invoice].[InvoiceFinancialStatusTypeId] = [invoice].[InvoiceFinancialStatusType].[Id]
                WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
                  AND [invoice].[Invoice].[IsDeleted] = 0
                  AND (@StatusFilter IS NULL OR [invoice].[Invoice].[InvoiceStatusTypeId] = @StatusFilter)
                  AND (@FinancialStatusFilter IS NULL OR [invoice].[Invoice].[InvoiceFinancialStatusTypeId] = @FinancialStatusFilter)
                  AND (@CustomerFilter IS NULL OR [invoice].[Invoice].[CustomerId] = @CustomerFilter)
                  AND (@VatPeriodId IS NULL OR [invoice].[Invoice].[VatSubmissionPeriodId] = @VatPeriodId)
                  AND (@SearchTerm IS NULL OR (
                      [invoice].[Invoice].[InvoiceNumber] LIKE '%' + @SearchTerm + '%'
                      OR [customer].[Customer].[Name] LIKE '%' + @SearchTerm + '%'
                  ))
                ORDER BY [invoice].[Invoice].[InvoiceDate] DESC, [invoice].[Invoice].[Id] DESC";

            var results = new List<InvoiceListDto>();
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
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@StatusFilter", statusFilter ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@FinancialStatusFilter", financialStatusFilter ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@CustomerFilter", customerFilter ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@VatPeriodId", vatPeriodId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@SearchTerm", (object?)escapedSearchTerm ?? DBNull.Value));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new InvoiceListDto
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                        InvoiceNumber = reader.GetString(reader.GetOrdinal("InvoiceNumber")),
                        CustomerId = reader.GetInt32(reader.GetOrdinal("CustomerId")),
                        CustomerName = reader.GetString(reader.GetOrdinal("CustomerName")),
                        InvoiceDate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("InvoiceDate"))),
                        DueDate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("DueDate"))),
                        TotalAmount = reader.GetDecimal(reader.GetOrdinal("TotalAmount")),
                        StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
                        FinancialStatusName = reader.GetString(reader.GetOrdinal("FinancialStatusName")),
                        InvoiceStatusTypeId = reader.GetInt32(reader.GetOrdinal("InvoiceStatusTypeId")),
                        InvoiceFinancialStatusTypeId = reader.GetInt32(reader.GetOrdinal("InvoiceFinancialStatusTypeId"))
                    });
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
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
    /// Updates only the InvoiceFinancialStatusTypeId and UpdatedAtUtc for an invoice.
    /// Used by the FinancialStatusEngine after recalculating status.
    /// </summary>
    public virtual async Task UpdateFinancialStatusAsync(int invoiceId, int financialStatusTypeId)
    {
        try
        {
            const string query = @"
                UPDATE [invoice].[Invoice]
                SET [InvoiceFinancialStatusTypeId] = @FinancialStatusTypeId,
                    [UpdatedAtUtc] = @UpdatedAtUtc
                WHERE [Id] = @InvoiceId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@InvoiceId", invoiceId),
                new SqlParameter("@FinancialStatusTypeId", financialStatusTypeId),
                new SqlParameter("@UpdatedAtUtc", DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns revenue grouped by product name for won leads with ClosedAtUtc in [startDate, endDate).
    /// Leads with no ProductId are grouped under "General Enquiry".
    /// </summary>
    public async Task<List<RevenueBreakdownDto>> GetRevenueByProductAsync(DateTime startDate, DateTime endDate, int wonStatusTypeId, int businessId)
    {
        try
        {
            const string query = @"
                SELECT ISNULL([sales].[Product].[Name], 'General Enquiry') AS [Name],
                       SUM([invoice].[Invoice].[TotalAmount]) AS [TotalRevenue]
                FROM [invoice].[Invoice]
                INNER JOIN [sales].[LeadRequest] ON [invoice].[Invoice].[LeadRequestId] = [sales].[LeadRequest].[Id]
                LEFT JOIN [sales].[Product] ON [sales].[LeadRequest].[ProductId] = [sales].[Product].[Id]
                WHERE [sales].[LeadRequest].[BusinessId] = @BusinessId
                  AND [sales].[LeadRequest].[ClosedAtUtc] >= @StartDate
                  AND [sales].[LeadRequest].[ClosedAtUtc] < @EndDate
                  AND [sales].[LeadRequest].[LeadStatusTypeId] = @WonStatusTypeId
                GROUP BY ISNULL([sales].[Product].[Name], 'General Enquiry')
                ORDER BY SUM([invoice].[Invoice].[TotalAmount]) DESC";

            var results = new List<RevenueBreakdownDto>();
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@StartDate", startDate));
                command.Parameters.Add(new SqlParameter("@EndDate", endDate));
                command.Parameters.Add(new SqlParameter("@WonStatusTypeId", wonStatusTypeId));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new RevenueBreakdownDto
                    {
                        Name = reader.GetString(reader.GetOrdinal("Name")),
                        TotalRevenue = reader.GetDecimal(reader.GetOrdinal("TotalRevenue"))
                    });
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }

            // Compute percentage of total for each row
            var grandTotal = results.Sum(r => r.TotalRevenue);
            foreach (var row in results)
            {
                row.Percentage = grandTotal > 0
                    ? Math.Round((row.TotalRevenue / grandTotal) * 100, 2)
                    : 0;
            }

            return results;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns revenue grouped by lead source name for won leads with ClosedAtUtc in [startDate, endDate).
    /// </summary>
    public async Task<List<RevenueBreakdownDto>> GetRevenueBySourceAsync(DateTime startDate, DateTime endDate, int wonStatusTypeId, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [sales].[LeadSourceType].[Name] AS [Name],
                       SUM([invoice].[Invoice].[TotalAmount]) AS [TotalRevenue]
                FROM [invoice].[Invoice]
                INNER JOIN [sales].[LeadRequest] ON [invoice].[Invoice].[LeadRequestId] = [sales].[LeadRequest].[Id]
                INNER JOIN [sales].[LeadSourceType] ON [sales].[LeadRequest].[LeadSourceTypeId] = [sales].[LeadSourceType].[Id]
                WHERE [sales].[LeadRequest].[BusinessId] = @BusinessId
                  AND [sales].[LeadRequest].[ClosedAtUtc] >= @StartDate
                  AND [sales].[LeadRequest].[ClosedAtUtc] < @EndDate
                  AND [sales].[LeadRequest].[LeadStatusTypeId] = @WonStatusTypeId
                GROUP BY [sales].[LeadSourceType].[Name]
                ORDER BY SUM([invoice].[Invoice].[TotalAmount]) DESC";

            var results = new List<RevenueBreakdownDto>();
            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@StartDate", startDate));
                command.Parameters.Add(new SqlParameter("@EndDate", endDate));
                command.Parameters.Add(new SqlParameter("@WonStatusTypeId", wonStatusTypeId));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new RevenueBreakdownDto
                    {
                        Name = reader.GetString(reader.GetOrdinal("Name")),
                        TotalRevenue = reader.GetDecimal(reader.GetOrdinal("TotalRevenue"))
                    });
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }

            // Compute percentage of total for each row
            var grandTotal = results.Sum(r => r.TotalRevenue);
            foreach (var row in results)
            {
                row.Percentage = grandTotal > 0
                    ? Math.Round((row.TotalRevenue / grandTotal) * 100, 2)
                    : 0;
            }

            return results;
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
