using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Services;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

/// <summary>
/// Handles Z-Report bulk import from CSV files.
/// Gated to Professional tier via the 'zreport_import' plan feature.
/// </summary>
[Authorize]
[ModuleAccess(PortalModules.ZReportImport)]
public class ZReportImportController : Controller
{
    private readonly IZReportImportService _importService;
    private readonly IRevenueSourceService _revenueSourceService;
    private readonly ICurrentTenantService _tenantService;
    private readonly IBusinessService _businessService;
    private readonly IMemoryCache _cache;

    public ZReportImportController(
        IZReportImportService importService,
        IRevenueSourceService revenueSourceService,
        ICurrentTenantService tenantService,
        IBusinessService businessService,
        IMemoryCache cache)
    {
        _importService = importService;
        _revenueSourceService = revenueSourceService;
        _tenantService = tenantService;
        _businessService = businessService;
        _cache = cache;
    }

    /// <summary>
    /// Upload page — select Revenue Source and upload CSV file.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!await IsZReportEnabledAsync())
            return RedirectToAction("Dashboard", "Revenue");

        var sources = await _revenueSourceService.GetActiveAsync();
        ViewData["RevenueSources"] = sources;
        return View();
    }

    /// <summary>
    /// Downloads a sample CSV template for Z-Report import.
    /// </summary>
    [HttpGet]
    public IActionResult DownloadTemplate()
    {
        var csv = "Date From,Date To,Z-Number,VAT Rate,Net Sales,VAT Amount,Discount,Export Date\n" +
                  "01/07/2026,01/07/2026,78001,5,4200.00,210.00,50.00,02/07/2026 08:15\n" +
                  "01/07/2026,01/07/2026,78001,9,380.00,34.20,10.00,02/07/2026 08:15\n" +
                  "02/07/2026,02/07/2026,78002,5,3800.00,190.00,40.00,03/07/2026 08:00\n" +
                  "01/07/2026,31/07/2026,MONTHLY-JUL,5,19000.00,950.00,,01/08/2026 10:00\n" +
                  "01/07/2026,31/07/2026,MONTHLY-JUL,9,1200.00,108.00,,01/08/2026 10:00\n";

        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv", "zreport-import-template.csv");
    }

    /// <summary>
    /// Parses uploaded CSV and returns preview data.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostParseFile(IFormFile file, int revenueSourceId)
    {
        try
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "Please select a file to upload." });

            using var stream = file.OpenReadStream();
            var result = await _importService.ParseAndPreviewAsync(stream, file.FileName, revenueSourceId);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            // Store preview in memory cache (keyed by business + timestamp, expires in 30 min)
            var cacheKey = $"ZReportImport_{_tenantService.CurrentBusinessId}_{DateTime.UtcNow.Ticks}";
            _cache.Set(cacheKey, result.Data, TimeSpan.FromMinutes(30));

            return Json(new { success = true, message = "File parsed successfully.", cacheKey });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to process file. Please try again." });
        }
    }

    /// <summary>
    /// Preview page — shows grouped Z-Reports for confirmation.
    /// </summary>
    [HttpGet]
    public IActionResult Preview(string cacheKey)
    {
        if (string.IsNullOrEmpty(cacheKey) || !_cache.TryGetValue(cacheKey, out ZReportImportPreview? preview) || preview == null)
            return RedirectToAction("Index");

        ViewData["CacheKey"] = cacheKey;
        return View(preview);
    }

    /// <summary>
    /// Confirms the import and inserts all Z-Reports.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostConfirmImport([FromBody] ConfirmImportRequest? request)
    {
        try
        {
            if (string.IsNullOrEmpty(request?.CacheKey) ||
                !_cache.TryGetValue(request.CacheKey, out ZReportImportPreview? preview) || preview == null)
            {
                return Json(new { success = false, message = "Import session expired. Please upload the file again." });
            }

            var result = await _importService.ConfirmImportAsync(preview, request?.ExcludeGroupIndexes);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            // Remove from cache after successful import
            _cache.Remove(request.CacheKey);

            return Json(new
            {
                success = true,
                message = $"{result.Data!.ImportedCount} Z-Report(s) imported successfully. Total: €{result.Data.TotalGross:N2}",
                data = result.Data
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Import failed. No records were created. Please try again." });
        }
    }

    private async Task<bool> IsZReportEnabledAsync()
    {
        var profile = await _businessService.GetBusinessProfileAsync(_tenantService.CurrentBusinessId);
        return profile?.IsZReportEnabled ?? false;
    }
}

public class ConfirmImportRequest
{
    public string? CacheKey { get; set; }
    public List<int>? ExcludeGroupIndexes { get; set; }
}
