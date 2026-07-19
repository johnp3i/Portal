using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

/// <summary>
/// Handles Revenue Source management and Z-Report manual entry.
/// Part of the Revenue Ingestion feature (Phase 1).
/// </summary>
[Authorize]
[ModuleAccess(PortalModules.Revenue)]
public class ZReportController : Controller
{
    private readonly IRevenueSourceService _revenueSourceService;
    private readonly IRevenueSummaryService _revenueSummaryService;
    private readonly ICurrentTenantService _tenantService;
    private readonly IBusinessService _businessService;
    private readonly VatSubmissionPeriodRepository _vatPeriodRepository;

    public ZReportController(
        IRevenueSourceService revenueSourceService,
        IRevenueSummaryService revenueSummaryService,
        ICurrentTenantService tenantService,
        IBusinessService businessService,
        VatSubmissionPeriodRepository vatPeriodRepository)
    {
        _revenueSourceService = revenueSourceService;
        _revenueSummaryService = revenueSummaryService;
        _tenantService = tenantService;
        _businessService = businessService;
        _vatPeriodRepository = vatPeriodRepository;
    }

    // ════════════════════════════════════════════
    // PAGE ACTIONS — Revenue Sources
    // ════════════════════════════════════════════

    /// <summary>
    /// Revenue Sources management page (list with create/edit/toggle modals).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Sources()
    {
        if (!await IsZReportEnabledAsync())
            return RedirectToAction("Dashboard", "Revenue");

        var sources = await _revenueSourceService.GetAllAsync();
        return View(sources);
    }

    // ════════════════════════════════════════════
    // PAGE ACTIONS — Z-Reports
    // ════════════════════════════════════════════

    /// <summary>
    /// Z-Reports list page with filtering and pagination.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(int? sourceId, DateOnly? dateFrom, DateOnly? dateTo, string? zNumber, string dateMode = "period", int page = 1)
    {
        if (!await IsZReportEnabledAsync())
            return RedirectToAction("Dashboard", "Revenue");

        var pagedResult = await _revenueSummaryService.GetPagedAsync(sourceId, dateFrom, dateTo, zNumber, page, 15, dateMode);

        ViewData["CurrentPage"] = pagedResult.CurrentPage;
        ViewData["TotalPages"] = pagedResult.TotalPages;
        ViewData["TotalCount"] = pagedResult.TotalCount;
        ViewData["PageSize"] = pagedResult.PageSize;
        ViewData["HasPreviousPage"] = pagedResult.HasPreviousPage;
        ViewData["HasNextPage"] = pagedResult.HasNextPage;

        // Pass filter values back to view
        ViewData["SourceId"] = sourceId;
        ViewData["DateFrom"] = dateFrom?.ToString("yyyy-MM-dd");
        ViewData["DateTo"] = dateTo?.ToString("yyyy-MM-dd");
        ViewData["ZNumber"] = zNumber;
        ViewData["DateMode"] = dateMode;

        // Sources for dropdown
        var sources = await _revenueSourceService.GetActiveAsync();
        ViewData["RevenueSources"] = sources;

        // VAT periods for labels in the list
        var vatPeriods = await _vatPeriodRepository.GetAllByBusinessIdAsync(_tenantService.CurrentBusinessId);
        ViewData["VatPeriods"] = vatPeriods;

        return View(pagedResult);
    }

    /// <summary>
    /// Z-Report create/edit form page.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Entry(int? id = null)
    {
        if (!await IsZReportEnabledAsync())
            return RedirectToAction("Dashboard", "Revenue");

        var sources = await _revenueSourceService.GetActiveAsync();
        ViewData["RevenueSources"] = sources;

        // Load unsubmitted VAT periods for the dropdown
        var businessId = _tenantService.CurrentBusinessId;
        var vatPeriods = await _vatPeriodRepository.GetAllByBusinessIdAsync(businessId);
        ViewData["VatPeriods"] = vatPeriods;

        if (id.HasValue)
        {
            var summary = await _revenueSummaryService.GetByIdAsync(id.Value);
            if (summary == null)
                return NotFound();

            var lines = await _revenueSummaryService.GetLinesAsync(id.Value);
            ViewData["Lines"] = lines;
            ViewData["IsEdit"] = true;

            // Check if assigned to a submitted period (locked)
            var isLocked = await _revenueSummaryService.IsLockedAsync(id.Value);
            ViewData["IsLocked"] = isLocked;

            return View(summary);
        }

        ViewData["IsEdit"] = false;
        ViewData["IsLocked"] = false;
        return View(new RevenueSummary());
    }

