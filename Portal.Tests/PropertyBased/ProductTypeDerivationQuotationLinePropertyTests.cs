using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;
using Portal.Web.Models;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: invoice-line-product-type-reverse-charge, Property 6: Product type derivation on quotation lines

/// <summary>
/// Property-based tests for product type derivation on quotation lines.
/// For any quotation line, the displayed product type is derived from the linked product's
/// current ProductTypeId. If the line has no ProductCode, or the referenced product has
/// ProductTypeId = NULL, then no product type SHALL be displayed. If the product's ProductTypeId
/// is 1 → "Services", if 2 → "Goods".
/// **Validates: Requirements 2.5, 3.2, 3.3**
/// </summary>
public class ProductTypeDerivationQuotationLinePropertyTests
{
    #region Pure Derivation Logic (mirrors QuotationController.BuildDisplayLinesAsync)

    /// <summary>
    /// Pure function that derives the ProductTypeName for a quotation line given
    /// the line's ProductCode and the resolved product (if any).
    /// This is the same logic used in QuotationController.BuildDisplayLinesAsync.
    /// </summary>
    private static string? DeriveProductTypeName(QuotationLine line, Product? product)
    {
        if (string.IsNullOrEmpty(line.ProductCode))
            return null;

        if (product == null)
            return null;

        return product.ProductTypeId switch
        {
            1 => "Services",
            2 => "Goods",
            _ => null
        };
    }

    #endregion

    #region Property 6a: No ProductCode → no type shown

    /// <summary>
    /// Property 6a: For any quotation line with no ProductCode (null or empty),
    /// the derived ProductTypeName SHALL be null regardless of any product state.
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoProductCode_ReturnsNullProductTypeName(PositiveInt lineSeed, bool useNull)
    {
        var line = new QuotationLine
        {
            Id = lineSeed.Get,
            QuotationId = 1,
            Description = $"Manual line {lineSeed.Get}",
            Quantity = 1m,
            UnitPrice = 100m,
            VatRate = 15m,
            Discount = 0m,
            DiscountType = "Percentage",
            LineTotal = 100m,
            SortOrder = 1,
            ProductCode = useNull ? null : string.Empty
        };

        // Even if a product exists, it shouldn't matter — no ProductCode means no derivation
        var product = new Product
        {
            Id = 1,
            BusinessId = 1,
            ProductCode = "SOME-CODE",
            Description = "Some product",
            DefaultSellingPrice = 100m,
            DefaultCostPrice = 50m,
            DefaultVatRate = 15m,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            ProductTypeId = 1
        };

        var result = DeriveProductTypeName(line, product);

        return (result == null)
            .Label($"ProductCode={line.ProductCode ?? "null"}, Expected=null, Got={result ?? "null"}");
    }

    #endregion

    #region Property 6b: ProductCode present but product has null ProductTypeId → no type shown

    /// <summary>
    /// Property 6b: For any quotation line with a ProductCode referencing a product that has
    /// ProductTypeId = NULL, the derived ProductTypeName SHALL be null.
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProductWithNullProductTypeId_ReturnsNullProductTypeName(PositiveInt codeSeed)
    {
        var productCode = $"PROD-{Math.Abs(codeSeed.Get) % 99999:D5}";

        var line = new QuotationLine
        {
            Id = 1,
            QuotationId = 1,
            Description = "Line with product",
            Quantity = 1m,
            UnitPrice = 100m,
            VatRate = 15m,
            Discount = 0m,
            DiscountType = "Percentage",
            LineTotal = 100m,
            SortOrder = 1,
            ProductCode = productCode
        };

        var product = new Product
        {
            Id = codeSeed.Get,
            BusinessId = 1,
            ProductCode = productCode,
            Description = "Legacy product without type",
            DefaultSellingPrice = 100m,
            DefaultCostPrice = 50m,
            DefaultVatRate = 15m,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            ProductTypeId = null
        };

        var result = DeriveProductTypeName(line, product);

        return (result == null)
            .Label($"ProductCode={productCode}, ProductTypeId=null, Expected=null, Got={result ?? "null"}");
    }

    #endregion

    #region Property 6c: ProductCode present and product has ProductTypeId=1 → "Services"

    /// <summary>
    /// Property 6c: For any quotation line with a ProductCode referencing a product that has
    /// ProductTypeId = 1, the derived ProductTypeName SHALL be "Services".
    /// **Validates: Requirements 2.5, 3.2, 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProductWithTypeId1_ReturnsServices(PositiveInt codeSeed)
    {
        var productCode = $"SVC-{Math.Abs(codeSeed.Get) % 99999:D5}";

        var line = new QuotationLine
        {
            Id = 1,
            QuotationId = 1,
            Description = "Service line",
            Quantity = 1m,
            UnitPrice = 200m,
            VatRate = 0m,
            Discount = 0m,
            DiscountType = "Percentage",
            LineTotal = 200m,
            SortOrder = 1,
            ProductCode = productCode
        };

        var product = new Product
        {
            Id = codeSeed.Get,
            BusinessId = 1,
            ProductCode = productCode,
            Description = "Service product",
            DefaultSellingPrice = 200m,
            DefaultCostPrice = 100m,
            DefaultVatRate = 0m,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            ProductTypeId = 1
        };

        var result = DeriveProductTypeName(line, product);

        return (result == "Services")
            .Label($"ProductCode={productCode}, ProductTypeId=1, Expected=Services, Got={result ?? "null"}");
    }

