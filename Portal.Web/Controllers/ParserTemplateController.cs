using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Import;
using Portal.Infrastructure.Services;
using Portal.Infrastructure.Services.Import;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.PurchaseImport)]
public class ParserTemplateController : Controller
{
    private readonly IParserTemplateService _templateService;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly PortalDbContext _dbContext;

    public ParserTemplateController(
        IParserTemplateService templateService,
        ICurrentTenantService currentTenantService,
        PortalDbContext dbContext)
    {
        _templateService = templateService;
        _currentTenantService = currentTenantService;
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        var templates = await _templateService.GetAllForBusinessAsync(businessId);

        var suppliers = await _dbContext.Suppliers
            .Where(s => s.BusinessId == businessId && s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new Portal.Infrastructure.Models.SelectListItem { Id = s.Id, Name = s.Name })
            .ToListAsync();

        var categories = await _dbContext.ExpenseCategories
            .Where(c => c.BusinessId == businessId && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new Portal.Infrastructure.Models.SelectListItem { Id = c.Id, Name = c.Name })
            .ToListAsync();

        var originTypes = await _dbContext.PurchaseOriginTypes.ToListAsync();

        ViewBag.Suppliers = suppliers;
        ViewBag.Categories = categories;
        ViewBag.OriginTypes = originTypes;

        return View(templates);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreateTemplate(
        int supplierId, string name, string fileFormatType,
        int headerRow, int dataStartRow, string? sheetName,
        string columnMappingsJson)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var isSuperAdmin = User.IsInRole("SuperAdmin");

            var template = new ParserTemplate
            {
                BusinessId = businessId,
                SupplierId = supplierId,
                Name = name,
                FileFormatType = fileFormatType,
                HeaderRow = headerRow > 0 ? headerRow : 1,
                DataStartRow = dataStartRow > 0 ? dataStartRow : 2,
                SheetName = sheetName,
                ColumnMappingsJson = columnMappingsJson,
                IsManaged = isSuperAdmin
            };

            var result = await _templateService.CreateTemplateAsync(template);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            return Json(new { success = true, message = "Template created.", id = result.Data });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to create template." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateTemplate(
        int id, int supplierId, string name, string fileFormatType,
        int headerRow, int dataStartRow, string? sheetName,
        string columnMappingsJson)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var isSuperAdmin = User.IsInRole("SuperAdmin");

            var template = new ParserTemplate
            {
                Id = id,
                BusinessId = businessId,
                SupplierId = supplierId,
                Name = name,
                FileFormatType = fileFormatType,
                HeaderRow = headerRow > 0 ? headerRow : 1,
                DataStartRow = dataStartRow > 0 ? dataStartRow : 2,
                SheetName = sheetName,
                ColumnMappingsJson = columnMappingsJson
            };

            var result = await _templateService.UpdateTemplateAsync(template, isSuperAdmin);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            return Json(new { success = true, message = "Template updated." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to update template." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostDeleteTemplate(int templateId)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var isSuperAdmin = User.IsInRole("SuperAdmin");

            var result = await _templateService.DeleteTemplateAsync(templateId, businessId, isSuperAdmin);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            return Json(new { success = true, message = "Template deleted." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to delete template." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetTemplate(int id)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var template = await _templateService.GetTemplateByIdAsync(id, businessId);

            if (template == null)
                return Json(new { success = false, message = "Template not found." });

            return Json(new { success = true, data = new
            {
                template.Id,
                template.SupplierId,
                template.Name,
                template.FileFormatType,
                template.HeaderRow,
                template.DataStartRow,
                template.SheetName,
                template.ColumnMappingsJson,
                template.IsManaged
            }});
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load template." });
        }
    }
}
