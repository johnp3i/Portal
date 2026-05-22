using Xunit;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;

namespace Portal.Tests.Unit.Services;

/// <summary>
/// Unit tests for CsvImportService covering CSV parsing, name matching, and validation.
/// </summary>
public class CsvImportServiceTests
{
    private readonly CsvImportService _service;
    private readonly List<Supplier> _activeSuppliers;
    private readonly List<ExpenseCategory> _activeCategories;

    public CsvImportServiceTests()
    {
        _service = new CsvImportService();

        _activeSuppliers = new List<Supplier>
        {
            new() { Id = 1, BusinessId = 1, Name = "Acme Corp", IsActive = true },
            new() { Id = 2, BusinessId = 1, Name = "Office Supplies Ltd", IsActive = true },
            new() { Id = 3, BusinessId = 1, Name = "Tech Solutions", IsActive = true }
        };

        _activeCategories = new List<ExpenseCategory>
        {
            new() { Id = 10, BusinessId = 1, Name = "Office Supplies", IsActive = true },
            new() { Id = 11, BusinessId = 1, Name = "Software", IsActive = true },
            new() { Id = 12, BusinessId = 1, Name = "Travel", IsActive = true }
        };
    }

    private static string BuildCsv(params string[] dataRows)
    {
        var header = "InvoiceDate,InvoiceNumber,SupplierName,ExpenseCategoryName,Description,AmountExcludingVat,VatAmount,PurchaseOriginType,Country,Notes";
        var lines = new List<string> { header };
        lines.AddRange(dataRows);
        return string.Join(Environment.NewLine, lines);
    }

    [Fact]
    public void ParseAndValidate_ValidRow_ReturnsValidRow()
    {
        var csv = BuildCsv("2024-01-15,INV-001,Acme Corp,Office Supplies,Printer paper,100.00,21.00,Domestic,,Bulk order");

        var result = _service.ParseAndValidate(csv, _activeSuppliers, _activeCategories);

        Assert.True(result.IsFileValid);
        Assert.Single(result.Rows);
        var row = result.Rows[0];
        Assert.True(row.IsValid);
        Assert.Equal(new DateOnly(2024, 1, 15), row.InvoiceDate);
        Assert.Equal("INV-001", row.InvoiceNumber);
        Assert.Equal("Acme Corp", row.SupplierName);
        Assert.Equal("Office Supplies", row.ExpenseCategoryName);
        Assert.Equal("Printer paper", row.Description);
        Assert.Equal(100.00m, row.AmountExcludingVat);
        Assert.Equal(21.00m, row.VatAmount);
        Assert.Equal(1, row.ResolvedSupplierId);
        Assert.Equal(10, row.ResolvedExpenseCategoryId);
        Assert.Equal(1, row.ResolvedPurchaseOriginTypeId);
        Assert.Null(row.ErrorMessage);
    }

    [Fact]
    public void ParseAndValidate_CaseInsensitiveSupplierMatch_ResolvesCorrectly()
    {
        var csv = BuildCsv("2024-01-15,INV-001,ACME CORP,office supplies,Test,100.00,21.00,Domestic,,");

        var result = _service.ParseAndValidate(csv, _activeSuppliers, _activeCategories);

        Assert.True(result.IsFileValid);
        Assert.Single(result.Rows);
        var row = result.Rows[0];
        Assert.True(row.IsValid);
        Assert.Equal(1, row.ResolvedSupplierId);
        Assert.Equal(10, row.ResolvedExpenseCategoryId);
    }

    [Fact]
    public void ParseAndValidate_UnmatchedSupplier_FlagsRowAsInvalid()
    {
        var csv = BuildCsv("2024-01-15,INV-001,Unknown Vendor,Office Supplies,Test,100.00,21.00,Domestic,,");

        var result = _service.ParseAndValidate(csv, _activeSuppliers, _activeCategories);

        Assert.True(result.IsFileValid);
        Assert.Single(result.Rows);
        var row = result.Rows[0];
        Assert.False(row.IsValid);
        Assert.Contains("Supplier 'Unknown Vendor' not found", row.ErrorMessage);
    }

    [Fact]
    public void ParseAndValidate_UnmatchedCategory_FlagsRowAsInvalid()
    {
        var csv = BuildCsv("2024-01-15,INV-001,Acme Corp,Unknown Category,Test,100.00,21.00,Domestic,,");

        var result = _service.ParseAndValidate(csv, _activeSuppliers, _activeCategories);

        Assert.True(result.IsFileValid);
        Assert.Single(result.Rows);
        var row = result.Rows[0];
        Assert.False(row.IsValid);
        Assert.Contains("Expense category 'Unknown Category' not found", row.ErrorMessage);
    }

