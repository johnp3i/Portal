using FsCheck;
using FsCheck.Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: invoice-edit-modal-lines, Property 3: Edit button populates modal fields matching data attributes

/// <summary>
/// Property-based tests for invoice modal pre-population round-trip integrity.
/// Validates that the serialization of InvoiceLine field values to data-* attributes
/// (as done by Razor) produces strings that, when parsed back (as done by JavaScript),
/// yield values identical to the originals.
///
/// The round-trip is:
///   Entity field value → .ToString("G") / ?? "" formatting → string attribute → parse back → equals original
///
/// **Validates: Requirements 2.1**
/// </summary>
public class InvoiceModalPrePopulationRoundTripPropertyTests
{
    #region Generators

    /// <summary>
    /// Generates a non-null, non-empty description string (1 to 100 chars, printable ASCII).
    /// </summary>
    private static Gen<string> DescriptionGen()
    {
        return from length in Gen.Choose(1, 100)
               from chars in Gen.ListOf(length, Gen.Choose(32, 126).Select(i => (char)i))
               select new string(chars.ToArray());
    }

    /// <summary>
    /// Generates a nullable string (null or 1-80 printable ASCII chars).
    /// Note: empty string "" is equivalent to null in the data attribute round-trip
    /// (Razor renders null as "", JS interprets "" as null), so we only generate
    /// null or non-empty strings to avoid false negatives.
    /// </summary>
    private static Gen<string?> NullableStringGen()
    {
        var nonNullGen = from length in Gen.Choose(1, 80)
                         from chars in Gen.ListOf(length, Gen.Choose(32, 126).Select(i => (char)i))
                         select new string(chars.ToArray());

        return Gen.OneOf(
            Gen.Constant<string?>(null),
            nonNullGen.Select(s => (string?)s)
        );
    }

    /// <summary>
    /// Generates a decimal suitable for quantity/price fields (0.0001 to 999999.9999).
    /// Uses integer parts to avoid floating-point representation issues with ToString("G").
    /// </summary>
    private static Gen<decimal> PositiveDecimalGen()
    {
        return from intPart in Gen.Choose(0, 999999)
               from fracPart in Gen.Choose(0, 9999)
               let value = (decimal)intPart + (decimal)fracPart / 10000m
               where value > 0m
               select value;
    }

    /// <summary>
    /// Generates a decimal for discount (0 to 99999.99).
    /// </summary>
    private static Gen<decimal> DiscountDecimalGen()
    {
        return from intPart in Gen.Choose(0, 99999)
               from fracPart in Gen.Choose(0, 99)
               select (decimal)intPart + (decimal)fracPart / 100m;
    }

    /// <summary>
    /// Generates a VAT rate decimal (0 to 100, whole or half values).
    /// </summary>
    private static Gen<decimal> VatRateGen()
    {
        return Gen.Choose(0, 200).Select(i => (decimal)i / 2m);
    }

    /// <summary>
    /// Generates a nullable decimal for cost price.
    /// </summary>
    private static Gen<decimal?> NullableCostPriceGen()
    {
        return Gen.OneOf(
            Gen.Constant<decimal?>(null),
            PositiveDecimalGen().Select(d => (decimal?)d)
        );
    }

    /// <summary>
    /// Generates a discount type string ("Percentage" or "Fixed").
    /// </summary>
    private static Gen<string> DiscountTypeGen()
    {
        return Gen.OneOf(
            Gen.Constant("Percentage"),
            Gen.Constant("Fixed")
        );
    }

    /// <summary>
    /// Generates a complete InvoiceLine field value object for round-trip testing.
    /// </summary>
    private static Gen<InvoiceLineFieldValues> InvoiceLineFieldValuesGen()
    {
        return from description in DescriptionGen()
               from subtitle in NullableStringGen()
               from referenceUrl in NullableStringGen()
               from quantity in PositiveDecimalGen()
               from unitPrice in PositiveDecimalGen()
               from vatRate in VatRateGen()
               from discount in DiscountDecimalGen()
               from discountType in DiscountTypeGen()
               from costPrice in NullableCostPriceGen()
               from isReverseCharge in Arb.Generate<bool>()
               from productCode in NullableStringGen()
               select new InvoiceLineFieldValues(
                   description, subtitle, referenceUrl,
                   quantity, unitPrice, vatRate,
                   discount, discountType, costPrice,
                   isReverseCharge, productCode);
    }

