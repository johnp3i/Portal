using System.Reflection;
using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;
using Portal.Web.Controllers;
using Portal.Web.Models;

namespace Portal.Tests.Unit.Properties;

/// <summary>
/// Property-based tests for form mapping consistency between single-form and bulk-entry paths.
/// Tests Property 8 from the purchase-classification-enhancements design document.
/// </summary>
public class FormMappingConsistencyPropertyTests
{
    // Feature: purchase-classification-enhancements, Property 8: Form Mapping Consistency
    // **Validates: Requirements 4.5**

    #region Generators

    /// <summary>
    /// Generates a valid positive integer for IDs (SupplierId, ExpenseCategoryId).
    /// </summary>
    private static Gen<int> PositiveIdGen()
    {
        return Gen.Choose(1, 10000);
    }

    /// <summary>
    /// Generates a valid PurchaseOriginTypeId in {1, 2, 3, 4}.
    /// </summary>
    private static Gen<int> ValidOriginTypeIdGen()
    {
        return Gen.Elements(1, 2, 3, 4);
    }

    /// <summary>
    /// Generates a valid PurchaseTypeId in {1, 2, 3}.
    /// </summary>
    private static Gen<int> ValidPurchaseTypeIdGen()
    {
        return Gen.Elements(1, 2, 3);
    }

