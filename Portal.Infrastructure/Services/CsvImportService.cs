using System.Globalization;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Parses CSV content into validated CsvPurchaseRowDto rows,
/// performing case-insensitive name matching against active suppliers and expense categories.
/// </summary>
public class CsvImportService : ICsvImportService
{
    private const int MaxRows = 500;

    private static readonly string[] ValidOriginTypes = { "Domestic", "EuReverseCharge", "NonEu" };

    private static readonly Dictionary<string, int> OriginTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Domestic", 1 },
        { "EuReverseCharge", 2 },
        { "NonEu", 3 }
    };

    /// <inheritdoc />
    public CsvImportResult ParseAndValidate(
        string csvContent,
        List<Supplier> activeSuppliers,
        List<ExpenseCategory> activeExpenseCategories)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return CsvImportResult.Fail("CSV file is empty.");
        }

        var lines = SplitCsvLines(csvContent);

        // First line is the header
        if (lines.Count < 2)
        {
            return CsvImportResult.Fail("CSV file must contain a header row and at least one data row.");
        }

        var dataLineCount = lines.Count - 1;

        // Reject files exceeding 500 rows before parsing
        if (dataLineCount > MaxRows)
        {
            return CsvImportResult.Fail($"CSV file exceeds the maximum of {MaxRows} rows.");
        }

        // Build case-insensitive lookup dictionaries
        var supplierLookup = BuildSupplierLookup(activeSuppliers);
        var categoryLookup = BuildCategoryLookup(activeExpenseCategories);

        var rows = new List<CsvPurchaseRowDto>();

        for (int i = 1; i < lines.Count; i++)
        {
            var line = lines[i];

            // Skip completely empty lines
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var rowNumber = i; // 1-based (header is line 0 conceptually, data starts at line 1)
            var row = ParseRow(line, rowNumber, supplierLookup, categoryLookup);
            rows.Add(row);
        }

        return CsvImportResult.Success(rows);
    }

    private CsvPurchaseRowDto ParseRow(
        string line,
        int rowNumber,
        Dictionary<string, Supplier> supplierLookup,
        Dictionary<string, ExpenseCategory> categoryLookup)
    {
        var row = new CsvPurchaseRowDto { RowNumber = rowNumber };
        var errors = new List<string>();

        var fields = ParseCsvLine(line);

        // Expected columns: InvoiceDate, InvoiceNumber, SupplierName, ExpenseCategoryName,
        //                    Description, AmountExcludingVat, VatAmount, PurchaseOriginType, Country, Notes
        if (fields.Count < 10)
        {
            row.IsValid = false;
            row.ErrorMessage = $"Row {rowNumber}: Expected 10 columns but found {fields.Count}.";
            return row;
        }

        // Column 0: InvoiceDate (required)
        var invoiceDateStr = fields[0].Trim();
        if (string.IsNullOrWhiteSpace(invoiceDateStr))
        {
            errors.Add("InvoiceDate is required.");
        }
        else if (DateOnly.TryParse(invoiceDateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var invoiceDate))
        {
            row.InvoiceDate = invoiceDate;
        }
        else
        {
            errors.Add($"InvoiceDate '{invoiceDateStr}' is not a valid date.");
        }

        // Column 1: InvoiceNumber (optional)
        row.InvoiceNumber = string.IsNullOrWhiteSpace(fields[1]) ? null : fields[1].Trim();

        // Column 2: SupplierName (required, case-insensitive match)
        var supplierName = fields[2].Trim();
        if (string.IsNullOrWhiteSpace(supplierName))
        {
            errors.Add("SupplierName is required.");
        }
        else
        {
            row.SupplierName = supplierName;
            if (supplierLookup.TryGetValue(supplierName, out var matchedSupplier))
            {
                row.ResolvedSupplierId = matchedSupplier.Id;
            }
            else
            {
                errors.Add($"Supplier '{supplierName}' not found.");
            }
        }

        // Column 3: ExpenseCategoryName (required, case-insensitive match)
        var categoryName = fields[3].Trim();
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            errors.Add("ExpenseCategoryName is required.");
        }
        else
        {
            row.ExpenseCategoryName = categoryName;
            if (categoryLookup.TryGetValue(categoryName, out var matchedCategory))
            {
                row.ResolvedExpenseCategoryId = matchedCategory.Id;
            }
            else
            {
                errors.Add($"Expense category '{categoryName}' not found.");
            }
        }

        // Column 4: Description (required)
        var description = fields[4].Trim();
        if (string.IsNullOrWhiteSpace(description))
        {
            errors.Add("Description is required.");
        }
        else
        {
            row.Description = description;
        }

        // Column 5: AmountExcludingVat (required, must be > 0)
        var amountStr = fields[5].Trim();
        if (string.IsNullOrWhiteSpace(amountStr))
        {
            errors.Add("AmountExcludingVat is required.");
        }
        else if (decimal.TryParse(amountStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            if (amount <= 0)
            {
                errors.Add("AmountExcludingVat must be greater than zero.");
            }
            else
            {
                row.AmountExcludingVat = amount;
            }
        }
        else
        {
            errors.Add($"AmountExcludingVat '{amountStr}' is not a valid number.");
        }

        // Column 6: VatAmount (required, must be >= 0)
        var vatStr = fields[6].Trim();
        if (string.IsNullOrWhiteSpace(vatStr))
        {
            errors.Add("VatAmount is required.");
        }
        else if (decimal.TryParse(vatStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var vatAmount))
        {
            if (vatAmount < 0)
            {
                errors.Add("VatAmount cannot be negative.");
            }
            else
            {
                row.VatAmount = vatAmount;
            }
        }
        else
        {
            errors.Add($"VatAmount '{vatStr}' is not a valid number.");
        }

        // Column 7: PurchaseOriginType (required, must be Domestic/EuReverseCharge/NonEu)
        var originTypeStr = fields[7].Trim();
        if (string.IsNullOrWhiteSpace(originTypeStr))
        {
            errors.Add("PurchaseOriginType is required.");
        }
        else if (OriginTypeMap.TryGetValue(originTypeStr, out var originTypeId))
        {
            row.PurchaseOriginType = originTypeStr;
            row.ResolvedPurchaseOriginTypeId = originTypeId;
        }
        else
        {
            errors.Add($"PurchaseOriginType '{originTypeStr}' is invalid. Must be Domestic, EuReverseCharge, or NonEu.");
        }

        // Column 8: Country (required for EU RC and Non-EU)
        var country = fields[8].Trim();
        row.Country = string.IsNullOrWhiteSpace(country) ? null : country;

        // Column 9: Notes (optional)
        row.Notes = string.IsNullOrWhiteSpace(fields[9]) ? null : fields[9].Trim();

        // Apply origin type logic validation
        if (row.ResolvedPurchaseOriginTypeId.HasValue)
        {
            ApplyOriginTypeValidation(row, errors);
        }

        // Set final validation state
        if (errors.Count > 0)
        {
            row.IsValid = false;
            row.ErrorMessage = string.Join(" ", errors);
        }
        else
        {
            row.IsValid = true;
        }

        return row;
    }

    private static void ApplyOriginTypeValidation(CsvPurchaseRowDto row, List<string> errors)
    {
        switch (row.ResolvedPurchaseOriginTypeId)
        {
            case 2: // EU Reverse Charge — VatAmount must be 0, Country required
                if (row.VatAmount != 0)
                {
                    // Per requirement 18.7: EU RC sets VatAmount to zero
                    row.VatAmount = 0;
                }
                if (string.IsNullOrWhiteSpace(row.Country))
                {
                    errors.Add("Country is required for EU Reverse Charge transactions.");
                }
                break;

            case 3: // Non-EU — Country required, VatAmount preserved
                if (string.IsNullOrWhiteSpace(row.Country))
                {
                    errors.Add("Country is required for Non-EU purchases.");
                }
                break;

            case 1: // Domestic — no additional requirements
            default:
                break;
        }
    }

    /// <summary>
    /// Builds a case-insensitive lookup dictionary for suppliers by name.
    /// </summary>
    private static Dictionary<string, Supplier> BuildSupplierLookup(List<Supplier> activeSuppliers)
    {
        var lookup = new Dictionary<string, Supplier>(StringComparer.OrdinalIgnoreCase);
        foreach (var supplier in activeSuppliers)
        {
            // Use TryAdd to handle potential duplicate names (first match wins)
            lookup.TryAdd(supplier.Name, supplier);
        }
        return lookup;
    }

    /// <summary>
    /// Builds a case-insensitive lookup dictionary for expense categories by name.
    /// </summary>
    private static Dictionary<string, ExpenseCategory> BuildCategoryLookup(List<ExpenseCategory> activeExpenseCategories)
    {
        var lookup = new Dictionary<string, ExpenseCategory>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in activeExpenseCategories)
        {
            lookup.TryAdd(category.Name, category);
        }
        return lookup;
    }

    /// <summary>
    /// Splits CSV content into lines, handling both \r\n and \n line endings.
    /// </summary>
    private static List<string> SplitCsvLines(string csvContent)
    {
        var lines = new List<string>();
        using var reader = new StringReader(csvContent);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lines.Add(line);
        }
        return lines;
    }

    /// <summary>
    /// Parses a single CSV line into fields, handling quoted fields with commas and escaped quotes.
    /// </summary>
    public static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // Check for escaped quote (double quote)
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // Skip next quote
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        // Add the last field
        fields.Add(current.ToString());

        return fields;
    }
}
