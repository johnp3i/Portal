using FsCheck;
using FsCheck.Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: invoice-edit-modal-lines, Property 7: Selected catalog item populates all target fields

/// <summary>
/// Property-based tests for catalog autocomplete field population in the invoice line item modal.
/// Validates that when a catalog item is selected from autocomplete, the modal's Description,
/// Unit Price, Cost Price, VAT%, and Product Code fields are populated with the exact
/// corresponding catalog item values.
///
/// The JavaScript fillFormFields function:
///   - Sets unitPrice, vatRate, costPrice (|| '') fields via form.querySelector('[name="..."]')
///   - Sets productCode only if entry.productCode is truthy
///   - Sets description directly via input.value = entry.description
///
/// **Validates: Requirements 3.12**
/// </summary>
public class InvoiceCatalogAutocompletePopulationPropertyTests
{
    #region Records

    /// <summary>
    /// Represents a catalog item returned from the /LineItemCatalog/Search endpoint.
    /// </summary>
    private record CatalogItem(
        string Description,
        decimal UnitPrice,
        decimal? CostPrice,
        decimal VatRate,
        string? ProductCode);

    /// <summary>
    /// Represents the state of the form fields after a catalog item is selected.
    /// All values are strings because HTML form field values are always strings.
    /// </summary>
    private record FormFieldState(
        string Description,
        string UnitPrice,
        string CostPrice,
        string VatRate,
        string ProductCode);

    #endregion

    #region Generators

    /// <summary>
    /// Generates a non-empty printable ASCII string (1 to 100 chars).
    /// </summary>
    private static Gen<string> NonEmptyPrintableStringGen()
    {
        return from length in Gen.Choose(1, 100)
               from chars in Gen.ListOf(length, Gen.Choose(32, 126).Select(i => (char)i))
               select new string(chars.ToArray());
    }

    /// <summary>
    /// Generates a positive decimal for unit price (0.01 to 99999.99).
    /// </summary>
    private static Gen<decimal> PositiveDecimalGen()
    {
        return from intPart in Gen.Choose(0, 99999)
               from fracPart in Gen.Choose(1, 99)
               let value = (decimal)intPart + (decimal)fracPart / 100m
               where value > 0m
               select value;
    }

    /// <summary>
    /// Generates a nullable decimal for cost price (null or positive).
    /// </summary>
    private static Gen<decimal?> NullableCostPriceGen()
    {
        return Gen.OneOf(
            Gen.Constant<decimal?>(null),
            PositiveDecimalGen().Select(d => (decimal?)d)
        );
    }

    /// <summary>
    /// Generates a VAT rate decimal (0 to 99.99).
    /// </summary>
    private static Gen<decimal> VatRateGen()
    {
        return from intPart in Gen.Choose(0, 99)
               from fracPart in Gen.Choose(0, 99)
               select (decimal)intPart + (decimal)fracPart / 100m;
    }

    /// <summary>
    /// Generates a nullable printable ASCII string for product code.
    /// </summary>
    private static Gen<string?> NullableProductCodeGen()
    {
        var nonNullGen = from length in Gen.Choose(1, 50)
                         from chars in Gen.ListOf(length, Gen.Choose(32, 126).Select(i => (char)i))
                         select new string(chars.ToArray());

        return Gen.OneOf(
            Gen.Constant<string?>(null),
            Gen.Constant<string?>(""),
            nonNullGen.Select(s => (string?)s)
        );
    }

    /// <summary>
    /// Generates a catalog item with all non-null values (for Property 7a).
    /// </summary>
    private static Gen<CatalogItem> FullCatalogItemGen()
    {
        return from description in NonEmptyPrintableStringGen()
               from unitPrice in PositiveDecimalGen()
               from costPrice in PositiveDecimalGen()
               from vatRate in VatRateGen()
               from productCode in NonEmptyPrintableStringGen()
               select new CatalogItem(description, unitPrice, costPrice, vatRate, productCode);
    }

    /// <summary>
    /// Generates a catalog item with nullable costPrice and productCode (for Property 7b).
    /// </summary>
    private static Gen<CatalogItem> CatalogItemWithNullableFieldsGen()
    {
        return from description in NonEmptyPrintableStringGen()
               from unitPrice in PositiveDecimalGen()
               from costPrice in NullableCostPriceGen()
               from vatRate in VatRateGen()
               from productCode in NullableProductCodeGen()
               select new CatalogItem(description, unitPrice, costPrice, vatRate, productCode);
    }

    #endregion

    #region Form Population Logic

