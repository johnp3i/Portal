using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;
using Portal.Web.Models;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.Purchase)]
public class ExpenseCategoryLimitController : Controller
{
    private readonly IExpenseCategoryLimitService _expenseCategoryLimitService;

    public ExpenseCategoryLimitController(IExpenseCategoryLimitService expenseCategoryLimitService)
    {
        _expenseCategoryLimitService = expenseCategoryLimitService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var limits = await _expenseCategoryLimitService.GetLimitsForBusinessAsync();
        return View(limits);
    }

    [HttpPost]
    public async Task<IActionResult> CheckLimits([FromBody] CheckLimitsRequest request)
    {
        try
        {
            var result = await _expenseCategoryLimitService.EvaluateLimitsAsync(request);
            return Json(new { hasWarning = result.HasWarning, warnings = result.Warnings });
        }
        catch
        {
            return Json(new { hasWarning = false, warnings = new List<object>() });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] SaveLimitRequest request)
    {
        var result = await _expenseCategoryLimitService.SaveLimitAsync(
            request.ExpenseCategoryId,
            request.AnnualLimitEur,
            request.PeriodLimitEur);

        return Json(new { success = result.Success, message = result.Message });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear([FromBody] ClearLimitRequest request)
    {
        var result = await _expenseCategoryLimitService.ClearLimitAsync(
            request.ExpenseCategoryId,
            request.LimitType);

        return Json(new { success = result.Success, message = result.Message });
    }

    [HttpGet]
    public async Task<IActionResult> Progress()
    {
        try
        {
            var progress = await _expenseCategoryLimitService.GetSpendingProgressAsync();
            return Json(progress);
        }
        catch
        {
            return Json(new List<object>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> CategoryProgress(int expenseCategoryId)
    {
        try
        {
            var allProgress = await _expenseCategoryLimitService.GetSpendingProgressAsync();
            var categoryProgress = allProgress.FirstOrDefault(p => p.ExpenseCategoryId == expenseCategoryId);
            if (categoryProgress == null)
                return Json(new { found = false });
            return Json(new { found = true, data = categoryProgress });
        }
        catch
        {
            return Json(new { found = false });
        }
    }
}
