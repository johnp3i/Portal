using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

/// <summary>
/// Handles Sales Invoice Import (transaction-level POS data) into ExternalSalesRecord.
/// Gated to Professional tier via the 'zreport_import' plan feature.
/// </summary>
[Authorize]
[ModuleAccess(PortalModules.ZReportImport)]
public class SalesImportController : Controller
{
    private readonly ISalesImportService _importService;
    private readonly IRevenueSourceService _revenueSourceService;
    private readonly ExternalSalesRecordRepository _recordRepository;
    private readonly ICurrentTenantService _tenantService;
    private readonly IBusinessService _businessService;

    public SalesImportController(
        ISalesImportService importService,
        IRevenueSourceService revenueSourceService,
        ExternalSalesRecordRepository recordRepository,
        ICurrentTenantService tenantService,
        IBusinessService businessService)
    {
        _importService = importService;
        _revenueSourceService = revenueSourceService;
        _recordRepository = recordRepository;
        _tenantService = tenantService;
        _businessService = businessService;
    }

    // ════════════════════════════════════════════
    // IMPORT FLOW
    // ════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!await IsZReportEnabledAsync())
            return RedirectToAction("Dashboard", "Revenue");

        var sources = await _revenueSourceService.GetActiveAsync();
        ViewData["RevenueSources"] = sources;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostParseFile(IFormFile file, int? revenueSourceId)
    {
        try
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "Please select a file to upload." });

            using var stream = file.OpenReadStream();
            var result = await _importService.ParseAndPreviewAsync(stream, file.FileName, revenueSourceId);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            TempData["SalesImportPreview"] = JsonSerializer.Serialize(result.Data);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to process file." });
        }
    }

    [HttpGet]
    public IActionResult Preview()
    {
        var json = TempData["SalesImportPreview"] as string;
        if (string.IsNullOrEmpty(json))
            return RedirectToAction("Index");

        var preview = JsonSerializer.Deserialize<SalesImportPreview>(json);
        if (preview == null)
            return RedirectToAction("Index");

        TempData["SalesImportPreview"] = json;
        return View(preview);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostConfirmImport([FromBody] SalesConfirmRequest? request)
    {
        try
        {
            var json = TempData["SalesImportPreview"] as string;
            if (string.IsNullOrEmpty(json))
                return Json(new { success = false, message = "Import session expired. Please upload the file again." });

            var preview = JsonSerializer.Deserialize<SalesImportPreview>(json);
            if (preview == null)
                return Json(new { success = false, message = "Invalid import session." });

            var result = await _importService.ConfirmImportAsync(preview, request?.ExcludeRowIndexes);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            return Json(new
            {
                success = true,
                message = $"{result.Data!.ImportedCount} sales record(s) imported. Total: €{result.Data.TotalAmount:N2}",
                data = result.Data
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Import failed. No records were created." });
        }
    }

    // ════════════════════════════════════════════
    // SALES RECORDS LIST
    // ════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> Records(int? sourceId, DateOnly? dateFrom, DateOnly? dateTo, int page = 1)
    {
        if (!await IsZReportEnabledAsync())
            return RedirectToAction("Dashboard", "Revenue");

        var businessId = _tenantService.CurrentBusinessId;
        int pageSize = 15;
        int offset = (page - 1) * pageSize;

        var (items, totalCount) = await _recordRepository.GetPagedAsync(
            businessId, sourceId, dateFrom, dateTo, null, offset, pageSize, includeInactive: true);

        ViewData["CurrentPage"] = page;
        ViewData["TotalPages"] = (int)Math.Ceiling((double)totalCount / pageSize);
        ViewData["TotalCount"] = totalCount;
        ViewData["PageSize"] = pageSize;
        ViewData["HasPreviousPage"] = page > 1;
        ViewData["HasNextPage"] = page * pageSize < totalCount;
        ViewData["SourceId"] = sourceId;
        ViewData["DateFrom"] = dateFrom?.ToString("yyyy-MM-dd");
        ViewData["DateTo"] = dateTo?.ToString("yyyy-MM-dd");

        var sources = await _revenueSourceService.GetActiveAsync();
        ViewData["RevenueSources"] = sources;

        return View(new PagedResult<Portal.Infrastructure.Entities.ExternalSalesRecord>
        {
            Items = items,
            CurrentPage = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCancelRecord([FromForm] int id)
    {
        try
        {
            await _recordRepository.SoftDeleteAsync(id, _tenantService.CurrentBusinessId);
            return Json(new { success = true, message = "Record cancelled." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to cancel record." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostRestoreRecord([FromForm] int id)
    {
        try
        {
            await _recordRepository.RestoreAsync(id, _tenantService.CurrentBusinessId);
            return Json(new { success = true, message = "Record restored." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to restore record." });
        }
    }

    private async Task<bool> IsZReportEnabledAsync()
    {
        var profile = await _businessService.GetBusinessProfileAsync(_tenantService.CurrentBusinessId);
        return profile?.IsZReportEnabled ?? false;
    }
}

public class SalesConfirmRequest
{
    public List<int>? ExcludeRowIndexes { get; set; }
}
