using System.Globalization;
using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;

namespace Portal.Tests.Unit.Properties;

/// <summary>
/// Property-based tests for CSV import parsing logic.
/// Tests Properties 10 and 11 from the design document.
/// </summary>
public class CsvImportPropertyTests
{
    private static readonly string[] ValidOriginTypes = { "Domestic", "EuReverseCharge", "NonEu" };

    /// <summary>
    /// Generates a valid non-empty, non-whitespace string without commas, quotes, or newlines
    /// to avoid CSV escaping complexities in round-trip testing.
    /// </summary>
    private static Gen<string> SafeStringGen(int minLength = 3, int maxLength = 20)
    {
        return Gen.Choose(minLength, maxLength).SelectMany(len =>
            Gen.ArrayOf(len, Gen.Elements(
                'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
                'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
                'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
                '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', ' ', '-', '_'))
            .Select(chars => new string(chars).Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    /// <summary>
    /// Generates a valid positive decimal amount with 2 decimal places.
    /// </summary>
    private static Gen<decimal> PositiveAmountGen()
    {
        return Gen.Choose(1, 99999999).Select(i => Math.Round((decimal)i / 100m, 2));
    }

    /// <summary>
    /// Generates a valid non-negative decimal amount with 2 decimal places.
    /// </summary>
    private static Gen<decimal> NonNegativeAmountGen()
    {
        return Gen.Choose(0, 99999999).Select(i => Math.Round((decimal)i / 100m, 2));
    }

    /// <summary>
    /// Generates a valid DateOnly within a reasonable range.
    /// </summary>
    private static Gen<DateOnly> ValidDateGen()
    {
        return Gen.Choose(0, 3650)
            .Select(days => DateOnly.FromDateTime(new DateTime(2015, 1, 1).AddDays(days)));
    }

    /// <summary>
    /// A record holding all generated CSV row data for round-trip testing.
    /// </summary>
    private record CsvRowData(
        DateOnly InvoiceDate,
        string InvoiceNumber,
        string SupplierName,
        string CategoryName,
        string Description,
        decimal AmountExcludingVat,
        decimal VatAmount,
        string OriginType,
        string Country,
        string Notes);

    /// <summary>
    /// Generator for a complete valid CSV row data set (Domestic origin).
    /// </summary>
    private static Gen<CsvRowData> DomesticRowDataGen()
    {
        return from date in ValidDateGen()
               from invoiceNumber in SafeStringGen(3, 15)
               from supplierName in SafeStringGen(3, 20)
               from categoryName in SafeStringGen(3, 20)
               from description in SafeStringGen(5, 30)
               from amount in PositiveAmountGen()
               from vat in NonNegativeAmountGen()
               from country in SafeStringGen(3, 15)
               from notes in SafeStringGen(3, 30)
               select new CsvRowData(date, invoiceNumber, supplierName, categoryName, description, amount, vat, "Domestic", country, notes);
    }

    /// <summary>
    /// Generator for a complete valid CSV row data set (EU Reverse Charge origin).
    /// </summary>
    private static Gen<CsvRowData> EuRcRowDataGen()
    {
        return from date in ValidDateGen()
               from invoiceNumber in SafeStringGen(3, 15)
               from supplierName in SafeStringGen(3, 20)
               from categoryName in SafeStringGen(3, 20)
               from description in SafeStringGen(5, 30)
               from amount in PositiveAmountGen()
               from country in SafeStringGen(3, 15)
               from notes in SafeStringGen(3, 30)
               select new CsvRowData(date, invoiceNumber, supplierName, categoryName, description, amount, 0m, "EuReverseCharge", country, notes);
    }

    /// <summary>
    /// Generator for a complete valid CSV row data set (Non-EU origin).
    /// </summary>
    private static Gen<CsvRowData> NonEuRowDataGen()
    {
        return from date in ValidDateGen()
               from invoiceNumber in SafeStringGen(3, 15)
               from supplierName in SafeStringGen(3, 20)
               from categoryName in SafeStringGen(3, 20)
               from description in SafeStringGen(5, 30)
               from amount in PositiveAmountGen()
               from vat in NonNegativeAmountGen()
               from country in SafeStringGen(3, 15)
               from notes in SafeStringGen(3, 30)
               select new CsvRowData(date, invoiceNumber, supplierName, categoryName, description, amount, vat, "NonEu", country, notes);
    }

    /// <summary>
    /// Serializes purchase data into a CSV line matching the expected format:
    /// InvoiceDate, InvoiceNumber, SupplierName, ExpenseCategoryName, Description,
    /// AmountExcludingVat, VatAmount, PurchaseOriginType, Country, Notes
    /// </summary>
    private static string SerializeToCsvLine(CsvRowData data)
    {
        var fields = new[]
        {
            data.InvoiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            data.InvoiceNumber,
            data.SupplierName,
            data.CategoryName,
            data.Description,
            data.AmountExcludingVat.ToString(CultureInfo.InvariantCulture),
            data.VatAmount.ToString(CultureInfo.InvariantCulture),
            data.OriginType,
            data.Country,
            data.Notes
        };

        return string.Join(",", fields);
    }

    private static string BuildCsvContent(string dataLine)
    {
        const string header = "InvoiceDate,InvoiceNumber,SupplierName,ExpenseCategoryName,Description,AmountExcludingVat,VatAmount,PurchaseOriginType,Country,Notes";
        return header + "\n" + dataLine;
    }

    // Feature: purchase-expense-tracking, Property 10: CSV parse round-trip
    // **Validates: Requirements 18.2**
    [Property(MaxTest = 100)]
    public Property CsvParseRoundTrip_Domestic_PreservesFieldValues()
    {
        return Prop.ForAll(
            DomesticRowDataGen().ToArbitrary(),
            data =>
            {
                var suppliers = new List<Supplier>
                {
                    new Supplier { Id = 1, BusinessId = 1, Name = data.SupplierName, IsActive = true }
                };
                var categories = new List<ExpenseCategory>
                {
                    new ExpenseCategory { Id = 1, BusinessId = 1, Name = data.CategoryName, IsActive = true }
                };

                var csvLine = SerializeToCsvLine(data);
                var csvContent = BuildCsvContent(csvLine);

                var service = new CsvImportService();
                var result = service.ParseAndValidate(csvContent, suppliers, categories);

                if (!result.IsFileValid)
                    return false.Label($"File parse failed: {result.FileError}");

                if (result.Rows.Count != 1)
                    return false.Label($"Expected 1 row but got {result.Rows.Count}");

                var row = result.Rows[0];

                if (!row.IsValid)
                    return false.Label($"Row is invalid: {row.ErrorMessage}");

                return (row.InvoiceDate == data.InvoiceDate)
                    .Label($"InvoiceDate: expected {data.InvoiceDate}, got {row.InvoiceDate}")
                    .And((row.InvoiceNumber == data.InvoiceNumber)
                        .Label($"InvoiceNumber: expected '{data.InvoiceNumber}', got '{row.InvoiceNumber}'"))
                    .And((row.SupplierName == data.SupplierName)
                        .Label($"SupplierName: expected '{data.SupplierName}', got '{row.SupplierName}'"))
                    .And((row.ExpenseCategoryName == data.CategoryName)
                        .Label($"ExpenseCategoryName: expected '{data.CategoryName}', got '{row.ExpenseCategoryName}'"))
                    .And((row.Description == data.Description)
                        .Label($"Description: expected '{data.Description}', got '{row.Description}'"))
                    .And((row.AmountExcludingVat == data.AmountExcludingVat)
                        .Label($"AmountExcludingVat: expected {data.AmountExcludingVat}, got {row.AmountExcludingVat}"))
                    .And((row.VatAmount == data.VatAmount)
                        .Label($"VatAmount: expected {data.VatAmount}, got {row.VatAmount}"))
                    .And((row.PurchaseOriginType == data.OriginType)
                        .Label($"PurchaseOriginType: expected '{data.OriginType}', got '{row.PurchaseOriginType}'"))
                    .And((row.Country == data.Country)
                        .Label($"Country: expected '{data.Country}', got '{row.Country}'"))
                    .And((row.Notes == data.Notes)
                        .Label($"Notes: expected '{data.Notes}', got '{row.Notes}'"))
                    .And((row.ResolvedSupplierId == 1)
                        .Label($"ResolvedSupplierId: expected 1, got {row.ResolvedSupplierId}"))
                    .And((row.ResolvedExpenseCategoryId == 1)
                        .Label($"ResolvedExpenseCategoryId: expected 1, got {row.ResolvedExpenseCategoryId}"));
            });
    }

    // Feature: purchase-expense-tracking, Property 10: CSV parse round-trip (EU Reverse Charge variant)
    // **Validates: Requirements 18.2**
    [Property(MaxTest = 100)]
    public Property CsvParseRoundTrip_EuReverseCharge_PreservesFieldValues()
    {
        return Prop.ForAll(
            EuRcRowDataGen().ToArbitrary(),
            data =>
            {
                var suppliers = new List<Supplier>
                {
                    new Supplier { Id = 2, BusinessId = 1, Name = data.SupplierName, IsActive = true }
                };
                var categories = new List<ExpenseCategory>
                {
                    new ExpenseCategory { Id = 3, BusinessId = 1, Name = data.CategoryName, IsActive = true }
                };

                var csvLine = SerializeToCsvLine(data);
                var csvContent = BuildCsvContent(csvLine);

                var service = new CsvImportService();
                var result = service.ParseAndValidate(csvContent, suppliers, categories);

                if (!result.IsFileValid)
                    return false.Label($"File parse failed: {result.FileError}");

                if (result.Rows.Count != 1)
                    return false.Label($"Expected 1 row but got {result.Rows.Count}");

                var row = result.Rows[0];

                if (!row.IsValid)
                    return false.Label($"Row is invalid: {row.ErrorMessage}");

                // For EU RC, VatAmount is forced to 0 by the parser per requirement 18.7
                return (row.InvoiceDate == data.InvoiceDate)
                    .Label($"InvoiceDate: expected {data.InvoiceDate}, got {row.InvoiceDate}")
                    .And((row.InvoiceNumber == data.InvoiceNumber)
                        .Label($"InvoiceNumber: expected '{data.InvoiceNumber}', got '{row.InvoiceNumber}'"))
                    .And((row.SupplierName == data.SupplierName)
                        .Label($"SupplierName: expected '{data.SupplierName}', got '{row.SupplierName}'"))
                    .And((row.ExpenseCategoryName == data.CategoryName)
                        .Label($"ExpenseCategoryName: expected '{data.CategoryName}', got '{row.ExpenseCategoryName}'"))
                    .And((row.Description == data.Description)
                        .Label($"Description: expected '{data.Description}', got '{row.Description}'"))
                    .And((row.AmountExcludingVat == data.AmountExcludingVat)
                        .Label($"AmountExcludingVat: expected {data.AmountExcludingVat}, got {row.AmountExcludingVat}"))
                    .And((row.VatAmount == 0m)
                        .Label($"VatAmount: expected 0 (EU RC forces to zero), got {row.VatAmount}"))
                    .And((row.PurchaseOriginType == "EuReverseCharge")
                        .Label($"PurchaseOriginType: expected 'EuReverseCharge', got '{row.PurchaseOriginType}'"))
                    .And((row.Country == data.Country)
                        .Label($"Country: expected '{data.Country}', got '{row.Country}'"))
                    .And((row.Notes == data.Notes)
                        .Label($"Notes: expected '{data.Notes}', got '{row.Notes}'"))
                    .And((row.ResolvedSupplierId == 2)
                        .Label($"ResolvedSupplierId: expected 2, got {row.ResolvedSupplierId}"))
                    .And((row.ResolvedExpenseCategoryId == 3)
                        .Label($"ResolvedExpenseCategoryId: expected 3, got {row.ResolvedExpenseCategoryId}"));
            });
    }

    // Feature: purchase-expense-tracking, Property 10: CSV parse round-trip (Non-EU variant)
    // **Validates: Requirements 18.2**
    [Property(MaxTest = 100)]
    public Property CsvParseRoundTrip_NonEu_PreservesFieldValues()
    {
        return Prop.ForAll(
            NonEuRowDataGen().ToArbitrary(),
            data =>
            {
                var suppliers = new List<Supplier>
                {
                    new Supplier { Id = 5, BusinessId = 1, Name = data.SupplierName, IsActive = true }
                };
                var categories = new List<ExpenseCategory>
                {
                    new ExpenseCategory { Id = 7, BusinessId = 1, Name = data.CategoryName, IsActive = true }
                };

                var csvLine = SerializeToCsvLine(data);
                var csvContent = BuildCsvContent(csvLine);

                var service = new CsvImportService();
                var result = service.ParseAndValidate(csvContent, suppliers, categories);

                if (!result.IsFileValid)
                    return false.Label($"File parse failed: {result.FileError}");

                if (result.Rows.Count != 1)
                    return false.Label($"Expected 1 row but got {result.Rows.Count}");

                var row = result.Rows[0];

                if (!row.IsValid)
                    return false.Label($"Row is invalid: {row.ErrorMessage}");

                // Non-EU preserves VatAmount and requires Country
                return (row.InvoiceDate == data.InvoiceDate)
                    .Label($"InvoiceDate: expected {data.InvoiceDate}, got {row.InvoiceDate}")
                    .And((row.InvoiceNumber == data.InvoiceNumber)
                        .Label($"InvoiceNumber: expected '{data.InvoiceNumber}', got '{row.InvoiceNumber}'"))
                    .And((row.SupplierName == data.SupplierName)
                        .Label($"SupplierName: expected '{data.SupplierName}', got '{row.SupplierName}'"))
                    .And((row.ExpenseCategoryName == data.CategoryName)
                        .Label($"ExpenseCategoryName: expected '{data.CategoryName}', got '{row.ExpenseCategoryName}'"))
                    .And((row.Description == data.Description)
                        .Label($"Description: expected '{data.Description}', got '{row.Description}'"))
                    .And((row.AmountExcludingVat == data.AmountExcludingVat)
                        .Label($"AmountExcludingVat: expected {data.AmountExcludingVat}, got {row.AmountExcludingVat}"))
                    .And((row.VatAmount == data.VatAmount)
                        .Label($"VatAmount: expected {data.VatAmount}, got {row.VatAmount}"))
                    .And((row.PurchaseOriginType == "NonEu")
                        .Label($"PurchaseOriginType: expected 'NonEu', got '{row.PurchaseOriginType}'"))
                    .And((row.Country == data.Country)
                        .Label($"Country: expected '{data.Country}', got '{row.Country}'"))
                    .And((row.Notes == data.Notes)
                        .Label($"Notes: expected '{data.Notes}', got '{row.Notes}'"))
                    .And((row.ResolvedSupplierId == 5)
                        .Label($"ResolvedSupplierId: expected 5, got {row.ResolvedSupplierId}"))
                    .And((row.ResolvedExpenseCategoryId == 7)
                        .Label($"ResolvedExpenseCategoryId: expected 7, got {row.ResolvedExpenseCategoryId}"));
            });
    }

    // ===== Property 11: Case-insensitive name matching =====

    private static readonly List<Supplier> KnownSuppliers = new()
    {
        new() { Id = 101, BusinessId = 1, Name = "Acme Corp", IsActive = true },
        new() { Id = 102, BusinessId = 1, Name = "Office Supplies Ltd", IsActive = true },
        new() { Id = 103, BusinessId = 1, Name = "Tech Solutions", IsActive = true },
        new() { Id = 104, BusinessId = 1, Name = "Global Logistics", IsActive = true },
        new() { Id = 105, BusinessId = 1, Name = "Fresh Foods Market", IsActive = true }
    };

    private static readonly List<ExpenseCategory> KnownCategories = new()
    {
        new() { Id = 201, BusinessId = 1, Name = "Office Supplies", IsActive = true },
        new() { Id = 202, BusinessId = 1, Name = "Software", IsActive = true },
        new() { Id = 203, BusinessId = 1, Name = "Travel", IsActive = true },
        new() { Id = 204, BusinessId = 1, Name = "Marketing", IsActive = true },
        new() { Id = 205, BusinessId = 1, Name = "Utilities", IsActive = true }
    };

    /// <summary>
    /// Applies a random case transformation to a string based on a mode:
    /// 0 = all uppercase, 1 = all lowercase, 2 = random per-character based on seed.
    /// </summary>
    private static string ApplyCaseTransformation(string original, int mode, int seed)
    {
        return mode switch
        {
            0 => original.ToUpperInvariant(),
            1 => original.ToLowerInvariant(),
            _ => ApplyRandomPerCharCase(original, seed)
        };
    }

    private static string ApplyRandomPerCharCase(string original, int seed)
    {
        var random = new System.Random(seed);
        var chars = original.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = random.Next(2) == 0
                ? char.ToUpperInvariant(chars[i])
                : char.ToLowerInvariant(chars[i]);
        }
        return new string(chars);
    }

    // Feature: purchase-expense-tracking, Property 11: Case-insensitive name matching
    // **Validates: Requirements 18.4**
    [Property(MaxTest = 100)]
    public Property CaseInsensitive_NameMatching_Resolves_To_Same_Record()
    {
        var combinedGen = from supplierIndex in Gen.Choose(0, KnownSuppliers.Count - 1)
                          from categoryIndex in Gen.Choose(0, KnownCategories.Count - 1)
                          from caseMode in Gen.Choose(0, 2) // 0=upper, 1=lower, 2=random per-char
                          from seed in Gen.Choose(0, 10000)
                          select new { SupplierIndex = supplierIndex, CategoryIndex = categoryIndex, CaseMode = caseMode, Seed = seed };

        return Prop.ForAll(
            combinedGen.ToArbitrary(),
            input =>
            {
                var service = new CsvImportService();

                var originalSupplier = KnownSuppliers[input.SupplierIndex];
                var originalCategory = KnownCategories[input.CategoryIndex];

                var transformedSupplierName = ApplyCaseTransformation(originalSupplier.Name, input.CaseMode, input.Seed);
                var transformedCategoryName = ApplyCaseTransformation(originalCategory.Name, input.CaseMode, input.Seed + 1);

                var dataRow = $"2024-03-15,INV-001,{transformedSupplierName},{transformedCategoryName},Test purchase,100.00,21.00,Domestic,,";
                var csv = BuildCsvContent(dataRow);

                var result = service.ParseAndValidate(csv, KnownSuppliers, KnownCategories);

                var fileValid = result.IsFileValid
                    .Label("File should be valid");

                var hasRows = (result.Rows.Count == 1)
                    .Label($"Expected 1 row but got {result.Rows.Count}");

                if (!result.IsFileValid || result.Rows.Count == 0)
                    return fileValid.And(hasRows);

                var row = result.Rows[0];

                var rowValid = row.IsValid
                    .Label($"Row should be valid but got error: {row.ErrorMessage}");

                var supplierResolved = (row.ResolvedSupplierId == originalSupplier.Id)
                    .Label($"Supplier '{transformedSupplierName}' (original: '{originalSupplier.Name}') should resolve to Id={originalSupplier.Id} but got {row.ResolvedSupplierId}");

                var categoryResolved = (row.ResolvedExpenseCategoryId == originalCategory.Id)
                    .Label($"Category '{transformedCategoryName}' (original: '{originalCategory.Name}') should resolve to Id={originalCategory.Id} but got {row.ResolvedExpenseCategoryId}");

                return fileValid
                    .And(hasRows)
                    .And(rowValid)
                    .And(supplierResolved)
                    .And(categoryResolved);
            });
    }
}
