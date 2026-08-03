using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Services;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.Purchase)]
public class ExpenseCategoryController : Controller
{
    private readonly IExpenseCategoryService _expenseCategoryService;
    private readonly IExpenseCategoryTemplateService _templateService;
    private readonly ICurrentTenantService _tenantService;

    public ExpenseCategoryController(
        IExpenseCategoryService expenseCategoryService,
        IExpenseCategoryTemplateService templateService,
        ICurrentTenantService tenantService)
    {
        _expenseCategoryService = expenseCategoryService;
        _templateService = templateService;
        _tenantService = tenantService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var categories = await _expenseCategoryService.GetExpenseCategoriesAsync();
        return View(categories);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] string name, [FromForm] int? expenseTypeId)
    {
        var category = new ExpenseCategory { Name = name, ExpenseTypeId = expenseTypeId };
        var result = await _expenseCategoryService.CreateExpenseCategoryAsync(category);
        return Json(new { success = result.Success, message = result.Message, id = result.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromForm] int id, [FromForm] string name, [FromForm] int? expenseTypeId)
    {
        var category = new ExpenseCategory { Id = id, Name = name, ExpenseTypeId = expenseTypeId };
        var result = await _expenseCategoryService.UpdateExpenseCategoryAsync(category);
        return Json(new { success = result.Success, message = result.Message });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        var result = await _expenseCategoryService.DeactivateExpenseCategoryAsync(id);
        return Json(new { success = result.Success, message = result.Message });
    }

    [HttpGet]
    public async Task<IActionResult> AxGetTemplates()
    {
        try
        {
            var templates = await _templateService.GetActiveTemplatesAsync();
            var businessId = _tenantService.CurrentBusinessId;
            var existingNames = (await _expenseCategoryService.GetExpenseCategoriesAsync())
                .Select(c => c.Name.ToLower()).ToHashSet();

            var data = templates.Select(t => new
            {
                t.Id,
                t.Name,
                t.Description,
                alreadyImported = existingNames.Contains(t.Name.ToLower())
            });

            return Json(new { success = true, data });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load templates." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostImportTemplates([FromBody] int[] templateIds)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _templateService.ImportTemplatesAsync(businessId, templateIds);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            return Json(new { success = true, message = $"{result.Data} categories imported.", importedCount = result.Data });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to import categories." });
        }
    }
}
