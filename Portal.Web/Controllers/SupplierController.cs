using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Services;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.Purchase)]
public class SupplierController : Controller
{
    private readonly ISupplierService _supplierService;

    public SupplierController(ISupplierService supplierService)
    {
        _supplierService = supplierService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var suppliers = await _supplierService.GetSuppliersAsync();
        return View(suppliers);
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
}