    [Fact]
    public void ParseAndValidate_ExceedsMaxRows_RejectsFile()
    {
        var header = "InvoiceDate,InvoiceNumber,SupplierName,ExpenseCategoryName,Description,AmountExcludingVat,VatAmount,PurchaseOriginType,Country,Notes";
        var lines = new List<string> { header };
        for (int i = 0; i < 501; i++)
        {
            lines.Add($"2024-01-15,INV-{i:D3},Acme Corp,Office Supplies,Item {i},100.00,21.00,Domestic,,");
        }
        var csv = string.Join(Environment.NewLine, lines);

        var result = _service.ParseAndValidate(csv, _activeSuppliers, _activeCategories);

        Assert.False(result.IsFileValid);
        Assert.Contains("500 rows", result.FileError);
    }

    [Fact]
    public void ParseAndValidate_Exactly500Rows_Succeeds()
    {
        var header = "InvoiceDate,InvoiceNumber,SupplierName,ExpenseCategoryName,Description,AmountExcludingVat,VatAmount,PurchaseOriginType,Country,Notes";
        var lines = new List<string> { header };
        for (int i = 0; i < 500; i++)
        {
            lines.Add($"2024-01-15,INV-{i:D3},Acme Corp,Office Supplies,Item {i},100.00,21.00,Domestic,,");
        }
        var csv = string.Join(Environment.NewLine, lines);

        var result = _service.ParseAndValidate(csv, _activeSuppliers, _activeCategories);

        Assert.True(result.IsFileValid);
        Assert.Equal(500, result.Rows.Count);
    }

    [Fact]
    public void ParseAndValidate_EmptyContent_RejectsFile()
    {
        var result = _service.ParseAndValidate("", _activeSuppliers, _activeCategories);

        Assert.False(result.IsFileValid);
        Assert.Contains("empty", result.FileError);
    }

    [Fact]
    public void ParseAndValidate_HeaderOnly_RejectsFile()
    {
        var csv = "InvoiceDate,InvoiceNumber,SupplierName,ExpenseCategoryName,Description,AmountExcludingVat,VatAmount,PurchaseOriginType,Country,Notes";

        var result = _service.ParseAndValidate(csv, _activeSuppliers, _activeCategories);

        Assert.False(result.IsFileValid);
        Assert.Contains("header row and at least one data row", result.FileError);
    }

    [Fact]
    public void ParseAndValidate_EuReverseCharge_SetsVatToZero()
    {
        var csv = BuildCsv("2024-01-15,INV-001,Acme Corp,Software,EU purchase,500.00,100.00,EuReverseCharge,Germany,");

        var result = _service.ParseAndValidate(csv, _activeSuppliers, _activeCategories);

        Assert.True(result.IsFileValid);
        var row = result.Rows[0];
        Assert.True(row.IsValid);
        Assert.Equal(0m, row.VatAmount);
        Assert.Equal(2, row.ResolvedPurchaseOriginTypeId);
    }

    [Fact]
    public void ParseAndValidate_EuReverseCharge_WithoutCountry_FlagsInvalid()
    {
        var csv = BuildCsv("2024-01-15,INV-001,Acme Corp,Software,EU purchase,500.00,0.00,EuReverseCharge,,");

        var result = _service.ParseAndValidate(csv, _activeSuppliers, _activeCategories);

        Assert.True(result.IsFileValid);
        var row = result.Rows[0];
        Assert.False(row.IsValid);
        Assert.Contains("Country is required for EU Reverse Charge", row.ErrorMessage);
    }

    [Fact]
    public void ParseAndValidate_NonEu_WithoutCountry_FlagsInvalid()
    {
        var csv = BuildCsv("2024-01-15,INV-001,Acme Corp,Software,Non-EU purchase,500.00,50.00,NonEu,,");

        var result = _service.ParseAndValidate(csv, _activeSuppliers, _activeCategories);

        Assert.True(result.IsFileValid);
        var row = result.Rows[0];
        Assert.False(row.IsValid);
        Assert.Contains("Country is required for Non-EU", row.ErrorMessage);
    }

    [Fact]
    public void ParseAndValidate_NonEu_WithCountry_IsValid()
    {
        var csv = BuildCsv("2024-01-15,INV-001,Acme Corp,Software,Non-EU purchase,500.00,50.00,NonEu,United States,Import");

        var result = _service.ParseAndValidate(csv, _activeSuppliers, _activeCategories);

        Assert.True(result.IsFileValid);
        var row = result.Rows[0];
        Assert.True(row.IsValid);
        Assert.Equal(50.00m, row.VatAmount);
        Assert.Equal(3, row.ResolvedPurchaseOriginTypeId);
    }

    [Fact]
    public void ParseAndValidate_InvalidOriginType_FlagsInvalid()
    {
        var csv = BuildCsv("2024-01-15,INV-001,Acme Corp,Software,Test,500.00,50.00,InvalidType,,");

        var result = _service.ParseAndValidate(csv, _activeSuppliers, _activeCategories);

        Assert.True(result.IsFileValid);
        var row = result.Rows[0];
        Assert.False(row.IsValid);
        Assert.Contains("PurchaseOriginType", row.ErrorMessage);
    }

