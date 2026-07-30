using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;

namespace Portal.Web.Controllers;

[Authorize(Roles = "SuperAdmin")]
public class AdminWhatsNewController : Controller
{
    private readonly IAnnouncementService _announcementService;
    private readonly ILogger<AdminWhatsNewController> _logger;

    public AdminWhatsNewController(IAnnouncementService announcementService, ILogger<AdminWhatsNewController> logger)
    {
        _announcementService = announcementService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var announcements = await _announcementService.GetAllForAdminAsync();
        return View(announcements);
    }

    [HttpGet]
    public IActionResult Create()
    {
        var model = new CreateAnnouncementRequest
        {
            IsActive = true,
            PublishedAtUtc = DateTime.UtcNow
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateAnnouncementRequest model)
    {
        var result = await _announcementService.CreateAsync(model);
        if (result.Success)
            return RedirectToAction(nameof(Index));

        ViewBag.Error = result.Message;
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var item = await _announcementService.GetByIdForAdminAsync(id);
        if (item == null) return NotFound();

        var model = new UpdateAnnouncementRequest
        {
            Id = item.Id,
            Title = item.Title,
            Summary = item.Summary,
            DetailHtml = item.DetailHtml,
            ModuleKey = item.ModuleKey,
            CtaLabel = item.CtaLabel,
            CtaUrl = item.CtaUrl,
            TargetPlanTier = item.TargetPlanTier,
            IsActive = item.IsActive,
            PublishedAtUtc = item.PublishedAtUtc,
            ExpiresAtUtc = item.ExpiresAtUtc
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UpdateAnnouncementRequest model)
    {
        var result = await _announcementService.UpdateAsync(model);
        if (result.Success)
            return RedirectToAction(nameof(Index));

        ViewBag.Error = result.Message;
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> AxPostToggleActive(int id, bool isActive)
    {
        try
        {
            var result = await _announcementService.ToggleActiveAsync(id, isActive);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling announcement active state");
            return Json(new { success = false, message = "An error occurred." });
        }
    }

    [HttpPost]
    public IActionResult AxPostPreview([FromBody] string html)
    {
        return Json(new { success = true, rendered = html ?? string.Empty });
    }
}
