using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Models.Compliance;
using Portal.Infrastructure.Services;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.Compliance)]
public class ComplianceController : Controller
{
    private readonly IComplianceService _complianceService;
    private readonly ICurrentTenantService _tenantService;

    public ComplianceController(
        IComplianceService complianceService,
        ICurrentTenantService tenantService)
    {
        _complianceService = complianceService;
        _tenantService = tenantService;
    }

    // === Page Actions ===

    [HttpGet]
    public async Task<IActionResult> Index(string? category, string? status, DateTime? dateFrom, DateTime? dateTo, int page = 1)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _complianceService.GetApplicationsAsync(
                businessId, category, status, dateFrom, dateTo, page, 15);

            ViewBag.Categories = await _complianceService.GetCategoriesAsync();
            ViewBag.CurrentCategory = category;
            ViewBag.CurrentStatus = status;
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;

            return View(result);
        }
        catch (Exception ex)
        {
            return View("Error");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Import()
    {
        try
        {
            var templates = await _complianceService.GetAvailableTemplatesAsync(null);
            var categories = await _complianceService.GetCategoriesAsync();
            ViewBag.Categories = categories;
            return View(templates);
        }
        catch (Exception ex)
        {
            return View("Error");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var detail = await _complianceService.GetApplicationDetailAsync(id, businessId);

            if (detail == null)
                return NotFound();

            return View(detail);
        }
        catch (Exception ex)
        {
            return View("Error");
        }
    }

    [HttpGet]
    public IActionResult Calendar(int? year)
    {
        ViewBag.Year = year ?? DateTime.UtcNow.Year;
        return View();
    }

    // === AJAX Endpoints ===

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostImportTemplates([FromBody] ImportTemplatesRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _complianceService.ImportTemplatesAsync(businessId, request);

            if (result.Success)
                return Json(new { success = true, message = $"{result.Data} filing(s) imported successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateStatus(int id, string newStatus)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _complianceService.UpdateStatusAsync(id, newStatus, businessId);

            if (result.Success)
                return Json(new { success = true, message = "Status updated successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateDetails(int id, string? referenceNumber, string? notes, decimal? estimatedAmount)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _complianceService.UpdateDetailsAsync(id, referenceNumber, notes, estimatedAmount, businessId);

            if (result.Success)
                return Json(new { success = true, message = "Details saved successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreateFiling([FromBody] CreateFilingRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _complianceService.CreateFilingAsync(businessId, request);

            if (result.Success)
                return Json(new { success = true, message = "Filing created successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUploadAttachment(int id, IFormFile file)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            var result = await _complianceService.UploadAttachmentAsync(id, businessId, userId, file);

            if (result.Success)
                return Json(new { success = true, message = "Attachment uploaded successfully.", data = result.Data });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostDeleteAttachment(int attachmentId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _complianceService.DeleteAttachmentAsync(attachmentId, businessId);

            if (result.Success)
                return Json(new { success = true, message = "Attachment deleted." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetAvailableTemplates(string? country)
    {
        try
        {
            var templates = await _complianceService.GetAvailableTemplatesAsync(country);
            return Json(new { success = true, data = templates });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetCalendarData(int year)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var data = await _complianceService.GetCalendarDataAsync(businessId, year);
            return Json(new { success = true, data = data });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetCheckDuplicates(int[] templateIds, int year)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var hasDuplicates = await _complianceService.HasDuplicatesAsync(businessId, templateIds, year);
            return Json(new { success = true, hasDuplicates = hasDuplicates });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> DownloadAttachment(int attachmentId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _complianceService.DownloadAttachmentAsync(attachmentId, businessId);

            if (result == null)
                return NotFound();

            return File(result.FileStream, result.ContentType, result.OriginalFileName);
        }
        catch (Exception ex)
        {
            return NotFound();
        }
    }
}
