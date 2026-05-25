using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Services;
using Portal.Web.Models;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.Purchase)]
public class PurchaseController : Controller
{
    private readonly IPurchaseService _purchaseService;
    private readonly ISupplierService _supplierService;
    private readonly IExpenseCategoryService _expenseCategoryService;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly PortalDbContext _dbContext;

    public PurchaseController(
        IPurchaseService purchaseService,
        ISupplierService supplierService,
        IExpenseCategoryService expenseCategoryService,
        ICurrentTenantService currentTenantService,
        PortalDbContext dbContext)
    {
        _purchaseService = purchaseService;
        _supplierService = supplierService;
        _expenseCategoryService = expenseCategoryService;
        _currentTenantService = currentTenantService;
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? supplierId, int? expenseCategoryId, int? purchaseOriginTypeId, DateOnly? dateFrom, DateOnly? dateTo, string? searchTerm)
    {
        var purchases = await _purchaseService.GetFilteredPurchasesAsync(supplierId, expenseCategoryId, dateFrom, dateTo);

        // Apply origin type filter in-memory (not yet supported by service method)
        if (purchaseOriginTypeId.HasValue)
        {
            purchases = purchases.Where(p => p.PurchaseOriginTypeId == purchaseOriginTypeId.Value).ToList();
        }

        // Apply search term filter in-memory
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            purchases = purchases.Where(p =>
                (p.Description != null && p.Description.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (p.InvoiceNumber != null && p.InvoiceNumber.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (p.Supplier?.Name != null && p.Supplier.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        // Load VAT periods for discrepancy badge display
        var businessId = _currentTenantService.CurrentBusinessId;
        var vatPeriods = await _dbContext.VatSubmissionPeriods
            .Where(p => p.BusinessId == businessId)
            .ToListAsync();

        var vatPeriodLookup = vatPeriods.ToDictionary(p => p.Id);
        foreach (var purchase in purchases)
        {
            if (purchase.VatSubmissionPeriodId.HasValue && vatPeriodLookup.TryGetValue(purchase.VatSubmissionPeriodId.Value, out var period))
            {
                purchase.VatSubmissionPeriod = period;
            }
        }

        var suppliers = await _supplierService.GetActiveSuppliersAsync();
        var categories = await _expenseCategoryService.GetActiveExpenseCategoriesAsync();
        var originTypes = await _dbContext.PurchaseOriginTypes.ToListAsync();

        var profile = await _dbContext.BusinessProfiles
            .FirstOrDefaultAsync(bp => bp.BusinessId == businessId);
        var currencySymbol = profile?.CurrencySymbol ?? "€";

        var model = new PurchaseListViewModel
        {
            Purchases = purchases,
            Suppliers = suppliers,
            ExpenseCategories = categories,
            OriginTypes = originTypes,
            CurrencySymbol = currencySymbol,
            SupplierId = supplierId,
            ExpenseCategoryId = expenseCategoryId,
            PurchaseOriginTypeId = purchaseOriginTypeId,
            DateFrom = dateFrom,
            DateTo = dateTo,
            SearchTerm = searchTerm
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = await BuildFormViewModelAsync();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PurchaseFormViewModel model)
    {
        var purchase = MapFormToEntity(model);

        var result = await _purchaseService.CreatePurchaseAsync(purchase);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message!);
            await PopulateDropdownsAsync(model);
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var purchase = await _purchaseService.GetPurchaseByIdAsync(id);
        if (purchase == null)
        {
            return NotFound();
        }

        var model = await BuildFormViewModelAsync();
        model.Id = purchase.Id;
        model.SupplierId = purchase.SupplierId;
        model.ExpenseCategoryId = purchase.ExpenseCategoryId;
        model.PurchaseOriginTypeId = purchase.PurchaseOriginTypeId;
        model.InvoiceNumber = purchase.InvoiceNumber;
        model.InvoiceDate = purchase.InvoiceDate;
        model.Description = purchase.Description;
        model.AmountExcludingVat = purchase.AmountExcludingVat;
        model.VatAmount = purchase.VatAmount;
        model.Country = purchase.Country;
        model.Notes = purchase.Notes;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PurchaseFormViewModel model)
    {
        var purchase = MapFormToEntity(model);
        purchase.Id = id;

        var result = await _purchaseService.UpdatePurchaseAsync(purchase);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message!);
            model.Id = id;
            await PopulateDropdownsAsync(model);
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await _purchaseService.CancelPurchaseAsync(id);
        return Json(new { success = result.Success, message = result.Success ? "Purchase cancelled successfully." : result.Message });
    }

    [HttpGet]
    public async Task<IActionResult> BulkEntry()
    {
        var suppliers = await _supplierService.GetActiveSuppliersAsync();
        var categories = await _expenseCategoryService.GetActiveExpenseCategoriesAsync();
        var originTypes = await _dbContext.PurchaseOriginTypes.ToListAsync();

        ViewBag.Suppliers = suppliers;
        ViewBag.ExpenseCategories = categories;
        ViewBag.OriginTypes = originTypes;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkCreate([FromBody] List<BulkPurchaseRowDto> rows)
    {
        if (rows == null || rows.Count == 0)
        {
            return Json(new { success = false, message = "No rows provided." });
        }

        var errors = new List<object>();
        var purchases = new List<Purchase>();

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var rowNumber = i + 1;
            var rowErrors = ValidateBulkRow(row, rowNumber);

            if (rowErrors.Count > 0)
            {
                errors.AddRange(rowErrors);
            }
            else
            {
                purchases.Add(new Purchase
                {
                    SupplierId = row.SupplierId,
                    ExpenseCategoryId = row.ExpenseCategoryId,
                    PurchaseOriginTypeId = row.PurchaseOriginTypeId,
                    InvoiceNumber = row.InvoiceNumber,
                    InvoiceDate = row.InvoiceDate,
                    Description = row.Description,
                    AmountExcludingVat = row.AmountExcludingVat,
                    VatAmount = row.VatAmount,
                    Country = row.Country
                });
            }
        }

        if (errors.Count > 0)
        {
            return Json(new
            {
                success = false,
                message = $"{errors.Count} row(s) have validation errors.",
                errors
            });
        }

        var result = await _purchaseService.BulkCreatePurchasesAsync(purchases);
        if (!result.Success)
        {
            return Json(new { success = result.Success, message = result.Message });
        }

        return Json(new { success = true, message = $"{purchases.Count} purchase(s) saved successfully." });
    }

    [HttpGet]
    public async Task<IActionResult> CsvImport()
    {
        var suppliers = await _supplierService.GetActiveSuppliersAsync();
        var categories = await _expenseCategoryService.GetActiveExpenseCategoriesAsync();

        ViewBag.Suppliers = suppliers;
        ViewBag.ExpenseCategories = categories;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CsvImport(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return Json(new { success = false, message = "No file uploaded." });
        }

        var lines = new List<string>();
        using (var reader = new StreamReader(file.OpenReadStream()))
        {
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (line != null)
                {
                    lines.Add(line);
                }
            }
        }

        // Remove header row
        if (lines.Count > 0)
        {
            lines.RemoveAt(0);
        }

        // Enforce 500-row limit before parsing
        if (lines.Count > 500)
        {
            return Json(new { success = false, message = "CSV file exceeds the maximum of 500 rows." });
        }

        var suppliers = await _supplierService.GetActiveSuppliersAsync();
        var categories = await _expenseCategoryService.GetActiveExpenseCategoriesAsync();

        var parsedRows = new List<CsvPurchaseRowDto>();

        for (int i = 0; i < lines.Count; i++)
        {
            var rowNumber = i + 1;
            var csvRow = ParseCsvRow(lines[i], rowNumber, suppliers, categories);
            parsedRows.Add(csvRow);
        }

        return Json(new
        {
            success = true,
            message = $"{parsedRows.Count} row(s) parsed.",
            rows = parsedRows
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CsvConfirm([FromBody] List<CsvPurchaseRowDto> rows)
    {
        if (rows == null || rows.Count == 0)
        {
            return Json(new { success = false, message = "No rows provided." });
        }

        var validRows = rows.Where(r => r.IsValid &&
            r.ResolvedSupplierId.HasValue &&
            r.ResolvedExpenseCategoryId.HasValue &&
            r.ResolvedPurchaseOriginTypeId.HasValue).ToList();

        if (validRows.Count == 0)
        {
            return Json(new { success = false, message = "No valid rows to import." });
        }

        var purchases = validRows.Select(row => new Purchase
        {
            SupplierId = row.ResolvedSupplierId!.Value,
            ExpenseCategoryId = row.ResolvedExpenseCategoryId!.Value,
            PurchaseOriginTypeId = row.ResolvedPurchaseOriginTypeId!.Value,
            InvoiceNumber = row.InvoiceNumber,
            InvoiceDate = row.InvoiceDate,
            Description = row.Description,
            AmountExcludingVat = row.AmountExcludingVat,
            VatAmount = row.VatAmount,
            Country = row.Country,
            Notes = row.Notes
        }).ToList();

        var result = await _purchaseService.BulkCreatePurchasesAsync(purchases);
        if (!result.Success)
        {
            return Json(new { success = false, message = result.Message });
        }

        return Json(new { success = true, message = $"{purchases.Count} purchase(s) imported successfully.", count = purchases.Count });
    }

    #region Private Helpers

    private async Task<PurchaseFormViewModel> BuildFormViewModelAsync()
    {
        var suppliers = await _supplierService.GetActiveSuppliersAsync();
        var categories = await _expenseCategoryService.GetActiveExpenseCategoriesAsync();
        var originTypes = await _dbContext.PurchaseOriginTypes.ToListAsync();

        return new PurchaseFormViewModel
        {
            Suppliers = suppliers,
            ExpenseCategories = categories,
            OriginTypes = originTypes,
            InvoiceDate = DateOnly.FromDateTime(DateTime.Today)
        };
    }

    private async Task PopulateDropdownsAsync(PurchaseFormViewModel model)
    {
        model.Suppliers = await _supplierService.GetActiveSuppliersAsync();
        model.ExpenseCategories = await _expenseCategoryService.GetActiveExpenseCategoriesAsync();
        model.OriginTypes = await _dbContext.PurchaseOriginTypes.ToListAsync();
    }

    private static Purchase MapFormToEntity(PurchaseFormViewModel model)
    {
        return new Purchase
        {
            SupplierId = model.SupplierId,
            ExpenseCategoryId = model.ExpenseCategoryId,
            PurchaseOriginTypeId = model.PurchaseOriginTypeId,
            InvoiceNumber = model.InvoiceNumber,
            InvoiceDate = model.InvoiceDate,
            Description = model.Description,
            AmountExcludingVat = model.AmountExcludingVat,
            VatAmount = model.VatAmount,
            Country = model.Country,
            Notes = model.Notes
        };
    }

    private static List<object> ValidateBulkRow(BulkPurchaseRowDto row, int rowNumber)
    {
        var errors = new List<object>();

        if (row.AmountExcludingVat <= 0)
        {
            errors.Add(new { row = rowNumber, field = "AmountExcludingVat", message = "Amount must be greater than zero." });
        }

        if (row.VatAmount < 0)
        {
            errors.Add(new { row = rowNumber, field = "VatAmount", message = "VAT amount cannot be negative." });
        }

        if (row.SupplierId <= 0)
        {
            errors.Add(new { row = rowNumber, field = "SupplierId", message = "Supplier is required." });
        }

        if (row.ExpenseCategoryId <= 0)
        {
            errors.Add(new { row = rowNumber, field = "ExpenseCategoryId", message = "Expense category is required." });
        }

        if (row.PurchaseOriginTypeId < 1 || row.PurchaseOriginTypeId > 3)
        {
            errors.Add(new { row = rowNumber, field = "PurchaseOriginTypeId", message = "Invalid purchase origin type." });
        }

        if ((row.PurchaseOriginTypeId == 2 || row.PurchaseOriginTypeId == 3) && string.IsNullOrWhiteSpace(row.Country))
        {
            errors.Add(new { row = rowNumber, field = "Country", message = "Country is required for EU RC and Non-EU purchases." });
        }

        return errors;
    }

    private static CsvPurchaseRowDto ParseCsvRow(string line, int rowNumber, List<Supplier> suppliers, List<ExpenseCategory> categories)
    {
        var dto = new CsvPurchaseRowDto
        {
            RowNumber = rowNumber,
            IsValid = true
        };

        var columns = ParseCsvLine(line);

        // Expected columns: InvoiceDate, InvoiceNumber, SupplierName, ExpenseCategoryName, Description, AmountExcludingVat, VatAmount, PurchaseOriginType, Country, Notes
        if (columns.Length < 7)
        {
            dto.IsValid = false;
            dto.ErrorMessage = "Row has insufficient columns. Expected at least 7 columns.";
            return dto;
        }

        // Parse InvoiceDate
        if (!DateOnly.TryParse(columns[0].Trim(), out var invoiceDate))
        {
            dto.IsValid = false;
            dto.ErrorMessage = $"Invalid date format: '{columns[0].Trim()}'.";
            return dto;
        }
        dto.InvoiceDate = invoiceDate;

        // InvoiceNumber (optional)
        dto.InvoiceNumber = string.IsNullOrWhiteSpace(columns[1]) ? null : columns[1].Trim();

        // SupplierName
        dto.SupplierName = columns[2].Trim();

        // ExpenseCategoryName
        dto.ExpenseCategoryName = columns[3].Trim();

        // Description
        dto.Description = columns[4].Trim();

        // AmountExcludingVat
        if (!decimal.TryParse(columns[5].Trim(), out var amountExclVat) || amountExclVat <= 0)
        {
            dto.IsValid = false;
            dto.ErrorMessage = $"Invalid or non-positive Amount Excluding VAT: '{columns[5].Trim()}'.";
            return dto;
        }
        dto.AmountExcludingVat = amountExclVat;

        // VatAmount
        if (!decimal.TryParse(columns[6].Trim(), out var vatAmount) || vatAmount < 0)
        {
            dto.IsValid = false;
            dto.ErrorMessage = $"Invalid or negative VAT Amount: '{columns[6].Trim()}'.";
            return dto;
        }
        dto.VatAmount = vatAmount;

        // PurchaseOriginType (optional, defaults to Domestic)
        if (columns.Length > 7 && !string.IsNullOrWhiteSpace(columns[7]))
        {
            dto.PurchaseOriginType = columns[7].Trim();
        }

        // Country (optional)
        if (columns.Length > 8 && !string.IsNullOrWhiteSpace(columns[8]))
        {
            dto.Country = columns[8].Trim();
        }

        // Notes (optional)
        if (columns.Length > 9 && !string.IsNullOrWhiteSpace(columns[9]))
        {
            dto.Notes = columns[9].Trim();
        }

        // Match SupplierName (case-insensitive)
        var matchedSupplier = suppliers.FirstOrDefault(s =>
            s.Name.Equals(dto.SupplierName, StringComparison.OrdinalIgnoreCase));
        if (matchedSupplier == null)
        {
            dto.IsValid = false;
            dto.ErrorMessage = $"Supplier '{dto.SupplierName}' not found.";
            return dto;
        }
        dto.ResolvedSupplierId = matchedSupplier.Id;

        // Match ExpenseCategoryName (case-insensitive)
        var matchedCategory = categories.FirstOrDefault(c =>
            c.Name.Equals(dto.ExpenseCategoryName, StringComparison.OrdinalIgnoreCase));
        if (matchedCategory == null)
        {
            dto.IsValid = false;
            dto.ErrorMessage = $"Expense category '{dto.ExpenseCategoryName}' not found.";
            return dto;
        }
        dto.ResolvedExpenseCategoryId = matchedCategory.Id;

        // Resolve PurchaseOriginType
        var originTypeId = ResolvePurchaseOriginTypeId(dto.PurchaseOriginType);
        if (originTypeId == null)
        {
            dto.IsValid = false;
            dto.ErrorMessage = $"Invalid purchase origin type: '{dto.PurchaseOriginType}'. Expected: Domestic, EuReverseCharge, or NonEu.";
            return dto;
        }
        dto.ResolvedPurchaseOriginTypeId = originTypeId;

        // Validate Country requirement for EU RC and Non-EU
        if ((originTypeId == 2 || originTypeId == 3) && string.IsNullOrWhiteSpace(dto.Country))
        {
            dto.IsValid = false;
            dto.ErrorMessage = "Country is required for EU Reverse Charge and Non-EU purchases.";
            return dto;
        }

        return dto;
    }

    private static int? ResolvePurchaseOriginTypeId(string originTypeName)
    {
        return originTypeName?.Trim().ToLowerInvariant() switch
        {
            "domestic" => 1,
            "eureversecharge" => 2,
            "eu reverse charge" => 2,
            "eurc" => 2,
            "eu rc" => 2,
            "noneu" => 3,
            "non-eu" => 3,
            "non eu" => 3,
            _ => null
        };
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = string.Empty;
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Escaped quote
                    current += '"';
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current);
                current = string.Empty;
            }
            else
            {
                current += c;
            }
        }

        fields.Add(current);
        return fields.ToArray();
    }

    #endregion
}
