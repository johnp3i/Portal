using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Storage;
using Portal.Infrastructure.Services;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Data access for the orphaned-file cleanup (Phase 4b). Reads the file-reference paths from every
/// file-owning table (to build the "referenced" set the matcher compares against) and manages the
/// [Storage].[OrphanedFileCandidate] / [Storage].[OrphanedFileDeletionLog] tables.
///
/// Raw SQL throughout, consistent with the other storage repositories. All SELECTs list every
/// mapped column (the "required column not present" gotcha that broke Billing).
/// </summary>
public class OrphanedFileCleanupRepository
{
    private readonly PortalDbContext _context;

    public OrphanedFileCleanupRepository(PortalDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Column list for the entity-materializing <see cref="OrphanedFileStatusType"/> read. Every
    /// mapped scalar must appear or EF throws "The required column 'X' was not present". Verified
    /// by OrphanedFileCleanupRepositorySqlColumnTests against the EF model.
    /// </summary>
    public const string StatusTypeSelectColumns = @"
                       [Storage].[OrphanedFileStatusType].[Id] AS [Id],
                       [Storage].[OrphanedFileStatusType].[Name] AS [Name],
                       [Storage].[OrphanedFileStatusType].[Description] AS [Description]";

    /// <summary>
    /// Column list for the entity-materializing <see cref="OrphanedFileDeletionLog"/> read. Every
    /// mapped scalar must appear or EF throws "The required column 'X' was not present". Verified
    /// by OrphanedFileCleanupRepositorySqlColumnTests against the EF model.
    /// </summary>
    public const string DeletionLogSelectColumns = @"
                       [Storage].[OrphanedFileDeletionLog].[Id] AS [Id],
                       [Storage].[OrphanedFileDeletionLog].[RelativePath] AS [RelativePath],
                       [Storage].[OrphanedFileDeletionLog].[BusinessId] AS [BusinessId],
                       [Storage].[OrphanedFileDeletionLog].[FileSizeBytes] AS [FileSizeBytes],
                       [Storage].[OrphanedFileDeletionLog].[Reason] AS [Reason],
                       [Storage].[OrphanedFileDeletionLog].[DeletedAtUtc] AS [DeletedAtUtc],
                       [Storage].[OrphanedFileDeletionLog].[CreatedAtUtc] AS [CreatedAtUtc]";

    // ── Referenced-path sources (for the matcher) ─────────────────────────────

    /// <summary>
    /// All storage-relative paths referenced by a live DB row, across every file-owning table.
    /// Includes soft-deleted document attachments and inactive signatures (they still own a file).
    /// Logos are reconstructed to their on-disk form ({BusinessId}/logos/{FileName}).
    /// </summary>
    public async Task<List<string>> GetAllReferencedPathsAsync()
    {
        try
        {
            var paths = new List<string>();

            // Document attachments — StoragePath verbatim, ALL rows (incl. IsDeleted = 1).
            paths.AddRange(await _context.Database
                .SqlQueryRaw<string>("SELECT DocumentAttachment.StoragePath AS [Value] FROM [document].[DocumentAttachment]")
                .ToListAsync());

            // Compliance attachments — FilePath verbatim.
            paths.AddRange(await _context.Database
                .SqlQueryRaw<string>("SELECT ApplicationAttachment.FilePath AS [Value] FROM [compliance].[ApplicationAttachment]")
                .ToListAsync());

            // Signatures — FilePath verbatim, ALL rows (incl. inactive).
            paths.AddRange(await _context.Database
                .SqlQueryRaw<string>("SELECT Signature.FilePath AS [Value] FROM [portal].[Signature]")
                .ToListAsync());

            // Logos — DB stores only the bare filename; reconstruct the on-disk relative path.
            var logos = await _context.Database
                .SqlQueryRaw<LogoPathRow>("SELECT BusinessLogo.BusinessId AS [BusinessId], BusinessLogo.FileName AS [FileName] FROM [portal].[BusinessLogo]")
                .ToListAsync();
            foreach (var logo in logos)
                paths.Add(OrphanedFileMatcher.LogoRelativePath(logo.BusinessId, logo.FileName));

            return paths;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    // ── Candidates ────────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts a new candidate (Pending) or, if the path is already tracked, refreshes it ONLY when
    /// it is still Pending (keeps size/schedule current). Paused/Cancelled/Deleted rows are left
    /// untouched so an admin's decision — and cancelled exclusions — survive re-scans.
    /// </summary>
    public async Task UpsertPendingCandidateAsync(string relativePath, int? businessId, long fileSizeBytes, DateTime scheduledDeletionAtUtc)
    {
        try
        {
            const string query = @"
                MERGE [Storage].[OrphanedFileCandidate] AS Target
                USING (SELECT @RelativePath AS [RelativePath]) AS Source
                ON Target.[RelativePath] = Source.[RelativePath]
                WHEN MATCHED AND Target.[OrphanedFileStatusTypeId] = @PendingStatus THEN
                    UPDATE SET [BusinessId] = @BusinessId,
                               [FileSizeBytes] = @FileSizeBytes,
                               [ScheduledDeletionAtUtc] = @ScheduledDeletionAtUtc,
                               [UpdatedAtUtc] = GETUTCDATE()
                WHEN NOT MATCHED THEN
                    INSERT ([RelativePath], [BusinessId], [FileSizeBytes], [DetectedAtUtc],
                            [ScheduledDeletionAtUtc], [OrphanedFileStatusTypeId], [CreatedAtUtc], [UpdatedAtUtc])
                    VALUES (@RelativePath, @BusinessId, @FileSizeBytes, GETUTCDATE(),
                            @ScheduledDeletionAtUtc, @PendingStatus, GETUTCDATE(), GETUTCDATE());";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@RelativePath", relativePath),
                new SqlParameter("@BusinessId", (object?)businessId ?? DBNull.Value),
                new SqlParameter("@FileSizeBytes", fileSizeBytes),
                new SqlParameter("@ScheduledDeletionAtUtc", scheduledDeletionAtUtc),
                new SqlParameter("@PendingStatus", OrphanedFileStatus.Pending));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>All candidates with their status name/description, newest scheduled first.</summary>
    public async Task<List<OrphanedFileCandidateRow>> GetCandidatesAsync()
    {
        try
        {
            const string query = @"
                SELECT c.[Id] AS [Id],
                       c.[RelativePath] AS [RelativePath],
                       c.[BusinessId] AS [BusinessId],
                       c.[FileSizeBytes] AS [FileSizeBytes],
                       c.[DetectedAtUtc] AS [DetectedAtUtc],
                       c.[ScheduledDeletionAtUtc] AS [ScheduledDeletionAtUtc],
                       c.[OrphanedFileStatusTypeId] AS [OrphanedFileStatusTypeId],
                       s.[Name] AS [StatusName],
                       s.[Description] AS [StatusDescription]
                FROM [Storage].[OrphanedFileCandidate] c
                INNER JOIN [Storage].[OrphanedFileStatusType] s
                    ON s.[Id] = c.[OrphanedFileStatusTypeId]
                ORDER BY c.[ScheduledDeletionAtUtc] ASC";

            return await _context.Database
                .SqlQueryRaw<OrphanedFileCandidateRow>(query)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>Sets a candidate's status (cancel/pause/resume). Resume returns it to Pending.</summary>
    public async Task SetCandidateStatusAsync(int candidateId, int statusTypeId)
    {
        try
        {
            const string query = @"
                UPDATE [Storage].[OrphanedFileCandidate]
                SET [OrphanedFileStatusTypeId] = @StatusTypeId,
                    [UpdatedAtUtc] = GETUTCDATE()
                WHERE [Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", candidateId),
                new SqlParameter("@StatusTypeId", statusTypeId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>All status types (Id/Name/Description) for the page legend.</summary>
    public async Task<List<OrphanedFileStatusType>> GetStatusTypesAsync()
    {
        try
        {
            const string query = @"
                SELECT " + StatusTypeSelectColumns + @"
                FROM [Storage].[OrphanedFileStatusType]
                ORDER BY [Storage].[OrphanedFileStatusType].[Id]";

            return await _context.Set<OrphanedFileStatusType>()
                .FromSqlRaw(query)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    // ── Deletion log (report) ──────────────────────────────────────────────────

    /// <summary>The deletion audit trail, newest first (source for the deletion report).</summary>
    public async Task<List<OrphanedFileDeletionLog>> GetDeletionLogAsync()
    {
        try
        {
            const string query = @"
                SELECT " + DeletionLogSelectColumns + @"
                FROM [Storage].[OrphanedFileDeletionLog]
                ORDER BY [Storage].[OrphanedFileDeletionLog].[DeletedAtUtc] DESC";

            return await _context.Set<OrphanedFileDeletionLog>()
                .FromSqlRaw(query)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}

/// <summary>Keyless projection: a business's logo filename (to reconstruct the on-disk path).</summary>
public class LogoPathRow
{
    public int BusinessId { get; set; }
    public string FileName { get; set; } = null!;
}

/// <summary>Keyless projection: a candidate joined to its status name + description for the UI.</summary>
public class OrphanedFileCandidateRow
{
    public int Id { get; set; }
    public string RelativePath { get; set; } = null!;
    public int? BusinessId { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime DetectedAtUtc { get; set; }
    public DateTime ScheduledDeletionAtUtc { get; set; }
    public int OrphanedFileStatusTypeId { get; set; }
    public string StatusName { get; set; } = null!;
    public string StatusDescription { get; set; } = null!;
}
