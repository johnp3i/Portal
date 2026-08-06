using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Services;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.Payroll)]
public class PayrollComplianceController : Controller
{
    private readonly IPayrollService _payrollService;
    private readonly ICurrentTenantService _tenantService;

    public PayrollComplianceController(
        IPayrollService payrollService,
        ICurrentTenantService tenantService)
    {
        _payrollService = payrollService;
        _tenantService = tenantService;
    }

    // === Page Actions ===

    [HttpGet]
    public IActionResult ContributionReport()
    {
        return View();
    }

    // === AJAX Endpoints ===

    [HttpGet]
    public async Task<IActionResult> AxGetContributionReportData(int periodId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var report = await _payrollService.GetContributionReportAsync(periodId, businessId);

            if (report == null)
                return Json(new { success = false, message = "Period not found." });

            return Json(new { success = true, data = report });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load contribution report." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetComplianceFilingHistory(int periodId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var history = await _payrollService.GetComplianceFilingHistoryAsync(periodId, businessId);
            return Json(new { success = true, data = history });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load compliance history." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetAvailablePeriods()
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var periods = await _payrollService.GetPeriodsAsync(businessId);
            return Json(new { success = true, data = periods });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load periods." });
        }
    }
}
