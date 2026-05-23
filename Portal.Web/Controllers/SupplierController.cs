using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.Purchase)]
public class SupplierController : Controller
{
    private readonly ISupplierService _supplierService;
    private readonly ISupplierDashboardService _dashboardService;

    public SupplierController(ISupplierService supplierService, ISupplierDashboardService dashboardService)
    {
        _supplierService = supplierService;
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        // Trim and nullify empty/whitespace search
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        // Truncate search to 200 characters max
        if (search != null && search.Length > 200)
            search = search[..200];

        var pagedResult = await _supplierService.GetSuppliersPagedAsync(search, page);

        // Set ViewData for the shared paging control
        ViewData["CurrentPage"] = pagedResult.CurrentPage;
        ViewData["TotalPages"] = pagedResult.TotalPages;
        ViewData["TotalCount"] = pagedResult.TotalCount;
        ViewData["PageSize"] = pagedResult.PageSize;
        ViewData["HasPreviousPage"] = pagedResult.HasPreviousPage;
        ViewData["HasNextPage"] = pagedResult.HasNextPage;
        ViewData["SearchTerm"] = search;

        return View(pagedResult);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] string name)
    {
        var supplier = new Supplier { Name = name };
        var result = await _supplierService.CreateSupplierAsync(supplier);
        return Json(new { success = result.Success, message = result.Message, id = result.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromForm] int id, [FromForm] string name)
    {
        var supplier = new Supplier { Id = id, Name = name };
        var result = await _supplierService.UpdateSupplierAsync(supplier);
        return Json(new { success = result.Success, message = result.Message });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        var result = await _supplierService.DeactivateSupplierAsync(id);
        return Json(new { success = result.Success, message = result.Message });
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard(
        int id,
        int? periodId = null,
        int page = 1,
        string? description = null,
        int? categoryId = null,
        DateOnly? dateFrom = null,
        DateOnly? dateTo = null)
    {
        var supplier = await _supplierService.GetSupplierByIdAsync(id);
        if (supplier == null)
            return NotFound();

        // Trim and nullify empty/whitespace description
        description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        // Truncate description to 200 characters max
        if (description != null && description.Length > 200)
            description = description[..200];

        var dashboard = await _dashboardService.GetDashboardAsync(id, periodId, page, description, categoryId, dateFrom, dateTo);
        return View(dashboard);
    }
}
