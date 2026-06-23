using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.ExpenseInsights;
using Portal.Infrastructure.Services;
using Portal.Web.Models;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.ExpenseInsights)]
public class ExpenseInsightController : Controller
{
    private readonly IExpenseInsightsService _service;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly PortalDbContext _dbContext;

    public ExpenseInsightController(
        IExpenseInsightsService service,
        ICurrentTenantService currentTenantService,
        PortalDbContext dbContext)
    {
        _service = service;
        _currentTenantService = currentTenantService;
        _dbContext = dbContext;
    }

    /// <summary>
    /// GET /ExpenseInsight — Initial page load with server-rendered expense insights data.
    /// </summary>
    public async Task<IActionResult> Index()
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;

            var request = new ExpenseInsightsPeriodRequest
            {
                PeriodType = PnlPeriodType.CurrentMonth
            };

            var insightsData = await _service.GetInsightsDataAsync(request);
            var trendData = await _service.GetTrendDataAsync();
            var currencySymbol = await GetCurrencySymbolAsync(businessId);
            var budgetConfig = await GetBudgetConfigAsync(businessId, insightsData);

            var viewModel = new ExpenseInsightsViewModel
            {
                InsightsData = insightsData,
                TrendData = trendData,
                BudgetConfig = budgetConfig,
                CurrencySymbol = currencySymbol,
                SelectedPeriod = PnlPeriodType.CurrentMonth
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// GET /ExpenseInsight/AxGetInsightsData — AJAX endpoint for period switching.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> AxGetInsightsData(PnlPeriodType periodType, DateOnly? startDate = null, DateOnly? endDate = null)
    {
        try
        {
            if (periodType == PnlPeriodType.Custom)
            {
                if (startDate == null || endDate == null)
                    return Json(new { success = false, message = "Both start and end dates are required for custom range." });

                var validation = _service.ValidateCustomRange(startDate.Value, endDate.Value);
                if (!validation.IsValid)
                    return Json(new { success = false, message = validation.ErrorMessage });
            }

            var request = new ExpenseInsightsPeriodRequest
            {
                PeriodType = periodType,
                CustomStartDate = startDate,
                CustomEndDate = endDate
            };

            var data = await _service.GetInsightsDataAsync(request);
            return Json(new { success = true, data });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An error occurred loading expense insights." });
        }
    }

    /// <summary>
    /// GET /ExpenseInsight/AxGetTrendData — AJAX endpoint for trend chart data.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> AxGetTrendData()
    {
        try
        {
            var data = await _service.GetTrendDataAsync();
            return Json(new { success = true, data });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An error occurred loading trend data." });
        }
    }

    /// <summary>
    /// GET /ExpenseInsight/ExportCsv — Download CSV export of expense breakdown.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ExportCsv(PnlPeriodType periodType, DateOnly? startDate = null, DateOnly? endDate = null)
    {
        try
        {
            if (periodType == PnlPeriodType.Custom)
            {
                if (startDate == null || endDate == null)
                    return Json(new { success = false, message = "Both start and end dates are required for custom range." });

                var validation = _service.ValidateCustomRange(startDate.Value, endDate.Value);
                if (!validation.IsValid)
                    return Json(new { success = false, message = validation.ErrorMessage });
            }

            var request = new ExpenseInsightsPeriodRequest
            {
                PeriodType = periodType,
                CustomStartDate = startDate,
                CustomEndDate = endDate
            };

            var result = await _service.ExportCsvAsync(request);
            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "CSV export failed. Please try again." });
        }
    }

    /// <summary>
    /// POST /ExpenseInsight/AxPostUpdateBudget — AJAX endpoint for saving/clearing a budget limit.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateBudget(int expenseCategoryId, decimal? periodLimitEur)
    {
        try
        {
            // Validate budget value
            if (periodLimitEur.HasValue)
            {
                if (periodLimitEur.Value <= 0)
                    return Json(new { success = false, message = "Budget limit must be a positive value." });

                if (periodLimitEur.Value > 999_999_999.99m)
                    return Json(new { success = false, message = "Budget limit cannot exceed 999,999,999.99." });
            }

            var serviceResult = await _service.UpsertBudgetLimitAsync(expenseCategoryId, periodLimitEur);

            if (serviceResult.Success)
                return Json(new { success = true, message = "Budget limit updated successfully." });

            return Json(new { success = false, message = serviceResult.Message ?? "Failed to update budget limit." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An error occurred updating the budget limit." });
        }
    }

    #region Private Methods

    private async Task<string> GetCurrencySymbolAsync(int businessId)
    {
        var profile = await _dbContext.BusinessProfiles
            .FirstOrDefaultAsync(bp => bp.BusinessId == businessId);

        return profile?.CurrencySymbol ?? "€";
    }

    private async Task<List<Portal.Web.Models.ExpenseCategoryLimitViewModel>> GetBudgetConfigAsync(int businessId, ExpenseInsightsDto insightsData)
    {
        var activeCategories = await _dbContext.ExpenseCategories
            .Where(ec => ec.BusinessId == businessId && ec.IsActive)
            .ToListAsync();

        var limits = await _dbContext.ExpenseCategoryLimits
            .Where(l => l.BusinessId == businessId)
            .ToListAsync();

        var budgetConfig = activeCategories.Select(category =>
        {
            var limit = limits.FirstOrDefault(l => l.ExpenseCategoryId == category.Id);
            var currentSpend = insightsData.Categories
                .FirstOrDefault(c => c.ExpenseCategoryId == category.Id)?.TotalSpend ?? 0m;

            var effectiveLimit = limit?.PeriodLimitEur ?? limit?.AnnualLimitEur;
            var budgetStatus = ComputeBudgetStatusDisplay(currentSpend, effectiveLimit);

            return new Portal.Web.Models.ExpenseCategoryLimitViewModel
            {
                CategoryId = category.Id,
                CategoryName = category.Name,
                CurrentLimit = effectiveLimit,
                CurrentSpend = currentSpend,
                BudgetStatus = budgetStatus
            };
        })
        .OrderBy(b => b.CategoryName)
        .ToList();

        return budgetConfig;
    }

    private static string ComputeBudgetStatusDisplay(decimal spend, decimal? limit)
    {
        if (limit == null || limit <= 0) return "No Limit";
        var ratio = spend / limit.Value;
        if (ratio >= 1.0m) return "Exceeded";
        if (ratio >= 0.8m) return "Approaching";
        return "Within Limit";
    }

    #endregion
}
