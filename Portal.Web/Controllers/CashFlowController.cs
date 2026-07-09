using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Services;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.Cashflow)]
public class CashFlowController : Controller
{
    private readonly ICashFlowService _cashFlowService;
    private readonly ICurrentTenantService _currentTenantService;

    public CashFlowController(ICashFlowService cashFlowService, ICurrentTenantService currentTenantService)
    {
        _cashFlowService = cashFlowService;
        _currentTenantService = currentTenantService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> AxGetProjection(int daysAhead = 30, string? excludedInvoiceIds = null)
    {
        try
        {
            int[]? excluded = null;
            if (!string.IsNullOrWhiteSpace(excludedInvoiceIds))
            {
                excluded = excludedInvoiceIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.Parse(s.Trim()))
                    .ToArray();
            }

            var businessId = _currentTenantService.CurrentBusinessId;
            var projection = await _cashFlowService.GetProjectionAsync(businessId, daysAhead, excluded);
            return Json(new { success = true, data = projection });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load projection." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetSettings()
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var settings = await _cashFlowService.GetSettingsAsync(businessId);
            return Json(new { success = true, data = settings });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load settings." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostSaveSettings(decimal startingBalance, decimal alertThreshold)
    {
        try
        {
            if (startingBalance < 0)
                return Json(new { success = false, message = "Starting balance cannot be negative." });

            if (alertThreshold < 0)
                return Json(new { success = false, message = "Alert threshold cannot be negative." });

            var businessId = _currentTenantService.CurrentBusinessId;
            await _cashFlowService.SaveSettingsAsync(businessId, startingBalance, alertThreshold);
            return Json(new { success = true, message = "Settings saved successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to save settings." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetWidget()
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var widget = await _cashFlowService.GetWidgetDataAsync(businessId);
            return Json(new { success = true, data = widget });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load widget data." });
        }
    }
}
