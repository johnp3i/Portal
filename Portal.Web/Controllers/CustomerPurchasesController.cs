using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;
using Portal.Web.Models;
using Portal.Web.Security;
using Portal.Web.Services;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.Customer)]
public class CustomerPurchasesController : Controller
{
    private const string ErrorMessage = "Failed to load purchases. Please try again.";
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly ICustomerPurchasesService _service;
    private readonly ICustomerPurchasesExcelService _excelService;
    private readonly ILogger<CustomerPurchasesController> _logger;

    public CustomerPurchasesController(
        ICustomerPurchasesService service,
        ICustomerPurchasesExcelService excelService,
        ILogger<CustomerPurchasesController> logger)
    {
        _service = service;
        _excelService = excelService;
        _logger = logger;
    }

    [HttpGet("Customer/Purchases/{id:int}")]
    public async Task<IActionResult> Index(int id, string? period, string? from, string? to, string? currency)
    {
        var pageWatch = System.Diagnostics.Stopwatch.StartNew();

        var parsed = PurchasesQuery.Parse(period, from, to, currency: null);
        var safeCurrency = PurchasesQuery.NormalizeCurrency(currency);
        var query = (parsed.Success ? parsed.Data! : PurchasesQuery.Default) with { Currency = safeCurrency };

        var dto = await _service.GetDashboardAsync(id, query);
        if (dto is null)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var viewModel = new CustomerPurchasesPageViewModel
        {
            Dashboard = dto,
            CollapsedKpiIds = KpiCollapseCookie.Parse(Request.Cookies[KpiCollapseCookie.Name]),
            RegisteredFeatureKeys = userId is null ? new List<string>() : await _service.GetRegisteredFeatureKeysAsync(userId),
            ShowPeriodFallbackWarning = !parsed.Success && (period != null || from != null || to != null)
        };

        _logger.LogInformation("Customer purchases page for customer {CustomerId} prepared in {ElapsedMs} ms", id, pageWatch.ElapsedMilliseconds);
        return View(viewModel);
    }

    [HttpGet("Customer/Purchases/{id:int}/AxGetPurchasesData")]
    public async Task<IActionResult> AxGetPurchasesData(int id, string? period, string? from, string? to, string? currency)
    {
        var parsed = PurchasesQuery.Parse(period, from, to, currency);
        if (!parsed.Success)
        {
            return ValidationFailure(parsed.Message);
        }

        try
        {
            var dto = await _service.GetDashboardAsync(id, parsed.Data!);
            if (dto is null)
            {
                return NotFound();
            }

            return Json(new { success = true, data = dto }, PurchasesJson.Options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load purchases dashboard for customer {CustomerId}", id);
            return ServerError();
        }
    }

    [HttpGet("Customer/Purchases/{id:int}/AxGetProductPurchaseLines")]
    public async Task<IActionResult> AxGetProductPurchaseLines(int id, string? period, string? from, string? to, string? currency, string? productKey)
    {
        var parsed = PurchasesQuery.Parse(period, from, to, currency);
        if (!parsed.Success)
        {
            return ValidationFailure(parsed.Message);
        }

        try
        {
            var dto = await _service.GetProductPurchaseLinesAsync(id, parsed.Data!, productKey ?? "");
            if (dto is null)
            {
                return NotFound();
            }

            return Json(new { success = true, data = dto }, PurchasesJson.Options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load product purchase lines for customer {CustomerId}", id);
            return ServerError();
        }
    }

    [HttpGet("Customer/Purchases/{id:int}/AxGetExportPurchases")]
    public async Task<IActionResult> AxGetExportPurchases(int id, string? period, string? from, string? to, string? currency, CancellationToken cancellationToken)
    {
        var parsed = PurchasesQuery.Parse(period, from, to, currency);
        if (!parsed.Success)
        {
            return ValidationFailure(parsed.Message);
        }

        try
        {
            var result = await _excelService.GenerateAsync(id, parsed.Data!, cancellationToken);
            if (result is null)
            {
                return NotFound();
            }

            return File(result.Value.Content, XlsxContentType, result.Value.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export purchases for customer {CustomerId}", id);
            return ServerError();
        }
    }

    [HttpPost("Customer/Purchases/AxPostRegisterFeatureInterest")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostRegisterFeatureInterest([FromBody] RegisterFeatureInterestRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return StatusCode(403);
        }

        try
        {
            var result = await _service.RegisterFeatureInterestAsync(userId, request?.FeatureKey);
            if (!result.Success)
            {
                return ValidationFailure(result.Message);
            }

            return Json(new { success = true, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register feature interest");
            return StatusCode(500, new { success = false, kind = "error", message = "Failed to register interest. Please try again." });
        }
    }

    private IActionResult ValidationFailure(string? message)
    {
        return StatusCode(400, new { success = false, kind = "validation", message });
    }

    private IActionResult ServerError()
    {
        return StatusCode(500, new { success = false, kind = "error", message = ErrorMessage });
    }
}