    [Fact]
    public void ParseAndValidate_NegativeVatAmount_FlagsInvalid()
    {
        var csv = BuildCsv("2024-01-15,INV-001,Acme Corp,Office Supplies,Test,100.00,-5.00,Domestic,,");

        var result = _service.ParseAndValidate(csv, _activeSuppliers, _activeCategories);

        Assert.True(result.IsFileValid);
        var row = result.Rows[0];
        Assert.False(row.IsValid);
        Assert.Contains("VatAmount cannot be negative", row.ErrorMessage);
    }

    [Fact]
    public void ParseAndValidate_ZeroAmount_FlagsInvalid()
    {
        var csv = BuildCsv("2024-01-15,INV-001,Acme Corp,Office Supplies,Test,0.00,0.00,Domestic,,");

        var result = _service.ParseAndValidate(csv, _activeSuppliers, _activeCategories);

        Assert.True(result.IsFileValid);
        var row = result.Rows[0];
        Assert.False(row.IsValid);
        Assert.Contains("AmountExcludingVat must be greater than zero", row.ErrorMessage);
    }

    [Fact]
    public void ParseAndValidate_MissingDescription_FlagsInvalid()
    {
        var csv = BuildCsv("2024-01-15,INV-001,Acme Corp,Office Supplies,,100.00,21.00,Domestic,,");

        var result = _service.ParseAndValidate(csv, _activeSuppliers, _activeCategories);

        Assert.True(result.IsFileValid);
        var row = result.Rows[0];
        Assert.False(row.IsValid);
        Assert.Contains("Description is required", row.ErrorMessage);
    }

    [Fact]
    public void ParseAndValidate_InvalidDate_FlagsInvalid()
    {
        var csv = BuildCsv("not-a-date,INV-001,Acme Corp,Office Supplies,Test,100.00,21.00,Domestic,,");

        var result = _service.ParseAndValidate(csv, _activeSuppliers, _activeCategories);

        Assert.True(result.IsFileValid);
        var row = result.Rows[0];
        Assert.False(row.IsValid);
        Assert.Contains("InvoiceDate", row.ErrorMessage);
    }

    [Fact]
    public void ParseAndValidate_QuotedFieldsWithCommas_ParsesCorrectly()
    {
        var csv = BuildCsv("2024-01-15,INV-001,Acme Corp,Office Supplies,\"Paper, pens, and clips\",100.00,21.00,Domestic,,");

        var result = _service.ParseAndValidate(csv, _activeSuppliers, _activeCategories);

        Assert.True(result.IsFileValid);
        var row = result.Rows[0];
        Assert.True(row.IsValid);
        Assert.Equal("Paper, pens, and clips", row.Description);
    }

    [Fact]
    public void ParseAndValidate_TooFewColumns_FlagsInvalid()
    {
        var csv = BuildCsv("2024-01-15,INV-001,Acme Corp");

        var result = _service.ParseAndValidate(csv, _activeSuppliers, _activeCategories);

        Assert.True(result.IsFileValid);
        var row = result.Rows[0];
        Assert.False(row.IsValid);
        Assert.Contains("Expected 10 columns", row.ErrorMessage);
    }

    [Fact]
    public void ParseAndValidate_MultipleRows_ValidatesEachIndependently()
    {
        var csv = BuildCsv(
            "2024-01-15,INV-001,Acme Corp,Office Supplies,Valid row,100.00,21.00,Domestic,,",
            "2024-01-16,INV-002,Unknown Vendor,Office Supplies,Invalid supplier,200.00,42.00,Domestic,,",
            "2024-01-17,INV-003,Tech Solutions,Software,Another valid,300.00,63.00,Domestic,,"
        );

        var result = _service.ParseAndValidate(csv, _activeSuppliers, _activeCategories);

        Assert.True(result.IsFileValid);
        Assert.Equal(3, result.Rows.Count);
        Assert.True(result.Rows[0].IsValid);
        Assert.False(result.Rows[1].IsValid);
        Assert.True(result.Rows[2].IsValid);
    }

    [Fact]
    public void ParseAndValidate_InvalidAmountFormat_FlagsInvalid()
    {
        var csv = BuildCsv("2024-01-15,INV-001,Acme Corp,Office Supplies,Test,abc,21.00,Domestic,,");

        var result = _service.ParseAndValidate(csv, _activeSuppliers, _activeCategories);

        Assert.True(result.IsFileValid);
        var row = result.Rows[0];
        Assert.False(row.IsValid);
        Assert.Contains("AmountExcludingVat", row.ErrorMessage);
        Assert.Contains("not a valid number", row.ErrorMessage);
    }

    [Fact]
    public void ParseCsvLine_HandlesEscapedQuotes()
    {
        var line = "field1,\"He said \"\"hello\"\"\",field3";

        var fields = CsvImportService.ParseCsvLine(line);

        Assert.Equal(3, fields.Count);
        Assert.Equal("field1", fields[0]);
        Assert.Equal("He said \"hello\"", fields[1]);
        Assert.Equal("field3", fields[2]);
    }
}
