using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Services;

namespace Portal.Web.Controllers;

[Authorize(Roles = "SuperAdmin")]
[Route("Admin")]
public class AdminCategoryTemplateController : Controller
{
    private readonly IExpenseCategoryTemplateService _templateService;

    public AdminCategoryTemplateController(IExpenseCategoryTemplateService templateService)
    {
        _templateService = templateService;
    }

    [HttpGet("CategoryTemplates")]
    public async Task<IActionResult> CategoryTemplates()
    {
        var templates = await _templateService.GetAllTemplatesAsync();
        return View("~/Views/Admin/CategoryTemplates.cshtml", templates);
    }

    [HttpPost("AxPostCreateCategoryTemplate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreateCategoryTemplate(string name, string? description)
    {
        try
        {
            var result = await _templateService.CreateAsync(name, description);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to create template." });
        }
    }

    [HttpPost("AxPostUpdateCategoryTemplate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateCategoryTemplate(int id, string name, string? description)
    {
        try
        {
            var result = await _templateService.UpdateAsync(id, name, description);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to update template." });
        }
    }

    [HttpPost("AxPostDeactivateCategoryTemplate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostDeactivateCategoryTemplate(int id)
    {
        try
        {
            var result = await _templateService.DeactivateAsync(id);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to deactivate template." });
        }
    }

    [HttpPost("AxPostReactivateCategoryTemplate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostReactivateCategoryTemplate(int id)
    {
        try
        {
            var result = await _templateService.ReactivateAsync(id);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to reactivate template." });
        }
    }
}
