using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Entities.Storage;
using Portal.Infrastructure.Models.Storage;
using Portal.Infrastructure.Services;
using Portal.Web.Models;

namespace Portal.Web.Controllers;

/// <summary>
/// SuperAdmin platform-wide storage usage: every business with its plan/status and storage used
/// per category (Phase 1). Also hosts the orphaned-file cleanup admin surface (Phase 4b-1):
/// the "Upcoming for deletion" page and the deletion report. Report-only — no files are deleted.
/// </summary>
[Authorize(Roles = "SuperAdmin")]
[Route("Admin/Storage")]
public class AdminStorageController : Controller
{
    private readonly IStorageUsageService _storageUsageService;
    private readonly ISignatureService _signatureService;
    private readonly IOrphanedFileCleanupService _cleanupService;
    private readonly IBusinessTimeZoneService _timeZoneService;
    private readonly ICurrentTenantService _tenantService;

    public AdminStorageController(
        IStorageUsageService storageUsageService,
        ISignatureService signatureService,
        IOrphanedFileCleanupService cleanupService,
        IBusinessTimeZoneService timeZoneService,
        ICurrentTenantService tenantService)
    {
        _storageUsageService = storageUsageService;
        _signatureService = signatureService;
        _cleanupService = cleanupService;
        _timeZoneService = timeZoneService;
        _tenantService = tenantService;
    }

