using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Services;

namespace Portal.Web.Controllers;

/// <summary>
/// SuperAdmin platform-wide storage usage: every business with its plan/status and storage used
/// per category. Read-only (Phase 1 — visibility only). Sortable by name or total size.
/// </summary>
[Authorize(Roles = "SuperAdmin")]
[Route("Admin/Storage")]
public class AdminStorageController : Controller
{
    private readonly IStorageUsageService _storageUsageService;
    private readonly ISignatureService _signatureService;

    public AdminStorageController(IStorageUsageService storageUsageService, ISignatureService signatureService)
    {
        _storageUsageService = storageUsageService;
        _signatureService = signatureService;
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
}
