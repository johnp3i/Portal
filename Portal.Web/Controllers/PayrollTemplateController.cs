using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Models.Payroll;
using Portal.Infrastructure.Services;

namespace Portal.Web.Controllers;

[Authorize(Roles = "SuperAdmin")]
public class PayrollTemplateController : Controller
{
    private readonly ICountryTemplateService _templateService;

    public PayrollTemplateController(ICountryTemplateService templateService)
    {
        _templateService = templateService;
    }

    // === Page Actions ===

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public IActionResult TaxBands(string countryCode = "CY")
    {
        ViewBag.CountryCode = countryCode;
        return View();
    }

    // === AJAX Endpoints ===

    [HttpGet]
    public async Task<IActionResult> AxGetTemplates(string countryCode = "CY")
    {
        try
        {
            var templates = await _templateService.GetTemplatesByCountryAsync(countryCode);
            return Json(new { success = true, data = templates });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load templates." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreateTemplate([FromBody] CreateCountryTemplateRequest request)
    {
        try
        {
            var result = await _templateService.CreateTemplateAsync(request);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            return Json(new { success = true, message = "Template created successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to create template." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateTemplate([FromBody] UpdateCountryTemplateRequest request)
    {
        try
        {
            var result = await _templateService.UpdateTemplateAsync(request);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            return Json(new { success = true, message = "Template updated successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to update template." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostDeactivateTemplate([FromBody] int id)
    {
        try
        {
            var result = await _templateService.DeactivateTemplateAsync(id);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            return Json(new { success = true, message = "Template deactivated." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to deactivate template." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetTaxBands(string countryCode = "CY", int? year = null)
    {
        try
        {
            var bands = await _templateService.GetTaxBandsAsync(countryCode, year);
            return Json(new { success = true, data = bands });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load tax bands." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreateTaxBand([FromBody] CreateTaxBandRequest request)
    {
        try
        {
            var result = await _templateService.CreateTaxBandAsync(request);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            return Json(new { success = true, message = "Tax band created successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to create tax band." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateTaxBand([FromBody] UpdateTaxBandRequest request)
    {
        try
        {
            var result = await _templateService.UpdateTaxBandAsync(request);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            return Json(new { success = true, message = "Tax band updated successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to update tax band." });
        }
    }
}