    // GET /Admin/Storage
    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, string? plan, string sortBy = "size", string sortDir = "desc")
    {
        var model = await _storageUsageService.GetPlatformStorageAsync(search, plan, sortBy, sortDir);
        return View(model);
    }

    // POST /Admin/Storage/BackfillSignatureSizes
    // One-time pass to measure on-disk sizes for legacy signatures (FileSizeBytes = 0).
    [HttpPost("BackfillSignatureSizes")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostBackfillSignatureSizes()
    {
        try
        {
            var (updated, skipped) = await _signatureService.BackfillFileSizesAsync();
            return Json(new
            {
                success = true,
                message = $"Signature sizes backfilled: {updated} updated, {skipped} skipped (missing/empty on disk)."
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "The backfill could not be completed. Please try again." });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // ORPHANED-FILE CLEANUP (Phase 4b-1 — report-only)
    // ═══════════════════════════════════════════════════════════

    // GET /Admin/Storage/Cleanup — "Upcoming for deletion"
    [HttpGet("Cleanup")]
    public async Task<IActionResult> Cleanup()
    {
        var tz = await _timeZoneService.GetTimeZoneAsync(_tenantService.CurrentBusinessId);

        var candidates = await _cleanupService.GetCandidatesAsync();
        var preview = await _cleanupService.PreviewDeletionAsync();
        var model = new StorageCleanupViewModel
        {
            CleanupEnabled = await _cleanupService.IsEnabledAsync(),
            DeletionEnabled = await _cleanupService.IsDeletionEnabledAsync(),
            DueCount = preview.DueCount,
            DueBytes = preview.TotalBytes,
            GraceDays = await _cleanupService.GetGraceDaysAsync(),
            StatusTypes = await _cleanupService.GetStatusTypesAsync(),
            TimeZoneLabel = tz.Id,
            Candidates = candidates.Select(c => new StorageCleanupCandidateItem
            {
                Id = c.Id,
                RelativePath = c.RelativePath,
                BusinessId = c.BusinessId,
                FileSizeBytes = c.FileSizeBytes,
                DetectedAtLocal = ToLocal(c.DetectedAtUtc, tz),
                ScheduledDeletionAtLocal = ToLocal(c.ScheduledDeletionAtUtc, tz),
                StatusTypeId = c.OrphanedFileStatusTypeId,
                StatusName = c.StatusName,
                StatusDescription = c.StatusDescription
            }).ToList()
        };

        return View(model);
    }

    // GET /Admin/Storage/DeletionReport — permanent deletion history (empty until Phase 4b-2)
    [HttpGet("DeletionReport")]
    public async Task<IActionResult> DeletionReport()
    {
        var tz = await _timeZoneService.GetTimeZoneAsync(_tenantService.CurrentBusinessId);
        var log = await _cleanupService.GetDeletionLogAsync();

        ViewBag.TimeZoneLabel = tz.Id;
        ViewBag.Rows = log.Select(r => new StorageDeletionReportItem
        {
            RelativePath = r.RelativePath,
            BusinessId = r.BusinessId,
            FileSizeBytes = r.FileSizeBytes,
            Reason = r.Reason,
            DeletedAtLocal = ToLocal(r.DeletedAtUtc, tz)
        }).ToList();

        return View();
    }

    [HttpPost("Cleanup/ScanNow")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostScanNow()
    {
        try
        {
            var result = await _cleanupService.ScanAsync();
            return Json(new
            {
                success = true,
                message = $"Scan complete: {result.FilesScanned} file(s) scanned, {result.OrphansFound} orphan candidate(s) recorded. No files were deleted (report-only)."
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "The scan could not be completed. Please try again." });
        }
    }

    [HttpPost("Cleanup/Cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCancelCandidate(int id)
    {
        try
        {
            await _cleanupService.CancelAsync(id);
            return Json(new { success = true, message = "File excluded from deletion." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Could not cancel this candidate." });
        }
    }

    [HttpPost("Cleanup/Pause")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostPauseCandidate(int id)
    {
        try
        {
            await _cleanupService.PauseAsync(id);
            return Json(new { success = true, message = "Deletion paused for this file." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Could not pause this candidate." });
        }
    }

    [HttpPost("Cleanup/Resume")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostResumeCandidate(int id)
    {
        try
        {
            await _cleanupService.ResumeAsync(id);
            return Json(new { success = true, message = "File returned to the deletion queue." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Could not resume this candidate." });
        }
    }

    [HttpPost("Cleanup/SetEnabled")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostSetCleanupEnabled(bool enabled)
    {
        try
        {
            await _cleanupService.SetEnabledAsync(enabled);
            return Json(new { success = true, message = enabled ? "Automatic cleanup enabled." : "Automatic cleanup disabled." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Could not update the setting." });
        }
    }

    [HttpPost("Cleanup/SetGraceDays")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostSetGraceDays(int graceDays)
    {
        try
        {
            if (graceDays < 1)
                return Json(new { success = false, message = "Grace period must be at least 1 day." });

            await _cleanupService.SetGraceDaysAsync(graceDays);
            return Json(new { success = true, message = $"Grace period set to {graceDays} day(s). Applies to future scans." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Could not update the grace period." });
        }
    }

    [HttpPost("Cleanup/SetDeletionEnabled")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostSetDeletionEnabled(bool enabled)
    {
        try
        {
            await _cleanupService.SetDeletionEnabledAsync(enabled);
            return Json(new
            {
                success = true,
                message = enabled
                    ? "Automatic deletion enabled. Orphaned files past their scheduled date will now be permanently removed."
                    : "Automatic deletion disabled. Files will be detected but not removed."
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Could not update the setting." });
        }
    }

    [HttpPost("Cleanup/RunDeletionNow")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostRunDeletionNow()
    {
        try
        {
            var result = await _cleanupService.RunCleanupAsync();
            return Json(new
            {
                success = true,
                message = $"Deletion complete: {result.Deleted} file(s) removed, {result.Skipped} skipped, {StorageFormat.Bytes(result.BytesFreed)} freed."
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "The deletion run could not be completed. Please try again." });
        }
    }

    private static DateTime ToLocal(DateTime utc, TimeZoneInfo tz) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);
}

/// <summary>One row on the deletion report (deleted-at already in local time).</summary>
public class StorageDeletionReportItem
{
    public string RelativePath { get; set; } = null!;
    public int? BusinessId { get; set; }
    public long FileSizeBytes { get; set; }
    public string Reason { get; set; } = null!;
    public DateTime DeletedAtLocal { get; set; }

    public string SizeDisplay => StorageFormat.Bytes(FileSizeBytes);
    public string BusinessDisplay => BusinessId?.ToString() ?? "—";
}
