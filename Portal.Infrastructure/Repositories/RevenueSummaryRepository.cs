using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for RevenueSummary (Z-Report headers) and RevenueSummaryLine (VAT breakdown) CRUD operations.
/// Schema: [revenue]
/// </summary>
public class RevenueSummaryRepository : GenericStoredProcedureRepository<RevenueSummary>
{
    public RevenueSummaryRepository(PortalDbContext context) : base(context) { }

    /// <summary>
    /// Gets all active revenue summaries for a business, ordered by SummaryDate descending.
    /// Includes related RevenueSource name for display.
    /// </summary>
    public async Task<List<RevenueSummary>> GetAllByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [RevenueSourceId], [SummaryDate], [PeriodEndDate],
                       [ZReportNumber], [TotalNet], [TotalVat], [TotalGross], [TotalDiscount],
                       [TransactionCount], [Reference], [Notes], [ExportedAtUtc],
                       [VatSubmissionPeriodId], [ImportSessionId], [IsActive], [CreatedAtUtc]
                FROM [revenue].[RevenueSummary]
                WHERE RevenueSummary.BusinessId = @BusinessId AND RevenueSummary.IsActive = 1
                ORDER BY RevenueSummary.SummaryDate DESC
                OFFSET 0 ROWS";

            return await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets paged revenue summaries with optional filters.
    /// dateMode: "period" filters on SummaryDate, "created" filters on CreatedAtUtc.
    /// includeInactive: when true, returns all records (active and cancelled).
    /// </summary>
    public async Task<(List<RevenueSummary> Items, int TotalCount)> GetPagedAsync(
        int businessId,
        int? revenueSourceId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? zReportNumber,
        int offset,
        int pageSize,
        string dateMode = "period",
        bool includeInactive = false)
    {
        try
        {
            // Determine which column the date filter applies to
            var dateColumn = dateMode == "created" ? "RevenueSummary.CreatedAtUtc" : "RevenueSummary.SummaryDate";
            var orderColumn = dateMode == "created" ? "RevenueSummary.CreatedAtUtc" : "RevenueSummary.SummaryDate";
            var activeFilter = includeInactive ? "" : "AND RevenueSummary.IsActive = 1";

            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@Offset", offset),
                new SqlParameter("@PageSize", pageSize),
                new SqlParameter("@RevenueSourceId", revenueSourceId ?? (object)DBNull.Value),
                new SqlParameter("@DateFrom", dateFrom.HasValue ? dateFrom.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value),
                new SqlParameter("@DateTo", dateTo.HasValue ? dateTo.Value.ToDateTime(TimeOnly.MaxValue) : DBNull.Value),
                new SqlParameter("@ZReportNumber", zReportNumber ?? (object)DBNull.Value)
            };

            string countQuery = $@"
                SELECT COUNT(1)
                FROM [revenue].[RevenueSummary]
                WHERE RevenueSummary.BusinessId = @BusinessId
                  {activeFilter}
                  AND (@RevenueSourceId IS NULL OR RevenueSummary.RevenueSourceId = @RevenueSourceId)
                  AND (@DateFrom IS NULL OR {dateColumn} >= @DateFrom)
                  AND (@DateTo IS NULL OR {dateColumn} <= @DateTo)
                  AND (@ZReportNumber IS NULL OR RevenueSummary.ZReportNumber LIKE '%' + @ZReportNumber + '%')";

            string dataQuery = $@"
                SELECT [Id], [BusinessId], [RevenueSourceId], [SummaryDate], [PeriodEndDate],
                       [ZReportNumber], [TotalNet], [TotalVat], [TotalGross], [TotalDiscount],
                       [TransactionCount], [Reference], [Notes], [ExportedAtUtc],
                       [VatSubmissionPeriodId], [ImportSessionId], [IsActive], [CreatedAtUtc]
                FROM [revenue].[RevenueSummary]
                WHERE RevenueSummary.BusinessId = @BusinessId
                  {activeFilter}
                  AND (@RevenueSourceId IS NULL OR RevenueSummary.RevenueSourceId = @RevenueSourceId)
                  AND (@DateFrom IS NULL OR {dateColumn} >= @DateFrom)
                  AND (@DateTo IS NULL OR {dateColumn} <= @DateTo)
                  AND (@ZReportNumber IS NULL OR RevenueSummary.ZReportNumber LIKE '%' + @ZReportNumber + '%')
                ORDER BY {orderColumn} DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var connection = _context.Database.GetDbConnection();
            int totalCount;

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                // Get count
                using (var countCmd = connection.CreateCommand())
                {
                    countCmd.CommandText = countQuery;

                    var transaction = _context.Database.CurrentTransaction;
                    if (transaction != null)
                        countCmd.Transaction = transaction.GetDbTransaction();

                    countCmd.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                    countCmd.Parameters.Add(new SqlParameter("@RevenueSourceId", revenueSourceId ?? (object)DBNull.Value));
                    countCmd.Parameters.Add(new SqlParameter("@DateFrom", dateFrom.HasValue ? dateFrom.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value));
                    countCmd.Parameters.Add(new SqlParameter("@DateTo", dateTo.HasValue ? dateTo.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value));
                    countCmd.Parameters.Add(new SqlParameter("@ZReportNumber", zReportNumber ?? (object)DBNull.Value));

                    var countResult = await countCmd.ExecuteScalarAsync();
                    totalCount = Convert.ToInt32(countResult);
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            // Get data
            var items = await ExecuteStoredProcedure(dataQuery, parameters.ToArray());

            return (items, totalCount);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets a single revenue summary by Id and BusinessId.
    /// </summary>
    public async Task<RevenueSummary?> GetByIdAndBusinessIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [RevenueSourceId], [SummaryDate], [PeriodEndDate],
                       [ZReportNumber], [TotalNet], [TotalVat], [TotalGross], [TotalDiscount],
                       [TransactionCount], [Reference], [Notes], [ExportedAtUtc],
                       [VatSubmissionPeriodId], [ImportSessionId], [IsActive], [CreatedAtUtc]
                FROM [revenue].[RevenueSummary]
                WHERE RevenueSummary.Id = @Id AND RevenueSummary.BusinessId = @BusinessId";

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
    /// Inserts a RevenueSummary header and returns the new Id.
    /// </summary>
    public async Task<int> InsertAsync(RevenueSummary entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [revenue].[RevenueSummary]
                    ([BusinessId], [RevenueSourceId], [SummaryDate], [PeriodEndDate],
                     [ZReportNumber], [TotalNet], [TotalVat], [TotalGross], [TotalDiscount],
                     [TransactionCount], [Reference], [Notes], [ExportedAtUtc],
                     [VatSubmissionPeriodId], [ImportSessionId], [IsActive], [CreatedAtUtc])
                OUTPUT INSERTED.Id
                VALUES
                    (@BusinessId, @RevenueSourceId, @SummaryDate, @PeriodEndDate,
                     @ZReportNumber, @TotalNet, @TotalVat, @TotalGross, @TotalDiscount,
                     @TransactionCount, @Reference, @Notes, @ExportedAtUtc,
                     @VatSubmissionPeriodId, @ImportSessionId, @IsActive, @CreatedAtUtc)";

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
                command.Parameters.Add(new SqlParameter("@RevenueSourceId", entity.RevenueSourceId));
                command.Parameters.Add(new SqlParameter("@SummaryDate", entity.SummaryDate.ToDateTime(TimeOnly.MinValue)));
                command.Parameters.Add(new SqlParameter("@PeriodEndDate", entity.PeriodEndDate.HasValue ? entity.PeriodEndDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value));
                command.Parameters.Add(new SqlParameter("@ZReportNumber", entity.ZReportNumber ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@TotalNet", entity.TotalNet));
                command.Parameters.Add(new SqlParameter("@TotalVat", entity.TotalVat));
                command.Parameters.Add(new SqlParameter("@TotalGross", entity.TotalGross));
                command.Parameters.Add(new SqlParameter("@TotalDiscount", entity.TotalDiscount ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@TransactionCount", entity.TransactionCount ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Reference", entity.Reference ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@ExportedAtUtc", entity.ExportedAtUtc ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@VatSubmissionPeriodId", entity.VatSubmissionPeriodId ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@ImportSessionId", entity.ImportSessionId ?? (object)DBNull.Value));
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
    /// Inserts a RevenueSummaryLine (VAT breakdown) and returns the new Id.
    /// </summary>
    public async Task<int> InsertLineAsync(RevenueSummaryLine line)
    {
        try
        {
            const string query = @"
                INSERT INTO [revenue].[RevenueSummaryLine]
                    ([RevenueSummaryId], [VatRate], [NetAmount], [VatAmount], [TotalAmount],
                     [DiscountAmount], [Description], [CreatedAtUtc])
                OUTPUT INSERTED.Id
                VALUES
                    (@RevenueSummaryId, @VatRate, @NetAmount, @VatAmount, @TotalAmount,
                     @DiscountAmount, @Description, @CreatedAtUtc)";

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

                command.Parameters.Add(new SqlParameter("@RevenueSummaryId", line.RevenueSummaryId));
                command.Parameters.Add(new SqlParameter("@VatRate", line.VatRate));
                command.Parameters.Add(new SqlParameter("@NetAmount", line.NetAmount));
                command.Parameters.Add(new SqlParameter("@VatAmount", line.VatAmount));
                command.Parameters.Add(new SqlParameter("@TotalAmount", line.TotalAmount));
                command.Parameters.Add(new SqlParameter("@DiscountAmount", line.DiscountAmount ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Description", line.Description ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@CreatedAtUtc", line.CreatedAtUtc));

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
    /// Gets all lines for a given revenue summary.
    /// </summary>
    public async Task<List<RevenueSummaryLine>> GetLinesBySummaryIdAsync(int revenueSummaryId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [RevenueSummaryId], [VatRate], [NetAmount], [VatAmount],
                       [TotalAmount], [DiscountAmount], [Description], [CreatedAtUtc]
                FROM [revenue].[RevenueSummaryLine]
                WHERE RevenueSummaryLine.RevenueSummaryId = @RevenueSummaryId
                ORDER BY RevenueSummaryLine.VatRate
                OFFSET 0 ROWS";

            return await _context.Set<RevenueSummaryLine>()
                .FromSqlRaw(query, new SqlParameter("@RevenueSummaryId", revenueSummaryId))
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Soft-deletes a revenue summary by setting IsActive = 0.
    /// </summary>
    public async Task SoftDeleteAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [revenue].[RevenueSummary]
                SET [IsActive] = 0
                WHERE RevenueSummary.Id = @Id AND RevenueSummary.BusinessId = @BusinessId";

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
    /// Restores a soft-deleted revenue summary by setting IsActive = 1.
    /// </summary>
    public async Task RestoreAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [revenue].[RevenueSummary]
                SET [IsActive] = 1
                WHERE RevenueSummary.Id = @Id AND RevenueSummary.BusinessId = @BusinessId";

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
    /// Deletes all lines for a given revenue summary (used before re-inserting on edit).
    /// </summary>
    public async Task DeleteLinesBySummaryIdAsync(int revenueSummaryId)
    {
        try
        {
            const string query = @"
                DELETE FROM [revenue].[RevenueSummaryLine]
                WHERE RevenueSummaryLine.RevenueSummaryId = @RevenueSummaryId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@RevenueSummaryId", revenueSummaryId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates the header fields of a RevenueSummary.
    /// </summary>
    public async Task UpdateAsync(RevenueSummary entity)
    {
        try
        {
            const string query = @"
                UPDATE [revenue].[RevenueSummary]
                SET
                    [RevenueSourceId] = @RevenueSourceId,
                    [SummaryDate] = @SummaryDate,
                    [PeriodEndDate] = @PeriodEndDate,
                    [ZReportNumber] = @ZReportNumber,
                    [TotalNet] = @TotalNet,
                    [TotalVat] = @TotalVat,
                    [TotalGross] = @TotalGross,
                    [TotalDiscount] = @TotalDiscount,
                    [TransactionCount] = @TransactionCount,
                    [Reference] = @Reference,
                    [Notes] = @Notes,
                    [ExportedAtUtc] = @ExportedAtUtc,
                    [VatSubmissionPeriodId] = @VatSubmissionPeriodId
                WHERE RevenueSummary.Id = @Id AND RevenueSummary.BusinessId = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@RevenueSourceId", entity.RevenueSourceId),
                new SqlParameter("@SummaryDate", entity.SummaryDate.ToDateTime(TimeOnly.MinValue)),
                new SqlParameter("@PeriodEndDate", entity.PeriodEndDate.HasValue ? entity.PeriodEndDate.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value),
                new SqlParameter("@ZReportNumber", entity.ZReportNumber ?? (object)DBNull.Value),
                new SqlParameter("@TotalNet", entity.TotalNet),
                new SqlParameter("@TotalVat", entity.TotalVat),
                new SqlParameter("@TotalGross", entity.TotalGross),
                new SqlParameter("@TotalDiscount", entity.TotalDiscount ?? (object)DBNull.Value),
                new SqlParameter("@TransactionCount", entity.TransactionCount ?? (object)DBNull.Value),
                new SqlParameter("@Reference", entity.Reference ?? (object)DBNull.Value),
                new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value),
                new SqlParameter("@ExportedAtUtc", entity.ExportedAtUtc ?? (object)DBNull.Value),
                new SqlParameter("@VatSubmissionPeriodId", entity.VatSubmissionPeriodId ?? (object)DBNull.Value));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Checks for duplicate Z-report by BusinessId + RevenueSourceId + ZReportNumber.
    /// Returns the existing Id if found, or null.
    /// </summary>
    public async Task<int?> FindDuplicateAsync(int businessId, int revenueSourceId, string zReportNumber)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [RevenueSourceId], [SummaryDate], [PeriodEndDate],
                       [ZReportNumber], [TotalNet], [TotalVat], [TotalGross], [TotalDiscount],
                       [TransactionCount], [Reference], [Notes], [ExportedAtUtc],
                       [VatSubmissionPeriodId], [ImportSessionId], [IsActive], [CreatedAtUtc]
                FROM [revenue].[RevenueSummary]
                WHERE RevenueSummary.BusinessId = @BusinessId
                  AND RevenueSummary.RevenueSourceId = @RevenueSourceId
                  AND RevenueSummary.ZReportNumber = @ZReportNumber
                  AND RevenueSummary.IsActive = 1";

            var existing = await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@RevenueSourceId", revenueSourceId),
                new SqlParameter("@ZReportNumber", zReportNumber));

            return existing?.Id;
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
