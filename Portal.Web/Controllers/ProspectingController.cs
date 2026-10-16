using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Models.Sales;
using Portal.Infrastructure.Services.Sales;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

/// <summary>
/// Prospects & Campaigns — a complementary sub-area under Opportunities that replaces the
/// prospecting Excel. A Prospect is NOT a Sales Contact; it only enters the pipeline through
/// Convert-to-Lead. Gated by the Sales module, same as the rest of Opportunities.
/// </summary>
[Authorize]
[ModuleAccess(PortalModules.Sales)]
public class ProspectingController : Controller
{
    private readonly IProspectCampaignService _campaignService;
    private readonly IProspectService _prospectService;
    private readonly IProspectActivityLogService _activityLogService;
    private readonly IProspectImportService _importService;
    private readonly ISalesProductService _productService;
    private readonly ILogger<ProspectingController> _logger;

    public ProspectingController(
        IProspectCampaignService campaignService,
        IProspectService prospectService,
        IProspectActivityLogService activityLogService,
        IProspectImportService importService,
        ISalesProductService productService,
        ILogger<ProspectingController> logger)
    {
        _campaignService = campaignService;
        _prospectService = prospectService;
        _activityLogService = activityLogService;
        _importService = importService;
        _productService = productService;
        _logger = logger;
    }

    // Fixed page size for the activity log, consistent with other list views.
    private const int ActivityPageSize = 15;

