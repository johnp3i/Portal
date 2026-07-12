using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Import;
using Portal.Infrastructure.Models.Import;

namespace Portal.Infrastructure.Services.Import;

/// <summary>
/// Validates parsed import rows against business rules: required fields, amounts,
/// origin type constraints, expense category resolution, and supplier profile defaults.
/// </summary>
public class ImportValidationService : IImportValidationService
{
    private readonly PortalDbContext _dbContext;

    public ImportValidationService(PortalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<ValidatedRow>> ValidateRowsAsync(List<ParsedRow> rows, int supplierId, int businessId)
    {
        try
        {
            // Pre-load reference data for efficiency
            var categories = await _dbContext.ExpenseCategories
                .Where(c => c.BusinessId == businessId && c.IsActive)
                .Select(c => new { c.Id, c.Name })
                .ToListAsync();

            var categoryLookup = categories
                .ToDictionary(c => c.Name.ToLowerInvariant(), c => c.Id);

            var supplierProfile = await _dbContext.SupplierImportProfiles
                .FirstOrDefaultAsync(p => p.SupplierId == supplierId && p.BusinessId == businessId);

            var result = new List<ValidatedRow>();

            foreach (var row in rows)
            {
                var validated = ValidateRow(row, supplierProfile, categoryLookup);
                result.Add(validated);
            }

            return result;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ValidatedRow> ValidateRowAsync(ParsedRow row, int supplierId, int businessId)
    {
        try
        {
            var categories = await _dbContext.ExpenseCategories
                .Where(c => c.BusinessId == businessId && c.IsActive)
                .Select(c => new { c.Id, c.Name })
                .ToListAsync();

            var categoryLookup = categories
                .ToDictionary(c => c.Name.ToLowerInvariant(), c => c.Id);

            var supplierProfile = await _dbContext.SupplierImportProfiles
                .FirstOrDefaultAsync(p => p.SupplierId == supplierId && p.BusinessId == businessId);

            return ValidateRow(row, supplierProfile, categoryLookup);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private static ValidatedRow ValidateRow(
        ParsedRow row,
        SupplierImportProfile? profile,
        Dictionary<string, int> categoryLookup)
    {
        var validated = new ValidatedRow
        {
            Data = row,
            Status = RowValidationStatus.Valid,
            Errors = new List<string>(),
            Warnings = new List<string>()
        };

        // Apply supplier profile defaults for missing fields
        if (profile != null)
        {
            if (!row.ExpenseCategoryId.HasValue && string.IsNullOrEmpty(row.ExpenseCategoryName) && profile.DefaultExpenseCategoryId.HasValue)
            {
                row.ExpenseCategoryId = profile.DefaultExpenseCategoryId;
            }

            if (!row.PurchaseOriginTypeId.HasValue && string.IsNullOrEmpty(row.PurchaseOriginTypeName) && profile.DefaultPurchaseOriginTypeId.HasValue)
            {
                row.PurchaseOriginTypeId = profile.DefaultPurchaseOriginTypeId;
            }

            if (string.IsNullOrEmpty(row.Country) && !string.IsNullOrEmpty(profile.DefaultCountry))
            {
                row.Country = profile.DefaultCountry;
            }
        }

        // Required field: InvoiceDate
        if (!row.InvoiceDate.HasValue)
        {
            validated.Errors.Add("Invalid invoice date");
        }

        // Required field: AmountExcludingVat or TotalAmount
        if (!row.AmountExcludingVat.HasValue && !row.TotalAmount.HasValue)
        {
            validated.Errors.Add("Amount (excl. VAT) or Total is required");
        }

        // AmountExcludingVat must be > 0
        if (row.AmountExcludingVat.HasValue && row.AmountExcludingVat.Value <= 0)
        {
            validated.Errors.Add("Amount must be greater than zero");
        }

        // VatAmount must be >= 0
        if (row.VatAmount.HasValue && row.VatAmount.Value < 0)
        {
            validated.Errors.Add("VAT amount cannot be negative");
        }

        // Compute TotalAmount if not provided
        if (!row.TotalAmount.HasValue && row.AmountExcludingVat.HasValue)
        {
            row.TotalAmount = row.AmountExcludingVat.Value + (row.VatAmount ?? 0);
        }

        // If TotalAmount provided but not AmountExcludingVat, derive it
        if (row.TotalAmount.HasValue && !row.AmountExcludingVat.HasValue)
        {
            row.AmountExcludingVat = row.TotalAmount.Value - (row.VatAmount ?? 0);
        }

        // Resolve expense category by name
        if (!row.ExpenseCategoryId.HasValue && !string.IsNullOrEmpty(row.ExpenseCategoryName))
        {
            var key = row.ExpenseCategoryName.ToLowerInvariant();
            if (categoryLookup.TryGetValue(key, out var catId))
            {
                row.ExpenseCategoryId = catId;
            }
            else
            {
                validated.Warnings.Add($"Category '{row.ExpenseCategoryName}' not found");
            }
        }

        // Resolve origin type by name
        if (!row.PurchaseOriginTypeId.HasValue && !string.IsNullOrEmpty(row.PurchaseOriginTypeName))
        {
            row.PurchaseOriginTypeId = row.PurchaseOriginTypeName.ToLowerInvariant() switch
            {
                "domestic" => 1,
                "eureversecharge" or "eu reverse charge" or "eu rc" => 2,
                "noneu" or "non-eu" or "non eu" => 3,
                "eupaid" or "eu paid" => 4,
                _ => null
            };

            if (!row.PurchaseOriginTypeId.HasValue)
            {
                validated.Warnings.Add($"Origin type '{row.PurchaseOriginTypeName}' not recognized");
            }
        }

        // Origin type constraints
        if (row.PurchaseOriginTypeId == 2) // EU Reverse Charge
        {
            if (row.VatAmount.HasValue && row.VatAmount.Value > 0)
            {
                validated.Errors.Add("EU Reverse Charge purchases must have zero VAT");
            }
            if (string.IsNullOrEmpty(row.Country))
            {
                validated.Errors.Add("Country is required for this origin type");
            }
        }
        else if (row.PurchaseOriginTypeId == 3) // Non-EU
        {
            if (string.IsNullOrEmpty(row.Country))
            {
                validated.Errors.Add("Country is required for this origin type");
            }
        }

        // ExpenseCategoryId is required by the Purchase table
        if (!row.ExpenseCategoryId.HasValue)
        {
            validated.Warnings.Add("Expense category is required — assign before import");
        }

        // Determine final status
        if (validated.Errors.Count > 0)
        {
            validated.Status = RowValidationStatus.Invalid;
        }
        else if (validated.Warnings.Count > 0)
        {
            validated.Status = RowValidationStatus.Warning;
        }

        return validated;
    }
}
