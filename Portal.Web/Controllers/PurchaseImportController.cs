using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Import;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Import;
using Portal.Infrastructure.Repositories.Import;
using Portal.Infrastructure.Services;
using Portal.Infrastructure.Services.Import;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.PurchaseImport)]
public class PurchaseImportController : Controller
{
    private readonly IImportEngineService _importEngine;
    private readonly IParserTemplateService _templateService;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly PortalDbContext _dbContext;
    private readonly SupplierImportProfileRepository _profileRepository;

    public PurchaseImportController(
        IImportEngineService importEngine,
        IParserTemplateService templateService,
        ICurrentTenantService currentTenantService,
        PortalDbContext dbContext,
        SupplierImportProfileRepository profileRepository)
    {
        _importEngine = importEngine;
        _templateService = templateService;
        _currentTenantService = currentTenantService;
        _dbContext = dbContext;
        _profileRepository = profileRepository;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var businessId = _currentTenantService.CurrentBusinessId;
        var suppliers = await _dbContext.Suppliers
            .Where(s => s.BusinessId == businessId && s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new SelectListItem { Id = s.Id, Name = s.Name })
            .ToListAsync();

        ViewBag.Suppliers = suppliers;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Preview(int sessionId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;
        var session = await _dbContext.ImportSessions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.BusinessId == businessId && !s.IsConfirmed);

        if (session == null)
            return RedirectToAction("Index");

        var rows = System.Text.Json.JsonSerializer.Deserialize<List<ValidatedRow>>(session.RowDataJson) ?? new();

        ViewBag.SessionId = session.Id;
        ViewBag.FileName = session.FileName;
        ViewBag.SupplierId = session.SupplierId;
        ViewBag.TotalRows = session.TotalRows;
        ViewBag.ValidRows = session.ValidRows;
        ViewBag.InvalidRows = session.InvalidRows;
        ViewBag.WarningRows = rows.Count(r => r.Status == RowValidationStatus.Warning);
        ViewBag.BatchTotal = rows
            .Where(r => !r.IsRemoved && r.Status != RowValidationStatus.Invalid && r.Data.TotalAmount.HasValue)
            .Sum(r => r.Data.TotalAmount!.Value);

        var profile = await _dbContext.BusinessProfiles
            .FirstOrDefaultAsync(bp => bp.BusinessId == businessId);
        ViewBag.CurrencySymbol = profile?.CurrencySymbol ?? "€";

        var categories = await _dbContext.ExpenseCategories
            .Where(c => c.BusinessId == businessId && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem { Id = c.Id, Name = c.Name })
            .ToListAsync();
        ViewBag.Categories = categories;

        var vatPeriods = await _dbContext.VatSubmissionPeriods
            .Where(p => p.BusinessId == businessId)
            .OrderByDescending(p => p.PeriodStartDate)
            .Select(p => new SelectListItem { Id = p.Id, Name = p.PeriodLabel })
            .ToListAsync();
        ViewBag.VatPeriods = vatPeriods;

        return View(rows);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostParseFile(IFormFile file, int supplierId, int? templateId)
    {
        try
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "Please select a file to upload." });

            var businessId = _currentTenantService.CurrentBusinessId;

            using var stream = file.OpenReadStream();
            var result = await _importEngine.ParseFileAsync(stream, file.FileName, supplierId, templateId, businessId);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            return Json(new { success = true, sessionId = result.Data!.SessionId, data = result.Data });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to process file. Please try again." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostConfirmImport(int sessionId)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            var result = await _importEngine.ConfirmImportAsync(sessionId, businessId, userId);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            return Json(new { success = true, data = result.Data });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Import failed. No records were created. Please try again." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateRow(int sessionId, int rowIndex, string field, string value)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;

