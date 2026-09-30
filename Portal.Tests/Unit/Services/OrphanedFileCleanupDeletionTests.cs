using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Storage;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.Unit.Services;

/// <summary>
/// Unit tests for the DESTRUCTIVE deletion path (Phase 4b-2) of
/// <see cref="OrphanedFileCleanupService.RunCleanupAsync"/>. These assert the safety invariants of
/// the only code that actually removes user files:
///  - a candidate that became referenced again since the scan is NOT deleted (re-verification);
///  - a file already gone from disk is logged (not errored) and the candidate is closed out;
///  - a per-file failure does not abort the batch and leaves the row Pending to retry;
///  - only files that actually existed count toward "bytes freed".
///
/// The repository is a real concrete type over an InMemory context but its data-access methods are
/// virtual, so Moq intercepts them — no live SQL Server is touched.
/// </summary>
public class OrphanedFileCleanupDeletionTests : IDisposable
{
    private readonly PortalDbContext _context;
    private readonly Mock<OrphanedFileCleanupRepository> _repoMock;
    private readonly Mock<PlatformConfigRepository> _configRepoMock;
    private readonly Mock<IFileStorageService> _fileStorageMock;
    private readonly OrphanedFileCleanupService _service;

    public OrphanedFileCleanupDeletionTests()
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(1);

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new PortalDbContext(options, tenantMock.Object);

        _repoMock = new Mock<OrphanedFileCleanupRepository>(MockBehavior.Loose, _context);
        _configRepoMock = new Mock<PlatformConfigRepository>(MockBehavior.Loose, _context);
        _fileStorageMock = new Mock<IFileStorageService>(MockBehavior.Loose);

        var configuration = new ConfigurationBuilder().Build();

        _service = new OrphanedFileCleanupService(
            _repoMock.Object,
            _configRepoMock.Object,
            _fileStorageMock.Object,
            configuration,
            NullLogger<OrphanedFileCleanupService>.Instance);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private static OrphanedFileCandidateRow Candidate(int id, string path, long size = 100, int? businessId = 1) => new()
    {
        Id = id,
        RelativePath = path,
        BusinessId = businessId,
        FileSizeBytes = size,
        DetectedAtUtc = DateTime.UtcNow.AddDays(-40),
        ScheduledDeletionAtUtc = DateTime.UtcNow.AddDays(-10),
        OrphanedFileStatusTypeId = OrphanedFileStatus.Pending,
        StatusName = "Pending",
        StatusDescription = "Detected orphan awaiting deletion."
    };

    [Fact]
    public async Task RunCleanup_DeletesOrphan_LogsIt_AndMarksDeleted()
    {
        var candidate = Candidate(5, "1/document/9/abc_file.pdf", size: 2048);
        _repoMock.Setup(r => r.GetDueCandidatesAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<OrphanedFileCandidateRow> { candidate });
        // Nothing references it any more.
        _repoMock.Setup(r => r.GetAllReferencedPathsAsync()).ReturnsAsync(new List<string>());
        _fileStorageMock.Setup(f => f.ExistsAsync(candidate.RelativePath)).ReturnsAsync(true);

        var result = await _service.RunCleanupAsync();

        Assert.Equal(1, result.Deleted);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(2048, result.BytesFreed);
        _fileStorageMock.Verify(f => f.DeleteAsync(candidate.RelativePath), Times.Once);
        _repoMock.Verify(r => r.InsertDeletionLogAsync(candidate.RelativePath, 1, 2048, It.IsAny<string>(), It.IsAny<DateTime>()), Times.Once);
        _repoMock.Verify(r => r.SetCandidateStatusAsync(5, OrphanedFileStatus.Deleted), Times.Once);
    }

    [Fact]
    public async Task RunCleanup_SkipsCandidate_ThatIsReferencedAgain_AndNeverDeletesTheFile()
    {
        var candidate = Candidate(7, "1/logos/logo.png");
        _repoMock.Setup(r => r.GetDueCandidatesAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<OrphanedFileCandidateRow> { candidate });
        // The path is referenced again (a live record points at it now).
        _repoMock.Setup(r => r.GetAllReferencedPathsAsync())
            .ReturnsAsync(new List<string> { "1/logos/logo.png" });

        var result = await _service.RunCleanupAsync();

        Assert.Equal(0, result.Deleted);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.BytesFreed);
        // The core safety guarantee: the physical file is never touched.
        _fileStorageMock.Verify(f => f.DeleteAsync(It.IsAny<string>()), Times.Never);
        _repoMock.Verify(r => r.InsertDeletionLogAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        // It is returned to Pending so a later scan re-evaluates it.
        _repoMock.Verify(r => r.SetCandidateStatusAsync(7, OrphanedFileStatus.Pending), Times.Once);
        _repoMock.Verify(r => r.SetCandidateStatusAsync(7, OrphanedFileStatus.Deleted), Times.Never);
    }

