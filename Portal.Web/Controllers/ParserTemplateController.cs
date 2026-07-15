using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Import;
using Portal.Infrastructure.Models.Import;
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostTestTemplate(
        IFormFile file, string fileFormatType, int headerRow, int dataStartRow,
        string? sheetName, string columnMappingsJson)
    {
        try
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "Please select a file." });

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrEmpty(extension) ||
                !new[] { ".csv", ".xlsx", ".xls" }.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "Only CSV, XLSX, and XLS files are accepted." });
            }

            // Build a temporary template from the form data (not persisted)
            var tempTemplate = new ParserTemplate
            {
                FileFormatType = fileFormatType ?? "CSV",
                HeaderRow = headerRow > 0 ? headerRow : 1,
                DataStartRow = dataStartRow > 0 ? dataStartRow : 2,
                SheetName = sheetName,
                ColumnMappingsJson = columnMappingsJson ?? "[]"
            };

            var fileParsingService = HttpContext.RequestServices.GetRequiredService<IFileParsingService>();

            using var stream = file.OpenReadStream();
            List<ParsedRow> parsedRows;

            if (extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
            {
                parsedRows = fileParsingService.ParseCsv(stream, tempTemplate);
            }
            else
            {
                parsedRows = fileParsingService.ParseExcel(stream, tempTemplate);
            }

            // Return only first 5 rows for preview
            var previewRows = parsedRows.Take(5).Select(r => new
            {
                rowNumber = r.RowNumber,
                invoiceDate = r.InvoiceDate?.ToString("dd/MM/yyyy"),
                invoiceNumber = r.InvoiceNumber,
                description = r.Description,
                amountExcludingVat = r.AmountExcludingVat?.ToString("N2"),
                vatAmount = r.VatAmount?.ToString("N2"),
                totalAmount = r.TotalAmount?.ToString("N2"),
                country = r.Country
            }).ToList();

            if (previewRows.Count == 0)
            {
                return Json(new { success = true, rows = previewRows, totalParsed = 0,
                    message = $"No rows parsed. Header row {tempTemplate.HeaderRow} may not contain the expected column names. The system searched ±3 rows from that position." });
            }

            return Json(new { success = true, rows = previewRows, totalParsed = parsedRows.Count,
                message = $"{parsedRows.Count} rows parsed successfully. Showing first {previewRows.Count}." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to test template: " + ex.Message });
        }
    }
}