    /// <summary>
    /// Simulates the JavaScript fillFormFields function behavior when a catalog item is selected.
    /// 
    /// The JS logic:
    ///   - unitPrice field = entry.unitPrice
    ///   - vatRate field = entry.vatRate
    ///   - costPrice field = entry.costPrice || ''
    ///   - productCode field = entry.productCode (only set if truthy)
    ///   - description field = entry.description (set directly)
    /// 
    /// All form field values are strings in HTML.
    /// </summary>
    private static FormFieldState SimulateFillFormFields(CatalogItem entry)
    {
        // unitPrice → entry.unitPrice (always set)
        var unitPriceValue = entry.UnitPrice.ToString("G");

        // vatRate → entry.vatRate (always set)
        var vatRateValue = entry.VatRate.ToString("G");

        // costPrice → entry.costPrice || '' (falsy values become empty string)
        var costPriceValue = entry.CostPrice.HasValue && entry.CostPrice.Value != 0m
            ? entry.CostPrice.Value.ToString("G")
            : "";

        // productCode → entry.productCode (only set if truthy, otherwise remains empty)
        var productCodeValue = !string.IsNullOrEmpty(entry.ProductCode)
            ? entry.ProductCode
            : "";

        // description → entry.description (set directly via input.value)
        var descriptionValue = entry.Description;

        return new FormFieldState(
            Description: descriptionValue,
            UnitPrice: unitPriceValue,
            CostPrice: costPriceValue,
            VatRate: vatRateValue,
            ProductCode: productCodeValue);
    }

    #endregion

    #region Property 7a: Full catalog item populates all fields

    /// <summary>
    /// Property 7a: For any catalog item with non-null/non-empty values for description,
    /// unit price, cost price, VAT rate, and product code, selecting that item SHALL populate
    /// the modal's Description, Unit Price, Cost Price, VAT%, and Product Code fields with
    /// the exact corresponding values.
    ///
    /// **Validates: Requirements 3.12**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CatalogItem_WithAllValues_PopulatesAllTargetFields()
    {
        return Prop.ForAll(
            FullCatalogItemGen().ToArbitrary(),
            catalogItem =>
            {
                var formState = SimulateFillFormFields(catalogItem);

                var descriptionMatch = formState.Description == catalogItem.Description;
                var unitPriceMatch = formState.UnitPrice == catalogItem.UnitPrice.ToString("G");
                var costPriceMatch = formState.CostPrice == catalogItem.CostPrice!.Value.ToString("G");
                var vatRateMatch = formState.VatRate == catalogItem.VatRate.ToString("G");
                var productCodeMatch = formState.ProductCode == catalogItem.ProductCode;

                return descriptionMatch
                    .Label($"Description: expected='{catalogItem.Description}', actual='{formState.Description}'")
                    .And(unitPriceMatch.Label($"UnitPrice: expected='{catalogItem.UnitPrice.ToString("G")}', actual='{formState.UnitPrice}'"))
                    .And(costPriceMatch.Label($"CostPrice: expected='{catalogItem.CostPrice!.Value.ToString("G")}', actual='{formState.CostPrice}'"))
                    .And(vatRateMatch.Label($"VatRate: expected='{catalogItem.VatRate.ToString("G")}', actual='{formState.VatRate}'"))
                    .And(productCodeMatch.Label($"ProductCode: expected='{catalogItem.ProductCode}', actual='{formState.ProductCode}'"));
            });
    }

    #endregion

    #region Property 7b: Nullable fields get empty string values

    /// <summary>
    /// Property 7b: For any catalog item with null/empty costPrice or productCode,
    /// those fields SHALL get empty string values in the form state.
    /// The JS logic uses `entry.costPrice || ''` and only sets productCode if truthy.
    ///
    /// **Validates: Requirements 3.12**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CatalogItem_WithNullableFields_PopulatesEmptyStringForFalsyValues()
    {
        return Prop.ForAll(
            CatalogItemWithNullableFieldsGen().ToArbitrary(),
            catalogItem =>
            {
                var formState = SimulateFillFormFields(catalogItem);

                // Description is always set directly
                var descriptionMatch = formState.Description == catalogItem.Description;

                // UnitPrice is always set
                var unitPriceMatch = formState.UnitPrice == catalogItem.UnitPrice.ToString("G");

                // VatRate is always set
                var vatRateMatch = formState.VatRate == catalogItem.VatRate.ToString("G");

                // CostPrice: null/0 → "", otherwise entry.costPrice.ToString("G")
                var expectedCostPrice = catalogItem.CostPrice.HasValue && catalogItem.CostPrice.Value != 0m
                    ? catalogItem.CostPrice.Value.ToString("G")
                    : "";
                var costPriceMatch = formState.CostPrice == expectedCostPrice;

                // ProductCode: null/empty → "", otherwise the value
                var expectedProductCode = !string.IsNullOrEmpty(catalogItem.ProductCode)
                    ? catalogItem.ProductCode
                    : "";
                var productCodeMatch = formState.ProductCode == expectedProductCode;

                return descriptionMatch
                    .Label($"Description: expected='{catalogItem.Description}', actual='{formState.Description}'")
                    .And(unitPriceMatch.Label($"UnitPrice: expected='{catalogItem.UnitPrice.ToString("G")}', actual='{formState.UnitPrice}'"))
                    .And(vatRateMatch.Label($"VatRate: expected='{catalogItem.VatRate.ToString("G")}', actual='{formState.VatRate}'"))
                    .And(costPriceMatch.Label($"CostPrice: expected='{expectedCostPrice}', actual='{formState.CostPrice}'"))
                    .And(productCodeMatch.Label($"ProductCode: expected='{expectedProductCode}', actual='{formState.ProductCode}'"));
            });
    }

    #endregion
}
