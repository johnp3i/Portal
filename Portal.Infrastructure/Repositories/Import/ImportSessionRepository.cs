using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities.Import;

namespace Portal.Infrastructure.Repositories.Import;

/// <summary>
/// Repository for ImportSession transient data during the upload-preview-confirm workflow.
/// </summary>
public class ImportSessionRepository : GenericStoredProcedureRepository<ImportSession>
{
    public ImportSessionRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Creates a new import session and returns the generated Id.
    /// </summary>
    public async Task<int> CreateSessionAsync(ImportSession entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [import].[ImportSession]
                    ([BusinessId], [SupplierId], [ParserTemplateId], [FileName],
                     [TotalRows], [ValidRows], [InvalidRows], [RowDataJson])
                VALUES
                    (@BusinessId, @SupplierId, @ParserTemplateId, @FileName,
                     @TotalRows, @ValidRows, @InvalidRows, @RowDataJson);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var result = await _context.Database.SqlQueryRaw<int>(query,
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@SupplierId", entity.SupplierId),
                new SqlParameter("@ParserTemplateId", entity.ParserTemplateId ?? (object)DBNull.Value),
                new SqlParameter("@FileName", entity.FileName),
                new SqlParameter("@TotalRows", entity.TotalRows),
                new SqlParameter("@ValidRows", entity.ValidRows),
                new SqlParameter("@InvalidRows", entity.InvalidRows),
                new SqlParameter("@RowDataJson", entity.RowDataJson)
            ).ToListAsync();

            return result.FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets a session by Id scoped to a business.
    /// </summary>
    public async Task<ImportSession?> GetByIdAsync(int sessionId, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [SupplierId], [ParserTemplateId], [FileName],
                       [TotalRows], [ValidRows], [InvalidRows], [RowDataJson], [IsConfirmed], [CreatedAtUtc]
                FROM [import].[ImportSession]
                WHERE ImportSession.Id = @Id
                  AND ImportSession.BusinessId = @BusinessId
                  AND ImportSession.IsConfirmed = 0";

            return await ExecuteSingleRecordStoredProcedureUnfiltered(query,
                new SqlParameter("@Id", sessionId),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Updates the RowDataJson and row counts after user edits.
    /// </summary>
    public async Task UpdateRowDataAsync(int sessionId, int businessId, string rowDataJson, int validRows, int invalidRows, int totalRows)
    {
        try
        {
            const string query = @"
                UPDATE [import].[ImportSession]
                SET [RowDataJson] = @RowDataJson,
                    [ValidRows] = @ValidRows,
                    [InvalidRows] = @InvalidRows,
                    [TotalRows] = @TotalRows
                WHERE ImportSession.Id = @Id
                  AND ImportSession.BusinessId = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", sessionId),
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@RowDataJson", rowDataJson),
                new SqlParameter("@ValidRows", validRows),
                new SqlParameter("@InvalidRows", invalidRows),
                new SqlParameter("@TotalRows", totalRows));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Hard-deletes a session (transient data, not soft-delete).
    /// </summary>
    public async Task DeleteAsync(int sessionId, int businessId)
    {
        try
        {
            const string query = @"
                DELETE FROM [import].[ImportSession]
                WHERE ImportSession.Id = @Id
                  AND ImportSession.BusinessId = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", sessionId),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Cleans up expired sessions older than the cutoff date.
    /// </summary>
    public async Task DeleteExpiredSessionsAsync(DateTime cutoff)
    {
        try
        {
            const string query = @"
                DELETE FROM [import].[ImportSession]
                WHERE ImportSession.CreatedAtUtc < @Cutoff
                  AND ImportSession.IsConfirmed = 0";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Cutoff", cutoff));
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