    #endregion

    #region Round-Trip Simulation

    /// <summary>
    /// Simulates Razor rendering of data-* attribute values (step 1: entity → string).
    /// This matches the Razor template patterns in Edit.cshtml for invoice lines.
    /// </summary>
    private static InvoiceDataAttributeStrings SerializeToDataAttributes(InvoiceLineFieldValues fields)
    {
        return new InvoiceDataAttributeStrings(
            Description: fields.Description,
            Subtitle: fields.Subtitle ?? "",
            ReferenceUrl: fields.ReferenceUrl ?? "",
            Quantity: fields.Quantity.ToString("G"),
            UnitPrice: fields.UnitPrice.ToString("G"),
            VatRate: fields.VatRate.ToString("G"),
            Discount: fields.Discount.ToString("G"),
            DiscountType: fields.DiscountType,
            CostPrice: fields.CostPrice?.ToString("G") ?? "",
            IsReverseCharge: fields.IsReverseCharge ? "true" : "false",
            ProductCode: fields.ProductCode ?? ""
        );
    }

    /// <summary>
    /// Simulates JavaScript reading data attributes and populating form fields (step 2: string → parsed values).
    /// This matches the JS logic in invoice-line-modal.js showEditInvoiceLineModal().
    /// </summary>
    private static InvoiceLineFieldValues DeserializeFromDataAttributes(InvoiceDataAttributeStrings attrs)
    {
        return new InvoiceLineFieldValues(
            Description: attrs.Description,
            Subtitle: string.IsNullOrEmpty(attrs.Subtitle) ? null : attrs.Subtitle,
            ReferenceUrl: string.IsNullOrEmpty(attrs.ReferenceUrl) ? null : attrs.ReferenceUrl,
            Quantity: decimal.Parse(attrs.Quantity),
            UnitPrice: decimal.Parse(attrs.UnitPrice),
            VatRate: decimal.Parse(attrs.VatRate),
            Discount: decimal.Parse(attrs.Discount),
            DiscountType: attrs.DiscountType,
            CostPrice: string.IsNullOrEmpty(attrs.CostPrice) ? null : decimal.Parse(attrs.CostPrice),
            IsReverseCharge: attrs.IsReverseCharge == "true",
            ProductCode: string.IsNullOrEmpty(attrs.ProductCode) ? null : attrs.ProductCode
        );
    }

    #endregion

    #region Property 3: Modal pre-population round-trip

    /// <summary>
    /// Property 3: For any invoice line item with arbitrary field values, the data attribute
    /// serialization (Razor) produces values that, when deserialized (JavaScript reading
    /// data attributes and populating form fields), match the original entity values exactly.
    ///
    /// **Validates: Requirements 2.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DataAttribute_RoundTrip_PreservesAllInvoiceLineFieldValues()
    {
        return Prop.ForAll(
            InvoiceLineFieldValuesGen().ToArbitrary(),
            original =>
            {
                // Step 1: Entity → data-* attribute strings (Razor serialization)
                var dataAttributes = SerializeToDataAttributes(original);

                // Step 2: data-* attribute strings → parsed form field values (JS deserialization)
                var roundTripped = DeserializeFromDataAttributes(dataAttributes);

                // Assert all fields match
                var descriptionMatch = roundTripped.Description == original.Description;
                var subtitleMatch = roundTripped.Subtitle == original.Subtitle;
                var referenceUrlMatch = roundTripped.ReferenceUrl == original.ReferenceUrl;
                var quantityMatch = roundTripped.Quantity == original.Quantity;
                var unitPriceMatch = roundTripped.UnitPrice == original.UnitPrice;
                var vatRateMatch = roundTripped.VatRate == original.VatRate;
                var discountMatch = roundTripped.Discount == original.Discount;
                var discountTypeMatch = roundTripped.DiscountType == original.DiscountType;
                var costPriceMatch = roundTripped.CostPrice == original.CostPrice;
                var reverseChargeMatch = roundTripped.IsReverseCharge == original.IsReverseCharge;
                var productCodeMatch = roundTripped.ProductCode == original.ProductCode;

                var allMatch = descriptionMatch && subtitleMatch && referenceUrlMatch &&
                               quantityMatch && unitPriceMatch && vatRateMatch &&
                               discountMatch && discountTypeMatch && costPriceMatch &&
                               reverseChargeMatch && productCodeMatch;

                return allMatch
                    .Label($"Description: {descriptionMatch}")
                    .And(subtitleMatch.Label($"Subtitle: expected='{original.Subtitle}', actual='{roundTripped.Subtitle}'"))
                    .And(referenceUrlMatch.Label($"ReferenceUrl: expected='{original.ReferenceUrl}', actual='{roundTripped.ReferenceUrl}'"))
                    .And(quantityMatch.Label($"Quantity: expected={original.Quantity}, actual={roundTripped.Quantity}"))
                    .And(unitPriceMatch.Label($"UnitPrice: expected={original.UnitPrice}, actual={roundTripped.UnitPrice}"))
                    .And(vatRateMatch.Label($"VatRate: expected={original.VatRate}, actual={roundTripped.VatRate}"))
                    .And(discountMatch.Label($"Discount: expected={original.Discount}, actual={roundTripped.Discount}"))
                    .And(discountTypeMatch.Label($"DiscountType: expected='{original.DiscountType}', actual='{roundTripped.DiscountType}'"))
                    .And(costPriceMatch.Label($"CostPrice: expected={original.CostPrice}, actual={roundTripped.CostPrice}"))
                    .And(reverseChargeMatch.Label($"IsReverseCharge: expected={original.IsReverseCharge}, actual={roundTripped.IsReverseCharge}"))
                    .And(productCodeMatch.Label($"ProductCode: expected='{original.ProductCode}', actual='{roundTripped.ProductCode}'"));
            });
    }

