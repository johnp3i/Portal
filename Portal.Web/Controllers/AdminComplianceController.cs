using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Models.Compliance;
using Portal.Infrastructure.Services;

namespace Portal.Web.Controllers;

[Authorize(Roles = "SuperAdmin")]
public class AdminComplianceController : Controller
{
    private readonly IComplianceService _complianceService;

    public AdminComplianceController(IComplianceService complianceService)
    {
        _complianceService = complianceService;
    }

    // === Page Actions ===

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try
        {
            var types = await _complianceService.GetAllTypesAsync();
            var categories = await _complianceService.GetCategoriesAsync();
            ViewBag.Categories = categories;
            return View(types);
        }
        catch (Exception ex)
        {
            return View("Error");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Categories()
    {
        try
        {
            var categories = await _complianceService.GetCategoriesAsync();
            return View(categories);
        }
        catch (Exception ex)
        {
            return View("Error");
        }
    }

    // === AJAX Endpoints ===

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreateType([FromBody] CreateApplicationTypeRequest request)
    {
        try
        {
            var result = await _complianceService.CreateTypeAsync(request);

            if (result.Success)
                return Json(new { success = true, message = "Application type created successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateType([FromBody] UpdateApplicationTypeRequest request)
    {
        try
        {
            var result = await _complianceService.UpdateTypeAsync(request);

            if (result.Success)
                return Json(new { success = true, message = "Application type updated successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostDeactivateType(int id)
    {
        try
        {
            var result = await _complianceService.DeactivateTypeAsync(id);

            if (result.Success)
                return Json(new { success = true, message = "Application type deactivated." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostActivateType(int id)
    {
        try
        {
            var result = await _complianceService.ActivateTypeAsync(id);

            if (result.Success)
                return Json(new { success = true, message = "Application type activated." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreateCategory([FromBody] CreateCategoryRequest request)
    {
        try
        {
            var result = await _complianceService.CreateCategoryAsync(request);

            if (result.Success)
                return Json(new { success = true, message = "Category created successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateCategory([FromBody] UpdateCategoryRequest request)
    {
        try
        {
            var result = await _complianceService.UpdateCategoryAsync(request);

            if (result.Success)
                return Json(new { success = true, message = "Category updated successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }
}
