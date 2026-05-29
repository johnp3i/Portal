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

    public ExpenseCategoryController(IExpenseCategoryService expenseCategoryService)
    {
        _expenseCategoryService = expenseCategoryService;
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
}
