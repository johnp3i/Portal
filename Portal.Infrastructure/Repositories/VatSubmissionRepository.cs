using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for VatSubmission entity CRUD operations against the [vat].[VatSubmission] table.
/// </summary>
public class VatSubmissionRepository : GenericStoredProcedureRepository<VatSubmission>
{
    public VatSubmissionRepository(DbContext context) : base(context) { }

    public async Task<List<VatSubmission>> GetAllByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [VatSubmissionPeriodId],
                       [TotalOutputVat], [TotalInputVat], [NetVatPayable],
                       [IsSubmitted], [SubmittedAtUtc], [Notes], [CreatedAtUtc]
                FROM [vat].[VatSubmission]
                WHERE VatSubmission.BusinessId = @BusinessId";

            return await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<VatSubmission?> GetByIdAndBusinessIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [VatSubmissionPeriodId],
                       [TotalOutputVat], [TotalInputVat], [NetVatPayable],
                       [IsSubmitted], [SubmittedAtUtc], [Notes], [CreatedAtUtc]
                FROM [vat].[VatSubmission]
                WHERE VatSubmission.Id = @Id AND VatSubmission.BusinessId = @BusinessId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task<VatSubmission?> GetByPeriodIdAndBusinessIdAsync(int vatSubmissionPeriodId, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [VatSubmissionPeriodId],
                       [TotalOutputVat], [TotalInputVat], [NetVatPayable],
                       [IsSubmitted], [SubmittedAtUtc], [Notes], [CreatedAtUtc]
                FROM [vat].[VatSubmission]
                WHERE VatSubmission.VatSubmissionPeriodId = @VatSubmissionPeriodId
                  AND VatSubmission.BusinessId = @BusinessId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@VatSubmissionPeriodId", vatSubmissionPeriodId),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task InsertAsync(VatSubmission entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [vat].[VatSubmission]
                    ([BusinessId], [VatSubmissionPeriodId],
                     [TotalOutputVat], [TotalInputVat], [NetVatPayable],
                     [IsSubmitted], [SubmittedAtUtc], [Notes], [CreatedAtUtc])
                VALUES
                    (@BusinessId, @VatSubmissionPeriodId,
                     @TotalOutputVat, @TotalInputVat, @NetVatPayable,
                     @IsSubmitted, @SubmittedAtUtc, @Notes, @CreatedAtUtc)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@VatSubmissionPeriodId", entity.VatSubmissionPeriodId),
                new SqlParameter("@TotalOutputVat", entity.TotalOutputVat),
                new SqlParameter("@TotalInputVat", entity.TotalInputVat),
                new SqlParameter("@NetVatPayable", entity.NetVatPayable),
                new SqlParameter("@IsSubmitted", entity.IsSubmitted),
                new SqlParameter("@SubmittedAtUtc", entity.SubmittedAtUtc ?? (object)DBNull.Value),
                new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value),
                new SqlParameter("@CreatedAtUtc", entity.CreatedAtUtc)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task UpdateValuesAsync(VatSubmission entity)
    {
        try
        {
            const string query = @"
                UPDATE [vat].[VatSubmission]
                SET
                    [TotalOutputVat] = @TotalOutputVat,
                    [TotalInputVat] = @TotalInputVat,
                    [NetVatPayable] = @NetVatPayable
                WHERE VatSubmission.Id = @Id AND VatSubmission.BusinessId = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@TotalOutputVat", entity.TotalOutputVat),
                new SqlParameter("@TotalInputVat", entity.TotalInputVat),
                new SqlParameter("@NetVatPayable", entity.NetVatPayable)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task MarkAsSubmittedAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [vat].[VatSubmission]
                SET [IsSubmitted] = 1,
                    [SubmittedAtUtc] = GETUTCDATE()
                WHERE VatSubmission.Id = @Id AND VatSubmission.BusinessId = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }
}