    // ════════════════════════════════════════════
    // AJAX ENDPOINTS — Revenue Sources
    // ════════════════════════════════════════════

    /// <summary>
    /// Creates a new revenue source.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreateSource([FromForm] string name, [FromForm] string? description)
    {
        try
        {
            var source = new RevenueSource
            {
                Name = name?.Trim() ?? "",
                Description = description?.Trim()
            };

            var result = await _revenueSourceService.CreateAsync(source);
            return Json(new { success = result.Success, message = result.Success ? "Revenue source created successfully." : result.Message, id = result.Id });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    /// <summary>
    /// Updates an existing revenue source.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateSource([FromForm] int id, [FromForm] string name, [FromForm] string? description)
    {
        try
        {
            var source = new RevenueSource
            {
                Id = id,
                Name = name?.Trim() ?? "",
                Description = description?.Trim()
            };

            var result = await _revenueSourceService.UpdateAsync(source);
            return Json(new { success = result.Success, message = result.Success ? "Revenue source updated successfully." : result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    /// <summary>
    /// Toggles the active status of a revenue source.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostToggleSource([FromForm] int id, [FromForm] bool isActive)
    {
        try
        {
            var result = await _revenueSourceService.ToggleActiveAsync(id, isActive);
            return Json(new { success = result.Success, message = result.Success ? (isActive ? "Revenue source activated." : "Revenue source deactivated.") : result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    // ════════════════════════════════════════════
    // AJAX ENDPOINTS — Z-Reports
    // ════════════════════════════════════════════

    /// <summary>
    /// Creates a new Z-Report with VAT lines.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreateZReport([FromBody] ZReportFormModel model)
    {
        try
        {
            if (model == null)
                return Json(new { success = false, message = "Invalid request data." });

            var summary = new RevenueSummary
            {
                RevenueSourceId = model.RevenueSourceId,
                SummaryDate = model.SummaryDate,
                PeriodEndDate = model.PeriodEndDate,
                ZReportNumber = model.ZReportNumber?.Trim(),
                TotalDiscount = model.TotalDiscount,
                TransactionCount = model.TransactionCount,
                Reference = model.Reference?.Trim(),
                Notes = model.Notes?.Trim(),
                ExportedAtUtc = model.ExportedAtUtc,
                VatSubmissionPeriodId = model.VatSubmissionPeriodId
            };

            var lines = model.Lines?.Select(l => new RevenueSummaryLine
            {
                VatRate = l.VatRate,
                NetAmount = l.NetAmount,
                VatAmount = l.VatAmount,
                DiscountAmount = l.DiscountAmount,
                Description = l.Description?.Trim()
            }).ToList() ?? new List<RevenueSummaryLine>();

            var result = await _revenueSummaryService.CreateAsync(summary, lines);
            return Json(new { success = result.Success, message = result.Success ? "Z-Report created successfully." : result.Message, id = result.Id });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    /// <summary>
    /// Updates an existing Z-Report with VAT lines.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateZReport([FromBody] ZReportFormModel model)
    {
        try
        {
            if (model == null || model.Id <= 0)
                return Json(new { success = false, message = "Invalid request data." });

            var summary = new RevenueSummary
            {
                Id = model.Id,
                RevenueSourceId = model.RevenueSourceId,
                SummaryDate = model.SummaryDate,
                PeriodEndDate = model.PeriodEndDate,
                ZReportNumber = model.ZReportNumber?.Trim(),
                TotalDiscount = model.TotalDiscount,
                TransactionCount = model.TransactionCount,
                Reference = model.Reference?.Trim(),
                Notes = model.Notes?.Trim(),
                ExportedAtUtc = model.ExportedAtUtc,
                VatSubmissionPeriodId = model.VatSubmissionPeriodId
            };

            var lines = model.Lines?.Select(l => new RevenueSummaryLine
            {
                VatRate = l.VatRate,
                NetAmount = l.NetAmount,
                VatAmount = l.VatAmount,
                DiscountAmount = l.DiscountAmount,
                Description = l.Description?.Trim()
            }).ToList() ?? new List<RevenueSummaryLine>();

            var result = await _revenueSummaryService.UpdateAsync(summary, lines);
            return Json(new { success = result.Success, message = result.Success ? "Z-Report updated successfully." : result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    /// <summary>
    /// Soft-deletes a Z-Report.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostDeleteZReport([FromForm] int id)
    {
        try
        {
            var result = await _revenueSummaryService.DeleteAsync(id);
            return Json(new { success = result.Success, message = result.Success ? "Z-Report cancelled successfully." : result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    /// <summary>
    /// Restores a cancelled Z-Report.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostRestoreZReport([FromForm] int id)
    {
        try
        {
            var result = await _revenueSummaryService.RestoreAsync(id);
            return Json(new { success = result.Success, message = result.Success ? "Z-Report restored successfully." : result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    /// <summary>
    /// Gets a Z-Report with its lines for editing (returns JSON).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> AxGetZReportDetail(int id)
    {
        try
        {
            var summary = await _revenueSummaryService.GetByIdAsync(id);
            if (summary == null)
                return Json(new { success = false, message = "Z-Report not found." });

            var lines = await _revenueSummaryService.GetLinesAsync(id);

            return Json(new
            {
                success = true,
                data = new
                {
                    summary.Id,
                    summary.RevenueSourceId,
                    SummaryDate = summary.SummaryDate.ToString("yyyy-MM-dd"),
                    PeriodEndDate = summary.PeriodEndDate?.ToString("yyyy-MM-dd"),
                    summary.ZReportNumber,
                    summary.TotalNet,
                    summary.TotalVat,
                    summary.TotalGross,
                    summary.TotalDiscount,
                    summary.TransactionCount,
                    summary.Reference,
                    summary.Notes,
                    ExportedAtUtc = summary.ExportedAtUtc?.ToString("yyyy-MM-ddTHH:mm"),
                    Lines = lines.Select(l => new
                    {
                        l.Id,
                        l.VatRate,
                        l.NetAmount,
                        l.VatAmount,
                        l.TotalAmount,
                        l.DiscountAmount,
                        l.Description
                    })
                }
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    // ════════════════════════════════════════════
    // PRIVATE HELPERS
    // ════════════════════════════════════════════

    private async Task<bool> IsZReportEnabledAsync()
    {
        var profile = await _businessService.GetBusinessProfileAsync(_tenantService.CurrentBusinessId);
        return profile?.IsZReportEnabled ?? false;
    }
}

/// <summary>
/// Form model for Z-Report create/update AJAX requests.
/// </summary>
public class ZReportFormModel
{
    public int Id { get; set; }
    public int RevenueSourceId { get; set; }
    public DateOnly SummaryDate { get; set; }
    public DateOnly? PeriodEndDate { get; set; }
    public string? ZReportNumber { get; set; }
    public decimal? TotalDiscount { get; set; }
    public int? TransactionCount { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public DateTime? ExportedAtUtc { get; set; }
    public int? VatSubmissionPeriodId { get; set; }
    public List<ZReportLineModel>? Lines { get; set; }
}

/// <summary>
/// Line item model for Z-Report VAT breakdown.
/// </summary>
public class ZReportLineModel
{
    public decimal VatRate { get; set; }
    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal? DiscountAmount { get; set; }
    public string? Description { get; set; }
}
