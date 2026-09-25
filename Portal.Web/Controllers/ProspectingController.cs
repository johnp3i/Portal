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
    private readonly IProspectImportService _importService;
    private readonly ISalesProductService _productService;
    private readonly ILogger<ProspectingController> _logger;

    public ProspectingController(
        IProspectCampaignService campaignService,
        IProspectService prospectService,
        IProspectImportService importService,
        ISalesProductService productService,
        ILogger<ProspectingController> logger)
    {
        _campaignService = campaignService;
        _prospectService = prospectService;
        _importService = importService;
        _productService = productService;
        _logger = logger;
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

    // ═══════════════════════════════════════════════════════════
    // CAMPAIGN AJAX ENDPOINTS
    // ═══════════════════════════════════════════════════════════

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
