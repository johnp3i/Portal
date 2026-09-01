using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Services;
using Portal.Web.Models;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

/// <summary>
/// CRUD for External Platforms — external systems (other billing platforms, online stores) that a
/// business imports sales from, identified by an invoice PlatformCode.
/// Gated to Professional+ via the 'external_platform_import' plan feature.
/// </summary>
[Authorize]
[ModuleAccess(PortalModules.ExternalPlatformImport)]
public class ExternalPlatformController : Controller
{
    private readonly IExternalPlatformService _platformService;

    public ExternalPlatformController(IExternalPlatformService platformService)
    {
        _platformService = platformService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var platforms = await _platformService.GetAllAsync(includeInactive: true);
        return View(platforms);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreatePlatform([FromForm] CreateExternalPlatformRequest request)
    {
        try
        {
            var result = await _platformService.CreateAsync(request.Name, request.PlatformCode, request.Description);
            return Json(new
            {
                success = result.Success,
                message = result.Success ? "Platform created successfully." : result.Message,
                id = result.Id
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to create platform." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdatePlatform([FromForm] UpdateExternalPlatformRequest request)
    {
        try
        {
            var result = await _platformService.UpdateAsync(request.Id, request.Name, request.PlatformCode, request.Description);
            return Json(new
            {
                success = result.Success,
                message = result.Success ? "Platform updated successfully." : result.Message
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to update platform." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostSetPlatformActive([FromForm] int id, [FromForm] bool isActive)
    {
        try
        {
            var result = await _platformService.SetActiveAsync(id, isActive);
            return Json(new
            {
                success = result.Success,
                message = result.Success
                    ? (isActive ? "Platform activated." : "Platform deactivated.")
                    : result.Message
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to update platform status." });
        }
    }
}
