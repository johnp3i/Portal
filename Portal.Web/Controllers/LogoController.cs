using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Services;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

/// <summary>
/// Authenticated controller for managing the business logo library.
/// </summary>
[Authorize]
[ModuleAccess(PortalModules.Quotation, AccessLevels.Full)]
public class LogoController : Controller
{
    private readonly ILogoService _logoService;
    private readonly ICurrentTenantService _tenantService;

    public LogoController(ILogoService logoService, ICurrentTenantService tenantService)
    {
        _logoService = logoService;
        _tenantService = tenantService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var logos = await _logoService.GetByBusinessIdAsync(_tenantService.CurrentBusinessId);
        return View(logos);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile file, string displayName)
    {
        if (file == null || string.IsNullOrWhiteSpace(displayName))
        {
            TempData["Error"] = "File and display name are required.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _logoService.UploadAsync(_tenantService.CurrentBusinessId, file, displayName.Trim());
            TempData["Success"] = "Logo uploaded successfully.";
        }
        catch (ArgumentException ex)
        {
            TempData["Error"] = ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _logoService.DeleteAsync(id, _tenantService.CurrentBusinessId);
            TempData["Success"] = "Logo deleted successfully.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
