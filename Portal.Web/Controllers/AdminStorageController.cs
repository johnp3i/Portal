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

    public AdminStorageController(IStorageUsageService storageUsageService)
    {
        _storageUsageService = storageUsageService;
    }

    // GET /Admin/Storage
    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, string? plan, string sortBy = "size", string sortDir = "desc")
    {
        var model = await _storageUsageService.GetPlatformStorageAsync(search, plan, sortBy, sortDir);
        return View(model);
    }
}