    // Maps the quick-period chip name to an inclusive [from, to] date range (business-local "today"
    // is approximated with UtcNow.Date, consistent with how other brief/period features compute it).
    private static (DateOnly? From, DateOnly? To) ResolvePeriod(string? period)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        switch ((period ?? "last_7").Trim().ToLowerInvariant())
        {
            case "today": return (today, today);
            case "yesterday": var y = today.AddDays(-1); return (y, y);
            case "this_week":
                // Week starting Monday.
                var dow = (int)today.DayOfWeek; // Sun=0..Sat=6
                var daysFromMonday = (dow + 6) % 7;
                return (today.AddDays(-daysFromMonday), today);
            case "last_7": return (today.AddDays(-6), today);
            case "this_month": return (new DateOnly(today.Year, today.Month, 1), today);
            case "all": return (null, null);
            // Default to the last 7 days (matches the page's default chip).
            default: return (today.AddDays(-6), today);
        }
    }

    // Builds the activity filter from the raw query parameters, shared by the list endpoint and the
    // CSV export so both apply identical filtering. Explicit dates win over the quick-period chip.
    private ProspectActivityFilter BuildActivityFilter(
        int? campaignId, byte? activityType, byte? sentiment, string? city, string? search,
        string? period, DateTime? dateFrom, DateTime? dateTo)
    {
        DateOnly? from, to;
        if (dateFrom.HasValue || dateTo.HasValue)
        {
            from = dateFrom.HasValue ? DateOnly.FromDateTime(dateFrom.Value) : null;
            to = dateTo.HasValue ? DateOnly.FromDateTime(dateTo.Value) : null;
        }
        else
        {
            (from, to) = ResolvePeriod(period);
        }

        return new ProspectActivityFilter
        {
            CampaignId = campaignId,
            ActivityType = activityType,
            Sentiment = sentiment,
            City = string.IsNullOrWhiteSpace(city) ? null : city,
            Search = string.IsNullOrWhiteSpace(search) ? null : search,
            DateFrom = from,
            DateTo = to
        };
    }

    // ═══════════════════════════════════════════════════════════
    // PAGE ACTIONS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Smart landing for the sidebar "Prospecting" link. Working a campaign's prospect list is
    /// the daily action, so if there's exactly one active campaign we jump straight to its list;
    /// with zero or several active campaigns we show the campaigns list to pick.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Go()
    {
        var campaigns = await _campaignService.GetCampaignsAsync();
        var active = campaigns.Where(c => c.Status == 2).ToList(); // 2 = Active

        if (active.Count == 1)
            return RedirectToAction(nameof(Prospects), new { campaignId = active[0].Id });

        return RedirectToAction(nameof(Campaigns));
    }

    [HttpGet]
    public async Task<IActionResult> Campaigns()
    {
        var campaigns = await _campaignService.GetCampaignsAsync();
        ViewBag.Products = await _productService.GetActiveProductsAsync();
        return View(campaigns);
    }

    [HttpGet]
    public async Task<IActionResult> CampaignDashboard(int id)
    {
        var dashboard = await _campaignService.GetDashboardAsync(id);
        if (dashboard == null)
            return NotFound();

        ViewBag.Products = await _productService.GetActiveProductsAsync();
        return View(dashboard);
    }

    [HttpGet]
    public async Task<IActionResult> Prospects(int campaignId)
    {
        var dashboard = await _campaignService.GetDashboardAsync(campaignId);
        if (dashboard == null)
            return NotFound();

        return View(dashboard);
    }

    [HttpGet]
    public async Task<IActionResult> ProspectDetail(int id)
    {
        var detail = await _prospectService.GetProspectDetailAsync(id);
        if (detail == null)
            return NotFound();

        return View(detail);
    }

    [HttpGet]
    public async Task<IActionResult> Import(int campaignId)
    {
        var dashboard = await _campaignService.GetDashboardAsync(campaignId);
        if (dashboard == null)
            return NotFound();

        return View(dashboard);
    }

    /// <summary>
    /// Prospect Activity log — "what did we do" across prospecting, overall or for one campaign.
    /// Read-only. The AJAX endpoint feeds the table/summary; this action renders the shell and
    /// supplies the campaign dropdown + city options for the filters.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Activity(int? campaignId)
    {
        var campaigns = await _campaignService.GetCampaignsAsync();
        var cities = await _activityLogService.GetCitiesAsync(campaignId);

        ViewBag.Campaigns = campaigns;
        ViewBag.SelectedCampaignId = campaignId;
        ViewBag.Cities = cities;
        return View();
    }

    // ═══════════════════════════════════════════════════════════
    // CAMPAIGN AJAX ENDPOINTS
    // ═══════════════════════════════════════════════════════════

    /// <summary>Paged, filtered prospect activity log + period summary (read-only, tenant-scoped).</summary>
    [HttpGet]
    public async Task<IActionResult> AxGetProspectActivity(
        int? campaignId, byte? activityType, byte? sentiment, string? city, string? search,
        string? period, DateTime? dateFrom, DateTime? dateTo, int page = 1)
    {
        try
        {
            var filter = BuildActivityFilter(campaignId, activityType, sentiment, city, search, period, dateFrom, dateTo);
            var result = await _activityLogService.GetAsync(filter, page, ActivityPageSize);

            return Json(new
            {
                success = true,
                data = result.Items,
                summary = result.Summary,
                pagination = new { result.CurrentPage, result.PageSize, result.TotalCount, result.TotalPages }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load prospect activity log");
            return Json(new { success = false, message = "Could not load prospect activity." });
        }
    }

    /// <summary>Distinct prospect cities for the activity-log City filter (optional campaign scope).</summary>
    [HttpGet]
    public async Task<IActionResult> AxGetProspectActivityCities(int? campaignId)
    {
        try
        {
            var cities = await _activityLogService.GetCitiesAsync(campaignId);
            return Json(new { success = true, data = cities });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load prospect activity cities");
            return Json(new { success = false, message = "Could not load cities." });
        }
    }

    /// <summary>
    /// CSV export of the CURRENT filtered activity set (all matching rows, not just a page).
    /// PRIVACY: the next-action contact name and phone are intentionally excluded — the CSV mirrors
    /// the on-screen rule and only indicates whether a contact/phone is on file.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ExportProspectActivityCsv(
        int? campaignId, byte? activityType, byte? sentiment, string? city, string? search,
        string? period, DateTime? dateFrom, DateTime? dateTo)
    {
        try
        {
            var filter = BuildActivityFilter(campaignId, activityType, sentiment, city, search, period, dateFrom, dateTo);
            var items = await _activityLogService.GetAllForExportAsync(filter);

            static string Esc(string? v) => "\"" + (v ?? string.Empty).Replace("\"", "\"\"") + "\"";

            var csv = new StringBuilder();
            csv.AppendLine("Occurred,Prospect,City,Segment,Campaign,Type,Outcome,Signal,Follow-up,Next action,Next action date,Next action time,Has contact,Has phone,By");
            foreach (var a in items)
            {
                var occurred = a.OccurredAtUtc.ToString("yyyy-MM-dd HH:mm");
                var naDate = a.NextActionDate?.ToString("yyyy-MM-dd") ?? string.Empty;
                var naTime = a.NextActionTime?.ToString("HH:mm") ?? string.Empty;
                csv.AppendLine(string.Join(",",
                    Esc(occurred),
                    Esc(a.ProspectName),
                    Esc(a.City),
                    Esc(a.Segment),
                    Esc(a.CampaignName),
                    Esc(a.ActivityTypeName),
                    Esc(a.Outcome),
                    Esc(a.SentimentName),
                    Esc(a.IsFollowUp ? "Yes" : "No"),
                    Esc(a.NextAction),
                    Esc(naDate),
                    Esc(naTime),
                    Esc(a.HasNextActionContact ? "Yes" : "No"),
                    Esc(a.HasNextActionPhone ? "Yes" : "No"),
                    Esc(a.PerformedByName)));
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            return File(bytes, "text/csv", $"prospect-activity-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export prospect activity CSV");
            return StatusCode(500, "Could not export prospect activity.");
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetCampaignFunnel(int campaignId)
    {
        try
        {
            var funnel = await _campaignService.GetFunnelAsync(campaignId);
            return Json(new { success = true, data = funnel });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load campaign funnel {CampaignId}", campaignId);
            return Json(new { success = false, message = "Could not load the funnel." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreateCampaign([FromBody] CreateProspectCampaignRequest request)
    {
        try
        {
            var result = await _campaignService.CreateCampaignAsync(request);
            return Json(new { success = result.Success, message = result.Message, id = result.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create prospect campaign");
            return Json(new { success = false, message = "Something went wrong creating the campaign." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateCampaign([FromBody] UpdateProspectCampaignRequest request)
    {
        try
        {
            var result = await _campaignService.UpdateCampaignAsync(request);
            return Json(new { success = result.Success, message = result.Message, id = result.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update prospect campaign {Id}", request.Id);
            return Json(new { success = false, message = "Something went wrong updating the campaign." });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // PROSPECT AJAX ENDPOINTS
    // ═══════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> AxGetProspects(
        int campaignId, string? priority, byte? status, string? segment, string? nextAction, int page = 1, int pageSize = 15)
    {
        try
        {
            var paged = await _prospectService.GetProspectsPagedAsync(
                campaignId, priority, status, segment, nextAction, page, pageSize);
            return Json(new
            {
                success = true,
                data = paged.Items,
                pagination = new { paged.CurrentPage, paged.PageSize, paged.TotalCount, paged.TotalPages }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load prospects for campaign {CampaignId}", campaignId);
            return Json(new { success = false, message = "Could not load prospects." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetProspectDetail(int id)
    {
        try
        {
            var detail = await _prospectService.GetProspectDetailAsync(id);
            if (detail == null)
                return Json(new { success = false, message = "Prospect not found." });

            return Json(new { success = true, data = detail });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load prospect {Id}", id);
            return Json(new { success = false, message = "Could not load the prospect." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreateProspect([FromBody] CreateProspectRequest request)
    {
        try
        {
            var result = await _prospectService.CreateProspectAsync(request);
            return Json(new { success = result.Success, message = result.Message, id = result.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create prospect");
            return Json(new { success = false, message = "Something went wrong creating the prospect." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateProspect([FromBody] UpdateProspectRequest request)
    {
        try
        {
            var result = await _prospectService.UpdateProspectAsync(request);
            return Json(new { success = result.Success, message = result.Message, id = result.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update prospect {Id}", request.Id);
            return Json(new { success = false, message = "Something went wrong updating the prospect." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateProspectStatus(int prospectId, byte status)
    {
        try
        {
            var result = await _prospectService.UpdateStatusAsync(prospectId, status);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update status for prospect {Id}", prospectId);
            return Json(new { success = false, message = "Something went wrong updating the status." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostAddActivity([FromBody] AddProspectActivityRequest request)
    {
        try
        {
            var result = await _prospectService.AddActivityAsync(request);
            return Json(new { success = result.Success, message = result.Message, id = result.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add activity to prospect {Id}", request.ProspectId);
            return Json(new { success = false, message = "Something went wrong recording the activity." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateActivity([FromBody] UpdateProspectActivityRequest request)
    {
        try
        {
            var result = await _prospectService.UpdateActivityAsync(request);
            return Json(new { success = result.Success, message = result.Message, id = result.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update activity {Id}", request.Id);
            return Json(new { success = false, message = "Something went wrong updating the activity." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostConvertToLead([FromBody] ConvertProspectRequest request)
    {
        try
        {
            var result = await _prospectService.ConvertToLeadAsync(request);
            return Json(new { success = result.Success, message = result.Message, id = result.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert prospect {Id} to lead", request.ProspectId);
            return Json(new { success = false, message = "Conversion failed. No contact or lead was created." });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // IMPORT AJAX ENDPOINTS
    // Stateless preview/confirm: the file is re-parsed on confirm so the client
    // never dictates the imported rows.
    // ═══════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostImportPreview(IFormFile file, int campaignId)
    {
        try
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "Please select a file to upload." });

            using var stream = file.OpenReadStream();
            var result = await _importService.ParseAndPreviewAsync(stream, file.FileName, campaignId);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            return Json(new { success = true, data = result.Data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preview prospect import for campaign {CampaignId}", campaignId);
            return Json(new { success = false, message = "Could not read the file. Please try again." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostImportConfirm(IFormFile file, int campaignId)
    {
        try
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "Please select a file to upload." });

            // Re-parse the file server-side to build an authoritative preview, then import.
            using var stream = file.OpenReadStream();
            var previewResult = await _importService.ParseAndPreviewAsync(stream, file.FileName, campaignId);
            if (!previewResult.Success || previewResult.Data == null)
                return Json(new { success = false, message = previewResult.Message ?? "Could not read the file." });

            var result = await _importService.ConfirmImportAsync(previewResult.Data);
            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            return Json(new { success = true, message = "Import complete.", data = result.Data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to confirm prospect import for campaign {CampaignId}", campaignId);
            return Json(new { success = false, message = "Import failed. No prospects were changed. Please try again." });
        }
    }

    /// <summary>Downloads a blank .xlsx template (correct sheet, headers, and example rows).</summary>
    [HttpGet]
    public IActionResult AxGetTemplate()
    {
        var bytes = _importService.GenerateTemplate();
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Prospects-Import-Template.xlsx");
    }
}