            // Get supplierId from the session
            var session = await _dbContext.ImportSessions
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.BusinessId == businessId);

            if (session == null)
                return Json(new { success = false, message = "Session not found." });

            var result = await _importEngine.RevalidateRowAsync(sessionId, rowIndex, field, value, businessId, session.SupplierId);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            return Json(new { success = true, data = result.Data });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to update row." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostRemoveRow(int sessionId, int rowIndex)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var result = await _importEngine.RemoveRowAsync(sessionId, rowIndex, businessId);

            if (!result.Success)
                return Json(new { success = false, message = result.Message });

            return Json(new { success = true, message = "Row removed." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to remove row." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostBulkApply(int sessionId, string? category, string? origin, string? country, int? vatPeriodId)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;

            var session = await _dbContext.ImportSessions
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.BusinessId == businessId && !s.IsConfirmed);

            if (session == null)
                return Json(new { success = false, message = "Session not found." });

            var rows = System.Text.Json.JsonSerializer.Deserialize<List<ValidatedRow>>(session.RowDataJson) ?? new();
            var changed = false;

            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].IsRemoved) continue;

                if (!string.IsNullOrEmpty(category))
                {
                    rows[i].Data.ExpenseCategoryName = category;
                    rows[i].Data.ExpenseCategoryId = null;
                    changed = true;
                }
                if (!string.IsNullOrEmpty(origin))
                {
                    rows[i].Data.PurchaseOriginTypeName = origin;
                    rows[i].Data.PurchaseOriginTypeId = null;
                    changed = true;
                }
                if (!string.IsNullOrEmpty(country))
                {
                    rows[i].Data.Country = country;
                    changed = true;
                }
                if (vatPeriodId.HasValue)
                {
                    rows[i].Data.VatSubmissionPeriodId = vatPeriodId.Value;
                    changed = true;
                }
            }

            if (!changed)
                return Json(new { success = false, message = "No changes specified." });

            // Re-validate all rows
            var validationService = HttpContext.RequestServices.GetRequiredService<IImportValidationService>();
            var validated = await validationService.ValidateRowsAsync(
                rows.Where(r => !r.IsRemoved).Select(r => r.Data).ToList(),
                session.SupplierId, businessId);

            // Rebuild the rows list preserving removed rows
            var newRows = new List<ValidatedRow>();
            var validIdx = 0;
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].IsRemoved)
                {
                    newRows.Add(rows[i]);
                }
                else
                {
                    var v = validated[validIdx++];
                    v.IsDuplicate = rows[i].IsDuplicate;
                    newRows.Add(v);
                }
            }

            var activeRows = newRows.Where(r => !r.IsRemoved).ToList();
            var validCount = activeRows.Count(r => r.Status != RowValidationStatus.Invalid);
            var invalidCount = activeRows.Count(r => r.Status == RowValidationStatus.Invalid);

            var importSessionRepo = HttpContext.RequestServices.GetRequiredService<ImportSessionRepository>();
            await importSessionRepo.UpdateRowDataAsync(sessionId, businessId,
                System.Text.Json.JsonSerializer.Serialize(newRows), validCount, invalidCount, activeRows.Count);

            return Json(new { success = true, message = "Applied to all rows." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to apply bulk changes." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetTemplatesForSupplier(int supplierId)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var templates = await _templateService.GetTemplatesForSupplierAsync(supplierId, businessId);

            var data = templates.Select(t => new { t.Id, t.Name, t.FileFormatType, t.IsManaged }).ToList();
            return Json(new { success = true, data });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load templates." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetSupplierProfile(int supplierId)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var profile = await _profileRepository.GetBySupplierAsync(supplierId, businessId);

            if (profile == null)
                return Json(new { success = true, data = (object?)null });

            return Json(new { success = true, data = new
            {
                profile.DefaultExpenseCategoryId,
                profile.DefaultPurchaseOriginTypeId,
                profile.DefaultCountry
            }});
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load supplier profile." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostSaveSupplierProfile(int supplierId, int? defaultExpenseCategoryId, int? defaultPurchaseOriginTypeId, string? defaultCountry)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;

            var profile = new SupplierImportProfile
            {
                BusinessId = businessId,
                SupplierId = supplierId,
                DefaultExpenseCategoryId = defaultExpenseCategoryId,
                DefaultPurchaseOriginTypeId = defaultPurchaseOriginTypeId,
                DefaultCountry = defaultCountry
            };

            await _profileRepository.UpsertAsync(profile);

            return Json(new { success = true, message = "Supplier profile saved." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to save supplier profile." });
        }
    }
}
