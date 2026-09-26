using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Data;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Aggregates "logical" storage usage (SUM of live rows' FileSizeBytes) from the file-metadata
/// tables. Used by the per-business Storage page and the SuperAdmin platform Storage page.
///
/// Categories measured: Document attachments ([document].[DocumentAttachment], excludes soft-deleted),
/// Compliance attachments ([compliance].[ApplicationAttachment], scoped to a business via a join to
/// [compliance].[BusinessApplication]), and Business logos ([portal].[BusinessLogo]). Signatures are
/// intentionally NOT measured yet — the [portal].[Signature] table has no size column.
///
/// Raw SQL throughout: the per-business SUMs mirror DocumentAttachmentRepository.GetSummaryAsync, and
/// the cross-tenant GROUP BY avoids EF global query filters entirely (raw SQL isn't filtered).
/// </summary>
public class StorageUsageRepository
{
    private readonly PortalDbContext _context;

    public StorageUsageRepository(PortalDbContext context)
    {
        _context = context;
    }

    // ── Per-business (My Business → Storage) ──────────────────────────────

    /// <summary>Document-attachment total (bytes) + file count for one business (live rows only).</summary>
    public async Task<(long Bytes, int Files)> GetAttachmentUsageAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT ISNULL(SUM(DocumentAttachment.FileSizeBytes), 0) AS [Bytes], COUNT(*) AS [Files]
                FROM [document].[DocumentAttachment]
                WHERE DocumentAttachment.BusinessId = @BusinessId AND DocumentAttachment.IsDeleted = 0";
            return await ReadBytesFilesAsync(query, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>Compliance-attachment total (bytes) + file count for one business (join to BusinessApplication).</summary>
    public async Task<(long Bytes, int Files)> GetComplianceUsageAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT ISNULL(SUM(ApplicationAttachment.FileSizeBytes), 0) AS [Bytes], COUNT(*) AS [Files]
                FROM [compliance].[ApplicationAttachment]
                INNER JOIN [compliance].[BusinessApplication]
                    ON [BusinessApplication].[Id] = [ApplicationAttachment].[BusinessApplicationId]
                WHERE [BusinessApplication].[BusinessId] = @BusinessId";
            return await ReadBytesFilesAsync(query, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>Logo total (bytes) + file count for one business.</summary>
    public async Task<(long Bytes, int Files)> GetLogoUsageAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT ISNULL(SUM(BusinessLogo.FileSizeBytes), 0) AS [Bytes], COUNT(*) AS [Files]
                FROM [portal].[BusinessLogo]
                WHERE BusinessLogo.BusinessId = @BusinessId";
            return await ReadBytesFilesAsync(query, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>Signature file count for one business (size not measured — no size column yet).</summary>
    public async Task<int> GetSignatureCountAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT COUNT(*) AS [Value]
                FROM [portal].[Signature]
                WHERE Signature.BusinessId = @BusinessId AND Signature.IsActive = 1";
            var result = await _context.Database
                .SqlQueryRaw<int>(query, new SqlParameter("@BusinessId", businessId))
                .ToListAsync();
            return result.FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>Document-attachment breakdown by record type (EntityType) for one business, live rows only.</summary>
    public async Task<List<StorageByTypeRow>> GetAttachmentsByTypeAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT DocumentAttachment.EntityType AS [EntityType],
                       COUNT(*) AS [Files],
                       ISNULL(SUM(DocumentAttachment.FileSizeBytes), 0) AS [Bytes]
                FROM [document].[DocumentAttachment]
                WHERE DocumentAttachment.BusinessId = @BusinessId AND DocumentAttachment.IsDeleted = 0
                GROUP BY DocumentAttachment.EntityType
                ORDER BY [Bytes] DESC";
            return await _context.Database
                .SqlQueryRaw<StorageByTypeRow>(query, new SqlParameter("@BusinessId", businessId))
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    // ── Cross-tenant (SuperAdmin → Storage) ───────────────────────────────
    // Raw SQL is not subject to EF global query filters, so these see all businesses.

    /// <summary>Per-business document-attachment bytes across ALL businesses (live rows only).</summary>
    public Task<Dictionary<int, long>> GetAllAttachmentBytesAsync() =>
        ReadBytesByBusinessAsync(@"
            SELECT DocumentAttachment.BusinessId AS [BusinessId], ISNULL(SUM(DocumentAttachment.FileSizeBytes), 0) AS [Bytes]
            FROM [document].[DocumentAttachment]
            WHERE DocumentAttachment.IsDeleted = 0
            GROUP BY DocumentAttachment.BusinessId");

    /// <summary>Per-business compliance-attachment bytes across ALL businesses (join to BusinessApplication).</summary>
    public Task<Dictionary<int, long>> GetAllComplianceBytesAsync() =>
        ReadBytesByBusinessAsync(@"
            SELECT [BusinessApplication].[BusinessId] AS [BusinessId], ISNULL(SUM(ApplicationAttachment.FileSizeBytes), 0) AS [Bytes]
            FROM [compliance].[ApplicationAttachment]
            INNER JOIN [compliance].[BusinessApplication]
                ON [BusinessApplication].[Id] = [ApplicationAttachment].[BusinessApplicationId]
            GROUP BY [BusinessApplication].[BusinessId]");

    /// <summary>Per-business logo bytes across ALL businesses.</summary>
    public Task<Dictionary<int, long>> GetAllLogoBytesAsync() =>
        ReadBytesByBusinessAsync(@"
            SELECT BusinessLogo.BusinessId AS [BusinessId], ISNULL(SUM(BusinessLogo.FileSizeBytes), 0) AS [Bytes]
            FROM [portal].[BusinessLogo]
            GROUP BY BusinessLogo.BusinessId");

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task<(long Bytes, int Files)> ReadBytesFilesAsync(string query, SqlParameter parameter)
    {
        var connection = _context.Database.GetDbConnection();
        var opened = false;
        try
        {
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
                opened = true;
            }

            using var command = connection.CreateCommand();
            command.CommandText = query;
            var tx = _context.Database.CurrentTransaction;
            if (tx != null) command.Transaction = tx.GetDbTransaction();
            command.Parameters.Add(new SqlParameter(parameter.ParameterName, parameter.Value));

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var bytes = reader.IsDBNull(0) ? 0L : reader.GetInt64(0);
                var files = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                return (bytes, files);
            }
            return (0L, 0);
        }
        finally
        {
            if (opened && _context.Database.CurrentTransaction == null)
                await connection.CloseAsync();
        }
    }

    private async Task<Dictionary<int, long>> ReadBytesByBusinessAsync(string query)
    {
        var result = new Dictionary<int, long>();
        var connection = _context.Database.GetDbConnection();
        var opened = false;
        try
        {
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
                opened = true;
            }

            using var command = connection.CreateCommand();
            command.CommandText = query;
            var tx = _context.Database.CurrentTransaction;
            if (tx != null) command.Transaction = tx.GetDbTransaction();

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var businessId = reader.GetInt32(0);
                var bytes = reader.IsDBNull(1) ? 0L : reader.GetInt64(1);
                result[businessId] = bytes;
            }
        }
        finally
        {
            if (opened && _context.Database.CurrentTransaction == null)
                await connection.CloseAsync();
        }
        return result;
    }
}

/// <summary>Row for the document-attachment "by record type" breakdown. Keyless projection.</summary>
public class StorageByTypeRow
{
    public string EntityType { get; set; } = string.Empty;
    public int Files { get; set; }
    public long Bytes { get; set; }
}