    /// <summary>
    /// Generates a valid DateOnly within a reasonable range.
    /// </summary>
    private static Gen<DateOnly> ValidDateGen()
    {
        return Gen.Choose(0, 3650)
            .Select(days => DateOnly.FromDateTime(new DateTime(2020, 1, 1).AddDays(days)));
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
    /// Generates a safe non-empty string (no special characters that could cause issues).
    /// </summary>
    private static Gen<string> SafeStringGen(int minLength = 3, int maxLength = 20)
    {
        return Gen.Choose(minLength, maxLength).SelectMany(len =>
            Gen.ArrayOf(len, Gen.Elements(
                'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
                'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
                'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
                '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '-', '_'))
            .Select(chars => new string(chars).Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    /// <summary>
    /// Generates an optional nullable string (for InvoiceNumber which can be null).
    /// </summary>
    private static Gen<string?> OptionalStringGen()
    {
        return Gen.OneOf(
            Gen.Constant<string?>(null),
            SafeStringGen(3, 15).Select<string, string?>(s => s));
    }

    /// <summary>
    /// Record holding all valid purchase input data for both mapping paths.
    /// </summary>
    private record PurchaseInputData(
        int SupplierId,
        int ExpenseCategoryId,
        int PurchaseOriginTypeId,
        int PurchaseTypeId,
        DateOnly InvoiceDate,
        string? InvoiceNumber,
        string? Description,
        decimal AmountExcludingVat,
        decimal VatAmount,
        string? Country);

    /// <summary>
    /// Generates valid purchase input data. For origin types 2, 3, 4 a non-empty Country is generated.
    /// </summary>
    private static Gen<PurchaseInputData> ValidPurchaseInputGen()
    {
        return from supplierId in PositiveIdGen()
               from expenseCategoryId in PositiveIdGen()
               from originTypeId in ValidOriginTypeIdGen()
               from purchaseTypeId in ValidPurchaseTypeIdGen()
               from invoiceDate in ValidDateGen()
               from invoiceNumber in OptionalStringGen()
               from description in OptionalStringGen()
               from amount in PositiveAmountGen()
               from vat in NonNegativeAmountGen()
               from country in SafeStringGen(2, 15)
               select new PurchaseInputData(
                   supplierId,
                   expenseCategoryId,
                   originTypeId,
                   purchaseTypeId,
                   invoiceDate,
                   invoiceNumber,
                   description,
                   amount,
                   vat,
                   originTypeId >= 2 ? country : null);
    }

    #endregion

    #region Mapping Helpers

    /// <summary>
    /// Invokes the private static MapFormToEntity method on PurchaseController via reflection.
    /// </summary>
    private static Purchase InvokeMapFormToEntity(PurchaseFormViewModel model)
    {
        var method = typeof(PurchaseController).GetMethod(
            "MapFormToEntity",
            BindingFlags.NonPublic | BindingFlags.Static);

        if (method == null)
            throw new InvalidOperationException("MapFormToEntity method not found on PurchaseController.");

        return (Purchase)method.Invoke(null, new object[] { model })!;
    }

    /// <summary>
    /// Replicates the exact bulk entry mapping logic from PurchaseController.BulkCreate.
    /// This mirrors the inline mapping in the controller action.
    /// </summary>
    private static Purchase MapBulkRowToEntity(BulkPurchaseRowDto row)
    {
        return new Purchase
        {
            SupplierId = row.SupplierId,
            ExpenseCategoryId = row.ExpenseCategoryId,
            PurchaseOriginTypeId = row.PurchaseOriginTypeId,
            PurchaseTypeId = row.PurchaseTypeId,
            InvoiceNumber = row.InvoiceNumber,
            InvoiceDate = row.InvoiceDate,
            Description = row.Description,
            AmountExcludingVat = row.AmountExcludingVat,
            VatAmount = row.VatAmount,
            Country = row.Country
        };
    }

    #endregion

    #region Property 8: Form Mapping Consistency

    // Feature: purchase-classification-enhancements, Property 8: Form Mapping Consistency
    // **Validates: Requirements 4.5**
    [Property(MaxTest = 100)]
    public Property SameInputData_MappedThroughBothPaths_ProducesIdenticalEntities()
    {
        return Prop.ForAll(
            ValidPurchaseInputGen().ToArbitrary(),
            (input) =>
            {
                // Map through single-form path (MapFormToEntity)
                var formModel = new PurchaseFormViewModel
                {
                    SupplierId = input.SupplierId,
                    ExpenseCategoryId = input.ExpenseCategoryId,
                    PurchaseOriginTypeId = input.PurchaseOriginTypeId,
                    PurchaseTypeId = input.PurchaseTypeId,
                    InvoiceDate = input.InvoiceDate,
                    InvoiceNumber = input.InvoiceNumber,
                    Description = input.Description,
                    AmountExcludingVat = input.AmountExcludingVat,
                    VatAmount = input.VatAmount,
                    Country = input.Country
                };

                var singleFormEntity = InvokeMapFormToEntity(formModel);

                // Map through bulk-entry path
                var bulkRow = new BulkPurchaseRowDto
                {
                    SupplierId = input.SupplierId,
                    ExpenseCategoryId = input.ExpenseCategoryId,
                    PurchaseOriginTypeId = input.PurchaseOriginTypeId,
                    PurchaseTypeId = input.PurchaseTypeId,
                    InvoiceDate = input.InvoiceDate,
                    InvoiceNumber = input.InvoiceNumber,
                    Description = input.Description!,
                    AmountExcludingVat = input.AmountExcludingVat,
                    VatAmount = input.VatAmount,
                    Country = input.Country
                };

                var bulkEntity = MapBulkRowToEntity(bulkRow);

                // Compare all user-entered fields
                return (singleFormEntity.SupplierId == bulkEntity.SupplierId)
                    .Label($"SupplierId mismatch: single={singleFormEntity.SupplierId}, bulk={bulkEntity.SupplierId}")
                    .And((singleFormEntity.ExpenseCategoryId == bulkEntity.ExpenseCategoryId)
                        .Label($"ExpenseCategoryId mismatch: single={singleFormEntity.ExpenseCategoryId}, bulk={bulkEntity.ExpenseCategoryId}"))
                    .And((singleFormEntity.PurchaseOriginTypeId == bulkEntity.PurchaseOriginTypeId)
                        .Label($"PurchaseOriginTypeId mismatch: single={singleFormEntity.PurchaseOriginTypeId}, bulk={bulkEntity.PurchaseOriginTypeId}"))
                    .And((singleFormEntity.PurchaseTypeId == bulkEntity.PurchaseTypeId)
                        .Label($"PurchaseTypeId mismatch: single={singleFormEntity.PurchaseTypeId}, bulk={bulkEntity.PurchaseTypeId}"))
                    .And((singleFormEntity.InvoiceDate == bulkEntity.InvoiceDate)
                        .Label($"InvoiceDate mismatch: single={singleFormEntity.InvoiceDate}, bulk={bulkEntity.InvoiceDate}"))
                    .And((singleFormEntity.InvoiceNumber == bulkEntity.InvoiceNumber)
                        .Label($"InvoiceNumber mismatch: single='{singleFormEntity.InvoiceNumber}', bulk='{bulkEntity.InvoiceNumber}'"))
                    .And((singleFormEntity.Description == bulkEntity.Description)
                        .Label($"Description mismatch: single='{singleFormEntity.Description}', bulk='{bulkEntity.Description}'"))
                    .And((singleFormEntity.AmountExcludingVat == bulkEntity.AmountExcludingVat)
                        .Label($"AmountExcludingVat mismatch: single={singleFormEntity.AmountExcludingVat}, bulk={bulkEntity.AmountExcludingVat}"))
                    .And((singleFormEntity.VatAmount == bulkEntity.VatAmount)
                        .Label($"VatAmount mismatch: single={singleFormEntity.VatAmount}, bulk={bulkEntity.VatAmount}"))
                    .And((singleFormEntity.Country == bulkEntity.Country)
                        .Label($"Country mismatch: single='{singleFormEntity.Country}', bulk='{bulkEntity.Country}'"));
            });
    }

    #endregion
}
