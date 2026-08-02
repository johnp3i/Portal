using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Models.Payroll;
using Portal.Infrastructure.Services;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize(Roles = "SuperAdmin")]
public class AdminPayrollController : Controller
{
    private readonly IPayrollService _payrollService;

    public AdminPayrollController(IPayrollService payrollService)
    {
        _payrollService = payrollService;
    }

    // === Page Actions ===

    [HttpGet]
    public async Task<IActionResult> EarningTypes()
    {
        try
        {
            var earningTypes = await _payrollService.GetEarningTypesAsync();
            return View(earningTypes);
        }
        catch (Exception ex)
        {
            return View("Error");
        }
    }

    [HttpGet]
    public async Task<IActionResult> DeductionTypes()
    {
        try
        {
            var deductionTypes = await _payrollService.GetDeductionTypesForBusinessAsync(0);
            return View(deductionTypes);
        }
        catch (Exception ex)
        {
            return View("Error");
        }
    }

    [HttpGet]
    public async Task<IActionResult> DeductionRateHistory(int id)
    {
        try
        {
            var history = await _payrollService.GetRateHistoryAsync(id);
            ViewBag.DeductionTypeId = id;
            return View(history);
        }
        catch (Exception ex)
        {
            return View("Error");
        }
    }

    // === AJAX Endpoints ===

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreateEarningType([FromBody] CreateEarningTypeRequest request)
    {
        try
        {
            var result = await _payrollService.CreateEarningTypeAsync(request);

            if (result.Success)
                return Json(new { success = true, message = "Earning type created successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostToggleEarningType(int id)
    {
        try
        {
            var result = await _payrollService.ToggleEarningTypeAsync(id);

            if (result.Success)
                return Json(new { success = true, message = "Earning type status updated." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreateDeductionType([FromBody] CreateDeductionTypeRequest request)
    {
        try
        {
            var result = await _payrollService.CreateDeductionTypeAsync(0, request);

            if (result.Success)
                return Json(new { success = true, message = "Deduction type template created successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostToggleDeductionType(int id)
    {
        try
        {
            var result = await _payrollService.ToggleDeductionTypeAsync(id);

            if (result.Success)
                return Json(new { success = true, message = "Deduction type status updated." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostAddRateHistory([FromBody] AddRateHistoryRequest request)
    {
        try
        {
            var result = await _payrollService.AddRateHistoryAsync(request);

            if (result.Success)
                return Json(new { success = true, message = "Rate history entry added successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }
}
