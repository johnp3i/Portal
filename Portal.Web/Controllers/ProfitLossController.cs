using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;
using Portal.Web.Models;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.Pnl)]
public class ProfitLossController : Controller
{
    private readonly IPnlService _pnlService;
    private readonly IPnlPdfService _pnlPdfService;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly PortalDbContext _dbContext;

    public ProfitLossController(
        IPnlService pnlService,
        IPnlPdfService pnlPdfService,
        ICurrentTenantService currentTenantService,
        PortalDbContext dbContext)
    {
        _pnlService = pnlService;
        _pnlPdfService = pnlPdfService;
        _currentTenantService = currentTenantService;
        _dbContext = dbContext;
    }

    /// <summary>
    /// GET /ProfitLoss — Initial page load with server-rendered P&amp;L data.
    /// </summary>
    public async Task<IActionResult> Index(string? period, string? startDate, string? endDate)
    {
        try
        {
            var periodType = ParsePeriodType(period);
            var businessId = _currentTenantService.CurrentBusinessId;

            // Validate custom date range
            if (periodType == PnlPeriodType.Custom)
            {
                if (!TryParseDateRange(startDate, endDate, out var customStart, out var customEnd))
                {
                    TempData["Error"] = "Please provide valid start and end dates for a custom range.";
                    return View(BuildEmptyViewModel(periodType, startDate, endDate, await GetCurrencySymbolAsync(businessId)));
                }

                var validation = _pnlService.ValidateCustomRange(customStart, customEnd);
                if (!validation.IsValid)
                {
                    TempData["Error"] = validation.ErrorMessage;
                    return View(BuildEmptyViewModel(periodType, startDate, endDate, await GetCurrencySymbolAsync(businessId)));
                }
            }

            var request = BuildRequest(periodType, startDate, endDate);
            var summary = await _pnlService.GetSummaryAsync(request);
            var currencySymbol = await GetCurrencySymbolAsync(businessId);

            var viewModel = new PnlViewModel
            {
                Summary = summary,
                SelectedPeriod = periodType,
                CustomStartDate = startDate,
                CustomEndDate = endDate,
                CurrencySymbol = currencySymbol
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// GET /ProfitLoss/AxGetPnlData — AJAX endpoint for period switching.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> AxGetPnlData(string period, string? startDate, string? endDate)
    {
        try
        {
            var periodType = ParsePeriodType(period);
            var businessId = _currentTenantService.CurrentBusinessId;

            // Validate custom date range
            if (periodType == PnlPeriodType.Custom)
            {
                if (!TryParseDateRange(startDate, endDate, out var customStart, out var customEnd))
                {
                    return Json(new { success = false, message = "Please provide valid start and end dates for a custom range." });
                }

                var validation = _pnlService.ValidateCustomRange(customStart, customEnd);
                if (!validation.IsValid)
                {
                    return Json(new { success = false, message = validation.ErrorMessage });
                }
            }

            var request = BuildRequest(periodType, startDate, endDate);
            var summary = await _pnlService.GetSummaryAsync(request);
            var currencySymbol = await GetCurrencySymbolAsync(businessId);

            var data = new
            {
                summary.PeriodStart,
                summary.PeriodEnd,
                summary.Revenue,
                summary.Cogs,
                summary.GrossProfit,
                summary.OperatingExpenses,
                summary.NetProfit,
                summary.GrossMargin,
                summary.NetMargin,
                summary.HasData,
                summary.Trend,
                summary.CategoryBreakdown,
                CurrencySymbol = currencySymbol,
                SelectedPeriod = periodType.ToString()
            };

            return Json(new { success = true, data });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load P&L data. Please try again." });
        }
    }

    /// <summary>
    /// GET /ProfitLoss/ExportPdf — Generate and download PDF export.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ExportPdf(string period, string? startDate, string? endDate)
    {
        try
        {
            var periodType = ParsePeriodType(period);
            var businessId = _currentTenantService.CurrentBusinessId;

            // Validate custom date range
            if (periodType == PnlPeriodType.Custom)
            {
                if (!TryParseDateRange(startDate, endDate, out var customStart, out var customEnd))
                {
                    return Json(new { success = false, message = "Please provide valid start and end dates for a custom range." });
                }

                var validation = _pnlService.ValidateCustomRange(customStart, customEnd);
                if (!validation.IsValid)
                {
                    return Json(new { success = false, message = validation.ErrorMessage });
                }
            }

            var request = BuildRequest(periodType, startDate, endDate);
            var summary = await _pnlService.GetSummaryAsync(request);
            var currencySymbol = await GetCurrencySymbolAsync(businessId);
            var businessName = await GetBusinessNameAsync(businessId);

            var pdfModel = new PnlPdfModel
            {
                BusinessName = businessName,
                CurrencySymbol = currencySymbol,
                Summary = summary
            };

            var pdfBytes = await _pnlPdfService.GenerateAsync(pdfModel);

            var filename = $"PnL_{businessName.Replace(" ", "_")}_{summary.PeriodStart:yyyyMMdd}_{summary.PeriodEnd:yyyyMMdd}.pdf";

            return File(pdfBytes, "application/pdf", filename);
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "PDF generation failed. Please try again." });
        }
    }

    #region Private Methods

    private static PnlPeriodType ParsePeriodType(string? period)
    {
        if (string.IsNullOrWhiteSpace(period))
            return PnlPeriodType.CurrentMonth;

        if (Enum.TryParse<PnlPeriodType>(period, ignoreCase: true, out var parsed))
            return parsed;

        return PnlPeriodType.CurrentMonth;
    }

    private static bool TryParseDateRange(string? startDate, string? endDate, out DateOnly start, out DateOnly end)
    {
        start = default;
        end = default;

        if (string.IsNullOrWhiteSpace(startDate) || string.IsNullOrWhiteSpace(endDate))
            return false;

        if (!DateOnly.TryParse(startDate, out start))
            return false;

        if (!DateOnly.TryParse(endDate, out end))
            return false;

        return true;
    }

    private static PnlPeriodRequest BuildRequest(PnlPeriodType periodType, string? startDate, string? endDate)
    {
        var request = new PnlPeriodRequest { PeriodType = periodType };

        if (periodType == PnlPeriodType.Custom)
        {
            if (DateOnly.TryParse(startDate, out var start))
                request.CustomStartDate = start;

            if (DateOnly.TryParse(endDate, out var end))
                request.CustomEndDate = end;
        }

        return request;
    }

    private async Task<string> GetCurrencySymbolAsync(int businessId)
    {
        var profile = await _dbContext.BusinessProfiles
            .FirstOrDefaultAsync(bp => bp.BusinessId == businessId);

        return profile?.CurrencySymbol ?? "€";
    }

    private async Task<string> GetBusinessNameAsync(int businessId)
    {
        var business = await _dbContext.Businesses
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.Id == businessId);

        return business?.Name ?? "Business";
    }

    private static PnlViewModel BuildEmptyViewModel(PnlPeriodType periodType, string? startDate, string? endDate, string currencySymbol)
    {
        return new PnlViewModel
        {
            Summary = new PnlSummaryDto { HasData = false },
            SelectedPeriod = periodType,
            CustomStartDate = startDate,
            CustomEndDate = endDate,
            CurrencySymbol = currencySymbol
        };
    }

    #endregion
}
