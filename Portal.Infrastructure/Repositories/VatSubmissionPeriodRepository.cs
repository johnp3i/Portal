using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for VatSubmissionPeriod entity CRUD operations against the [vat].[VatSubmissionPeriod] table.
/// </summary>
public class VatSubmissionPeriodRepository : GenericStoredProcedureRepository<VatSubmissionPeriod>
{
    public VatSubmissionPeriodRepository(DbContext context) : base(context) { }

    public async Task<List<VatSubmissionPeriod>> GetAllByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [PeriodStartDate], [PeriodEndDate], [PeriodLabel], [CreatedAtUtc]
                FROM [vat].[VatSubmissionPeriod]
                WHERE [vat].[VatSubmissionPeriod].[BusinessId] = @BusinessId
                ORDER BY [vat].[VatSubmissionPeriod].[PeriodStartDate] DESC
                OFFSET 0 ROWS";

            return await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task<VatSubmissionPeriod?> GetByIdAndBusinessIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [PeriodStartDate], [PeriodEndDate], [PeriodLabel], [CreatedAtUtc]
                FROM [vat].[VatSubmissionPeriod]
                WHERE [vat].[VatSubmissionPeriod].[Id] = @Id
                  AND [vat].[VatSubmissionPeriod].[BusinessId] = @BusinessId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<VatSubmissionPeriod?> GetLatestByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT TOP 1 [Id], [BusinessId], [PeriodStartDate], [PeriodEndDate], [PeriodLabel], [CreatedAtUtc]
                FROM [vat].[VatSubmissionPeriod]
                WHERE [vat].[VatSubmissionPeriod].[BusinessId] = @BusinessId
                ORDER BY [vat].[VatSubmissionPeriod].[PeriodEndDate] DESC";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task<VatSubmissionPeriod?> GetByDateAndBusinessIdAsync(DateOnly invoiceDate, int businessId)
    {
        try
        {
            const string query = @"
                SELECT TOP 1 [Id], [BusinessId], [PeriodStartDate], [PeriodEndDate], [PeriodLabel], [CreatedAtUtc]
                FROM [vat].[VatSubmissionPeriod]
                WHERE [vat].[VatSubmissionPeriod].[BusinessId] = @BusinessId
                  AND [vat].[VatSubmissionPeriod].[PeriodStartDate] <= @InvoiceDate
                  AND [vat].[VatSubmissionPeriod].[PeriodEndDate] >= @InvoiceDate
                ORDER BY [vat].[VatSubmissionPeriod].[PeriodStartDate] ASC";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@InvoiceDate", invoiceDate));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task<List<VatSubmissionPeriod>> GetUnsubmittedPeriodsFromAsync(int businessId, DateOnly fromDate)
    {
        try
        {
            const string query = @"
                SELECT [vat].[VatSubmissionPeriod].[Id],
                       [vat].[VatSubmissionPeriod].[BusinessId],
                       [vat].[VatSubmissionPeriod].[PeriodStartDate],
                       [vat].[VatSubmissionPeriod].[PeriodEndDate],
                       [vat].[VatSubmissionPeriod].[PeriodLabel],
                       [vat].[VatSubmissionPeriod].[CreatedAtUtc]
                FROM [vat].[VatSubmissionPeriod]
                LEFT JOIN [vat].[VatSubmission]
                    ON [vat].[VatSubmissionPeriod].[Id] = [vat].[VatSubmission].[VatSubmissionPeriodId]
                   AND [vat].[VatSubmission].[BusinessId] = @BusinessId
                WHERE [vat].[VatSubmissionPeriod].[BusinessId] = @BusinessId
                  AND [vat].[VatSubmissionPeriod].[PeriodStartDate] >= @FromDate
                  AND ([vat].[VatSubmission].[Id] IS NULL OR [vat].[VatSubmission].[IsSubmitted] = 0)
                ORDER BY [vat].[VatSubmissionPeriod].[PeriodStartDate] ASC
                OFFSET 0 ROWS";

            return await ExecuteStoredProcedure(query,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@FromDate", fromDate));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task InsertAsync(VatSubmissionPeriod entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [vat].[VatSubmissionPeriod]
                    ([BusinessId], [PeriodStartDate], [PeriodEndDate], [PeriodLabel], [CreatedAtUtc])
                VALUES
                    (@BusinessId, @PeriodStartDate, @PeriodEndDate, @PeriodLabel, @CreatedAtUtc)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@PeriodStartDate", entity.PeriodStartDate),
                new SqlParameter("@PeriodEndDate", entity.PeriodEndDate),
                new SqlParameter("@PeriodLabel", entity.PeriodLabel ?? (object)DBNull.Value),
                new SqlParameter("@CreatedAtUtc", entity.CreatedAtUtc)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }
}