    [Fact]
    public async Task RunCleanup_FileAlreadyGone_IsLoggedNotErrored_AndDoesNotCountBytes()
    {
        var candidate = Candidate(9, "1/document/3/gone_file.pdf", size: 500);
        _repoMock.Setup(r => r.GetDueCandidatesAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<OrphanedFileCandidateRow> { candidate });
        _repoMock.Setup(r => r.GetAllReferencedPathsAsync()).ReturnsAsync(new List<string>());
        // File is not on disk.
        _fileStorageMock.Setup(f => f.ExistsAsync(candidate.RelativePath)).ReturnsAsync(false);

        var result = await _service.RunCleanupAsync();

        Assert.Equal(1, result.Deleted);   // closed out
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0, result.BytesFreed); // nothing was actually on disk
        _repoMock.Verify(r => r.InsertDeletionLogAsync(candidate.RelativePath, It.IsAny<int?>(), 500, It.IsAny<string>(), It.IsAny<DateTime>()), Times.Once);
        _repoMock.Verify(r => r.SetCandidateStatusAsync(9, OrphanedFileStatus.Deleted), Times.Once);
    }

    [Fact]
    public async Task RunCleanup_OneFailure_DoesNotAbortBatch_AndLeavesFailedRowPending()
    {
        var ok = Candidate(1, "1/document/1/ok.pdf", size: 10);
        var bad = Candidate(2, "1/document/2/bad.pdf", size: 20);
        var ok2 = Candidate(3, "1/document/3/ok2.pdf", size: 30);
        _repoMock.Setup(r => r.GetDueCandidatesAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<OrphanedFileCandidateRow> { ok, bad, ok2 });
        _repoMock.Setup(r => r.GetAllReferencedPathsAsync()).ReturnsAsync(new List<string>());
        _fileStorageMock.Setup(f => f.ExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        // The middle file throws on delete.
        _fileStorageMock.Setup(f => f.DeleteAsync(bad.RelativePath)).ThrowsAsync(new IOException("locked"));

        var result = await _service.RunCleanupAsync();

        Assert.Equal(2, result.Deleted);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(40, result.BytesFreed); // 10 + 30, not 20
        // The two good ones are marked Deleted; the failed one is NOT.
        _repoMock.Verify(r => r.SetCandidateStatusAsync(1, OrphanedFileStatus.Deleted), Times.Once);
        _repoMock.Verify(r => r.SetCandidateStatusAsync(3, OrphanedFileStatus.Deleted), Times.Once);
        _repoMock.Verify(r => r.SetCandidateStatusAsync(2, It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task RunCleanup_NoDueCandidates_IsANoOp()
    {
        _repoMock.Setup(r => r.GetDueCandidatesAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<OrphanedFileCandidateRow>());

        var result = await _service.RunCleanupAsync();

        Assert.Equal(0, result.Deleted);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0, result.BytesFreed);
        // Never even builds the referenced set or touches storage when nothing is due.
        _repoMock.Verify(r => r.GetAllReferencedPathsAsync(), Times.Never);
        _fileStorageMock.Verify(f => f.DeleteAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PreviewDeletion_ReturnsCountAndTotalBytesOfDueCandidates()
    {
        _repoMock.Setup(r => r.GetDueCandidatesAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<OrphanedFileCandidateRow>
            {
                Candidate(1, "a", size: 100),
                Candidate(2, "b", size: 250)
            });

        var preview = await _service.PreviewDeletionAsync();

        Assert.Equal(2, preview.DueCount);
        Assert.Equal(350, preview.TotalBytes);
    }

    [Fact]
    public async Task IsDeletionEnabled_ReadsSeparateConfigKey()
    {
        _configRepoMock.Setup(r => r.GetByKeyAsync("OrphanedFileDeletionEnabled"))
            .ReturnsAsync(new Portal.Infrastructure.Entities.PlatformConfig { Key = "OrphanedFileDeletionEnabled", Value = "true" });

        Assert.True(await _service.IsDeletionEnabledAsync());
    }

    [Fact]
    public async Task IsDeletionEnabled_DefaultsFalse_WhenKeyMissing()
    {
        _configRepoMock.Setup(r => r.GetByKeyAsync("OrphanedFileDeletionEnabled"))
            .ReturnsAsync((Portal.Infrastructure.Entities.PlatformConfig?)null);

        Assert.False(await _service.IsDeletionEnabledAsync());
    }
}
