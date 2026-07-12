using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.Attachments)]
public class AttachmentController : Controller
{
    private readonly IDocumentAttachmentService _attachmentService;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IPlanCheckService _planCheckService;

    public AttachmentController(
        IDocumentAttachmentService attachmentService,
        ICurrentTenantService currentTenantService,
        IPlanCheckService planCheckService)
    {
        _attachmentService = attachmentService;
        _currentTenantService = currentTenantService;
        _planCheckService = planCheckService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? entityType, string? fileType, string? uploadedBy, DateTime? dateFrom, DateTime? dateTo, int page = 1)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Map file type filter to content type
            string? contentTypeFilter = fileType?.ToLowerInvariant() switch
            {
                "pdf" => "application/pdf",
                "png" => "image/png",
                "jpg" or "jpeg" => "image/jpeg",
                "webp" => "image/webp",
                _ => null
            };

            var pagedResult = await _attachmentService.GetAllPagedAsync(
                businessId, entityType, contentTypeFilter, uploadedBy,
                dateFrom, dateTo, page, 15, userId);

            var summary = await _attachmentService.GetSummaryAsync(businessId);

            ViewBag.Summary = summary;
            ViewBag.EntityType = entityType;
            ViewBag.FileType = fileType;
            ViewBag.UploadedBy = uploadedBy;
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;

            return View(pagedResult);
        }
        catch (Exception ex)
        {
            return View(new PagedResult<AttachmentIndexDto>
            {
                Items = new(),
                CurrentPage = 1,
                PageSize = 15,
                TotalCount = 0
            });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpload(IFormFile file, string entityType, int entityId)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, message = "No file provided." });
            }

            var businessId = _currentTenantService.CurrentBusinessId;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User not authenticated." });
            }

            var request = new UploadAttachmentRequest
            {
                BusinessId = businessId,
                UserId = userId,
                EntityType = entityType,
                EntityId = entityId,
                File = file
            };

            var result = await _attachmentService.UploadAsync(request);

            if (!result.Success)
            {
                return Json(new { success = false, message = result.Message });
            }

            return Json(new { success = true, data = result.Data });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to process file. Please try again." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetList(string entityType, int entityId)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var attachments = await _attachmentService.GetByEntityAsync(businessId, entityType, entityId, userId);

            return Json(new { success = true, data = attachments });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load attachments." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetDownload(int id)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;

            var result = await _attachmentService.DownloadAsync(id, businessId);

            if (!result.Success)
            {
                return NotFound(new { success = false, message = result.Message });
            }

            return File(result.Data!.FileStream, result.Data.ContentType, result.Data.OriginalFileName);
        }
        catch (Exception ex)
        {
            return NotFound(new { success = false, message = "The file is unavailable. Please contact support." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostDelete(int id)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User not authenticated." });
            }

            var isOwner = await _planCheckService.IsOwnerAsync(userId);

            var result = await _attachmentService.DeleteAsync(id, userId, businessId, isOwner);

            if (!result.Success)
            {
                return Json(new { success = false, message = result.Message });
            }

            return Json(new { success = true, message = "Attachment deleted." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to delete attachment. Please try again." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetCounts(string entityType, [FromQuery] int[] entityIds)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;

            var counts = await _attachmentService.GetCountsForEntitiesAsync(businessId, entityType, entityIds);

            return Json(new { success = true, data = counts });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load attachment counts." });
        }
    }
}
