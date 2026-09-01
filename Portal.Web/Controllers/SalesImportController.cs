using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
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
    private readonly IExternalPlatformService _externalPlatformService;
    private readonly IImportTemplateService _templateService;
    private readonly ExternalSalesRecordRepository _recordRepository;
    private readonly ICurrentTenantService _tenantService;
    private readonly IBusinessService _businessService;
    private readonly IMemoryCache _cache;

    public SalesImportController(
        ISalesImportService importService,
        IRevenueSourceService revenueSourceService,
        IExternalPlatformService externalPlatformService,
        IImportTemplateService templateService,
        ExternalSalesRecordRepository recordRepository,
        ICurrentTenantService tenantService,
        IBusinessService businessService,
        IMemoryCache cache)
    {
        _importService = importService;
        _revenueSourceService = revenueSourceService;
        _externalPlatformService = externalPlatformService;
        _templateService = templateService;
        _recordRepository = recordRepository;
        _tenantService = tenantService;
        _businessService = businessService;
        _cache = cache;
    }

    // ════════════════════════════════════════════
    // IMPORT FLOW
    // ════════════════════════════════════════════

    [HttpGet]
    [ModuleAccess(PortalModules.ExternalPlatformImport)]
    public async Task<IActionResult> Index()
    {
        // The Index page is the "Import Platform Sales" screen — driven by registered external
        // platforms, not POS revenue sources. Gated by the external_platform_import module.
        ViewData["ExternalPlatforms"] = await _externalPlatformService.GetActiveAsync();

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

            var cacheKey = $"SalesImport_{_tenantService.CurrentBusinessId}_{DateTime.UtcNow.Ticks}";
            _cache.Set(cacheKey, result.Data, TimeSpan.FromMinutes(30));

            return Json(new { success = true, cacheKey });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to process file." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ModuleAccess(PortalModules.ExternalPlatformImport)]
    public async Task<IActionResult> AxPostParseFileForPlatform(IFormFile file, int externalPlatformId)
    {
        try
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "Please select a file to upload." });

            // Buffer into a seekable MemoryStream so the service can validate the header
            // and then re-read the rows from the start.
            using var stream = new MemoryStream();
            using (var upload = file.OpenReadStream())
            {
                await upload.CopyToAsync(stream);
            }
            stream.Position = 0;

            var result = await _importService.ParseAndPreviewForPlatformAsync(stream, file.FileName, externalPlatformId);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            var cacheKey = $"SalesImport_{_tenantService.CurrentBusinessId}_{DateTime.UtcNow.Ticks}";
            _cache.Set(cacheKey, result.Data, TimeSpan.FromMinutes(30));

            return Json(new { success = true, cacheKey });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to process file." });
        }
    }

    [HttpGet]
    [ModuleAccess(PortalModules.ExternalPlatformImport)]
    public async Task<IActionResult> AxGetDownloadTemplate(string format = "csv", int? externalPlatformId = null)
    {
        try
        {
            string? platformCode = null;
            if (externalPlatformId.HasValue)
            {
                var platform = await _externalPlatformService.GetByIdAsync(externalPlatformId.Value);
                platformCode = platform?.PlatformCode; // null falls back to placeholder in the service
            }

            var (content, fileName, contentType) = string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase)
                ? _templateService.BuildExcelTemplate(platformCode)
                : _templateService.BuildCsvTemplate(platformCode);

            return File(content, contentType, fileName);
        }
        catch (Exception ex)
        {
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public IActionResult Preview(string cacheKey)
    {
        if (string.IsNullOrEmpty(cacheKey) || !_cache.TryGetValue(cacheKey, out SalesImportPreview? preview) || preview == null)
            return RedirectToAction("Index");

        ViewData["CacheKey"] = cacheKey;
        return View(preview);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostConfirmImport([FromBody] SalesConfirmRequest? request)
    {
        try
        {
            if (string.IsNullOrEmpty(request?.CacheKey) ||
                !_cache.TryGetValue(request.CacheKey, out SalesImportPreview? preview) || preview == null)
            {
                return Json(new { success = false, message = "Import session expired. Please upload the file again." });
            }

            var result = preview.ExternalPlatformId.HasValue
                ? await _importService.ConfirmImportForPlatformAsync(preview, request?.ExcludeRowIndexes)
                : await _importService.ConfirmImportAsync(preview, request?.ExcludeRowIndexes);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            _cache.Remove(request.CacheKey);

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
    public async Task<IActionResult> Records(int? sourceId, DateOnly? dateFrom, DateOnly? dateTo, int? platformId, int page = 1)
    {
        if (!await IsZReportEnabledAsync())
            return RedirectToAction("Dashboard", "Revenue");

        var businessId = _tenantService.CurrentBusinessId;
        int pageSize = 15;
        int offset = (page - 1) * pageSize;

        var (items, totalCount) = await _recordRepository.GetPagedAsync(
            businessId, sourceId, dateFrom, dateTo, null, offset, pageSize, includeInactive: true,
            externalPlatformId: platformId);

        ViewData["CurrentPage"] = page;
        ViewData["TotalPages"] = (int)Math.Ceiling((double)totalCount / pageSize);
        ViewData["TotalCount"] = totalCount;
        ViewData["PageSize"] = pageSize;
        ViewData["HasPreviousPage"] = page > 1;
        ViewData["HasNextPage"] = page * pageSize < totalCount;
        ViewData["SourceId"] = sourceId;
        ViewData["PlatformId"] = platformId;
        ViewData["DateFrom"] = dateFrom?.ToString("yyyy-MM-dd");
        ViewData["DateTo"] = dateTo?.ToString("yyyy-MM-dd");

        var sources = await _revenueSourceService.GetActiveAsync();
        ViewData["RevenueSources"] = sources;

        // Platform lookup for the filter + name display. Keyed by Id for the view.
        var platforms = await _externalPlatformService.GetAllAsync(includeInactive: true);
        ViewData["ExternalPlatforms"] = platforms;
        ViewData["ExternalPlatformNameMap"] = platforms.ToDictionary(p => p.Id, p => $"{p.Name} ({p.PlatformCode})");

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
    public string? CacheKey { get; set; }
    public List<int>? ExcludeRowIndexes { get; set; }
}