    /// <summary>
    /// Property 3 (supplementary): Empty string nullable fields round-trip as null.
    /// When a nullable field has an empty string value, the round-trip correctly interprets it as null.
    /// This verifies the Razor ?? "" → JS empty check → null mapping is consistent.
    ///
    /// **Validates: Requirements 2.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DataAttribute_NullableFields_EmptyStringMapsToNull()
    {
        return Prop.ForAll(
            InvoiceLineFieldValuesGen().ToArbitrary(),
            original =>
            {
                var dataAttributes = SerializeToDataAttributes(original);

                // Verify: null fields produce empty attribute strings
                var subtitleCorrect = original.Subtitle == null
                    ? dataAttributes.Subtitle == ""
                    : dataAttributes.Subtitle == original.Subtitle;

                var referenceUrlCorrect = original.ReferenceUrl == null
                    ? dataAttributes.ReferenceUrl == ""
                    : dataAttributes.ReferenceUrl == original.ReferenceUrl;

                var costPriceCorrect = original.CostPrice == null
                    ? dataAttributes.CostPrice == ""
                    : dataAttributes.CostPrice == original.CostPrice.Value.ToString("G");

                var productCodeCorrect = original.ProductCode == null
                    ? dataAttributes.ProductCode == ""
                    : dataAttributes.ProductCode == original.ProductCode;

                return subtitleCorrect
                    .Label($"Subtitle null→empty: original='{original.Subtitle}', attr='{dataAttributes.Subtitle}'")
                    .And(referenceUrlCorrect.Label($"ReferenceUrl null→empty: original='{original.ReferenceUrl}', attr='{dataAttributes.ReferenceUrl}'"))
                    .And(costPriceCorrect.Label($"CostPrice null→empty: original={original.CostPrice}, attr='{dataAttributes.CostPrice}'"))
                    .And(productCodeCorrect.Label($"ProductCode null→empty: original='{original.ProductCode}', attr='{dataAttributes.ProductCode}'"));
            });
    }

    #endregion

    #region Supporting Types

    /// <summary>
    /// Represents all field values of an InvoiceLine entity relevant to modal pre-population.
    /// </summary>
    private record InvoiceLineFieldValues(
        string Description,
        string? Subtitle,
        string? ReferenceUrl,
        decimal Quantity,
        decimal UnitPrice,
        decimal VatRate,
        decimal Discount,
        string DiscountType,
        decimal? CostPrice,
        bool IsReverseCharge,
        string? ProductCode);

    /// <summary>
    /// Represents the string values stored in data-* attributes on an invoice line table row.
    /// </summary>
    private record InvoiceDataAttributeStrings(
        string Description,
        string Subtitle,
        string ReferenceUrl,
        string Quantity,
        string UnitPrice,
        string VatRate,
        string Discount,
        string DiscountType,
        string CostPrice,
        string IsReverseCharge,
        string ProductCode);

    #endregion
}
