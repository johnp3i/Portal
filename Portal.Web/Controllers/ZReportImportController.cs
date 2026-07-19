using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public ZReportImportController(
        IZReportImportService importService,
        IRevenueSourceService revenueSourceService,
        ICurrentTenantService tenantService,
        IBusinessService businessService)
    {
        _importService = importService;
        _revenueSourceService = revenueSourceService;
        _tenantService = tenantService;
        _businessService = businessService;
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

            // Store preview in TempData for the Preview page
            TempData["ZReportImportPreview"] = JsonSerializer.Serialize(result.Data);

            return Json(new { success = true, message = "File parsed successfully." });
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
    public IActionResult Preview()
    {
        var previewJson = TempData["ZReportImportPreview"] as string;
        if (string.IsNullOrEmpty(previewJson))
            return RedirectToAction("Index");

        var preview = JsonSerializer.Deserialize<ZReportImportPreview>(previewJson);
        if (preview == null)
            return RedirectToAction("Index");

        // Keep it in TempData for the confirm action
        TempData["ZReportImportPreview"] = previewJson;

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
            var previewJson = TempData["ZReportImportPreview"] as string;
            if (string.IsNullOrEmpty(previewJson))
                return Json(new { success = false, message = "Import session expired. Please upload the file again." });

            var preview = JsonSerializer.Deserialize<ZReportImportPreview>(previewJson);
            if (preview == null)
                return Json(new { success = false, message = "Invalid import session." });

            var result = await _importService.ConfirmImportAsync(preview, request?.ExcludeGroupIndexes);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

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
    public List<int>? ExcludeGroupIndexes { get; set; }
}