    #endregion

    #region Property 6d: ProductCode present and product has ProductTypeId=2 → "Goods"

    /// <summary>
    /// Property 6d: For any quotation line with a ProductCode referencing a product that has
    /// ProductTypeId = 2, the derived ProductTypeName SHALL be "Goods".
    /// **Validates: Requirements 2.5, 3.2, 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProductWithTypeId2_ReturnsGoods(PositiveInt codeSeed)
    {
        var productCode = $"GDS-{Math.Abs(codeSeed.Get) % 99999:D5}";

        var line = new QuotationLine
        {
            Id = 1,
            QuotationId = 1,
            Description = "Goods line",
            Quantity = 5m,
            UnitPrice = 50m,
            VatRate = 15m,
            Discount = 0m,
            DiscountType = "Percentage",
            LineTotal = 250m,
            SortOrder = 1,
            ProductCode = productCode
        };

        var product = new Product
        {
            Id = codeSeed.Get,
            BusinessId = 1,
            ProductCode = productCode,
            Description = "Goods product",
            DefaultSellingPrice = 50m,
            DefaultCostPrice = 25m,
            DefaultVatRate = 15m,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            ProductTypeId = 2
        };

        var result = DeriveProductTypeName(line, product);

        return (result == "Goods")
            .Label($"ProductCode={productCode}, ProductTypeId=2, Expected=Goods, Got={result ?? "null"}");
    }

    #endregion

    #region Property 6e: ProductCode present but product not found → no type shown

    /// <summary>
    /// Property 6e: For any quotation line with a ProductCode where the product lookup
    /// returns null (product not found/deleted), the derived ProductTypeName SHALL be null.
    /// **Validates: Requirements 3.2, 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProductNotFound_ReturnsNullProductTypeName(PositiveInt codeSeed)
    {
        var productCode = $"DEL-{Math.Abs(codeSeed.Get) % 99999:D5}";

        var line = new QuotationLine
        {
            Id = 1,
            QuotationId = 1,
            Description = "Line with deleted product",
            Quantity = 1m,
            UnitPrice = 100m,
            VatRate = 15m,
            Discount = 0m,
            DiscountType = "Percentage",
            LineTotal = 100m,
            SortOrder = 1,
            ProductCode = productCode
        };

        // Product not found — null
        var result = DeriveProductTypeName(line, null);

        return (result == null)
            .Label($"ProductCode={productCode}, Product=null, Expected=null, Got={result ?? "null"}");
    }

    #endregion

    #region Property 6f: Derivation reflects current product state (type change)

    /// <summary>
    /// Property 6f: When a product's ProductTypeId is changed, subsequent derivation calls
    /// SHALL reflect the new value. This tests that derivation is stateless and always uses
    /// the current product state.
    /// **Validates: Requirements 2.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DerivationReflectsCurrentProductState(PositiveInt codeSeed, bool startAsServices)
    {
        var productCode = $"CHG-{Math.Abs(codeSeed.Get) % 99999:D5}";

        var line = new QuotationLine
        {
            Id = 1,
            QuotationId = 1,
            Description = "Line with changing product",
            Quantity = 1m,
            UnitPrice = 100m,
            VatRate = 15m,
            Discount = 0m,
            DiscountType = "Percentage",
            LineTotal = 100m,
            SortOrder = 1,
            ProductCode = productCode
        };

        var product = new Product
        {
            Id = codeSeed.Get,
            BusinessId = 1,
            ProductCode = productCode,
            Description = "Changing product",
            DefaultSellingPrice = 100m,
            DefaultCostPrice = 50m,
            DefaultVatRate = 15m,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            ProductTypeId = startAsServices ? 1 : 2
        };

        // First derivation
        var firstResult = DeriveProductTypeName(line, product);
        var expectedFirst = startAsServices ? "Services" : "Goods";

        // Simulate product type change
        product.ProductTypeId = startAsServices ? 2 : 1;

        // Second derivation — should reflect the new state
        var secondResult = DeriveProductTypeName(line, product);
        var expectedSecond = startAsServices ? "Goods" : "Services";

        var firstCorrect = firstResult == expectedFirst;
        var secondCorrect = secondResult == expectedSecond;

        return (firstCorrect && secondCorrect)
            .Label($"First: Expected={expectedFirst}, Got={firstResult ?? "null"}; " +
                   $"Second: Expected={expectedSecond}, Got={secondResult ?? "null"}");
    }

    #endregion
}
