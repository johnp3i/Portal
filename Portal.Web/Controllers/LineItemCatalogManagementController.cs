using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Services;
using Portal.Web.Models;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.Quotation)]
[Route("catalog")]
public class LineItemCatalogManagementController : Controller
{
    private readonly ILineItemCatalogService _catalogService;
    private readonly ICurrentTenantService _tenantService;

    public LineItemCatalogManagementController(
        ILineItemCatalogService catalogService,
        ICurrentTenantService tenantService)
    {
        _catalogService = catalogService;
        _tenantService = tenantService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var businessId = _tenantService.CurrentBusinessId;
        var entries = await _catalogService.GetAllAsync(businessId);
        return View(entries);
    }

    [HttpGet("edit/{id}")]
    public async Task<IActionResult> Edit(int id)
    {
        var businessId = _tenantService.CurrentBusinessId;
        var entry = await _catalogService.GetByIdAsync(id, businessId);

        if (entry == null)
        {
            return NotFound();
        }

        var viewModel = new LineItemCatalogEditViewModel
        {
            Id = entry.Id,
            Description = entry.Description,
            UnitPrice = entry.UnitPrice,
            VatRate = entry.VatRate,
            ReferenceUrl = entry.ReferenceUrl,
            Discount = entry.Discount,
            DiscountType = entry.DiscountType
        };

        return View(viewModel);
    }

    [HttpPost("edit/{id}")]
    [ValidateAntiForgeryToken]
    [ModuleAccess(PortalModules.Quotation, AccessLevels.Full)]
    public async Task<IActionResult> Edit(int id, LineItemCatalogEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var businessId = _tenantService.CurrentBusinessId;

        try
        {
            var entry = new LineItemCatalog
            {
                Id = id,
                BusinessId = businessId,
                Description = model.Description,
                UnitPrice = model.UnitPrice,
                VatRate = model.VatRate,
                ReferenceUrl = model.ReferenceUrl,
                Discount = model.Discount,
                DiscountType = model.DiscountType
            };

            await _catalogService.UpdateAsync(entry, businessId);
            TempData["SuccessMessage"] = "Catalog entry updated successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("delete/{id}")]
    [ValidateAntiForgeryToken]
    [ModuleAccess(PortalModules.Quotation, AccessLevels.Full)]
    public async Task<IActionResult> Delete(int id)
    {
        var businessId = _tenantService.CurrentBusinessId;

        try
        {
            await _catalogService.DeleteAsync(id, businessId);
            TempData["SuccessMessage"] = "Catalog entry deleted successfully.";
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index));
    }
}
