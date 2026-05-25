using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.ProductCatalog;

// Feature: product-catalog, Property 15: Auto-population matching priority
// Feature: product-catalog, Property 16: LastUsedDate update on match
// Feature: product-catalog, Property 17: Auto-creation when no match exists and ProductCode is present
// Feature: product-catalog, Property 18: No auto-creation without ProductCode
// Feature: product-catalog, Property 19: Existing product prices preserved on auto-population match

/// <summary>
/// Property-based tests for auto-population logic in ProductService.AutoPopulateFromLineItemAsync.
/// Tests Properties 15, 16, 17, 18, and 19 from the product-catalog design document.
/// Uses FsCheck.Xunit with Moq to mock repositories and ICurrentTenantService.
/// **Validates: Requirements 5.1, 5.2, 5.3, 5.4, 5.5, 5.7**
/// </summary>
public class AutoPopulationPropertyTests
{
    private const int TestBusinessId = 1;
    private const int ExistingProductId = 99;
    private const int GeneratedProductId = 42;

    #region Test Infrastructure

    private static (ProductService Service, Mock<ProductRepository> ProductRepo, Mock<ProductPriceHistoryRepository> PriceHistoryRepo) CreateService(int businessId = TestBusinessId)
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(businessId);

        var productRepoMock = new Mock<ProductRepository>(MockBehavior.Loose, new object[] { null! });
        var priceHistoryRepoMock = new Mock<ProductPriceHistoryRepository>(MockBehavior.Loose, new object[] { null! });
        var supplierRepoMock = new Mock<SupplierRepository>(MockBehavior.Loose, new object[] { null! });
        var loggerMock = new Mock<ILogger<ProductService>>();

        var service = new ProductService(
            tenantMock.Object,
            productRepoMock.Object,
            priceHistoryRepoMock.Object,
            supplierRepoMock.Object,
            loggerMock.Object);

        return (service, productRepoMock, priceHistoryRepoMock);
    }

    /// <summary>
    /// Generates a valid ProductCode: non-empty, trimmed, max 50 chars.
    /// </summary>
    private static Gen<string> ValidProductCodeGen()
    {
        return Gen.Choose(1, 50)
            .SelectMany(len => Gen.ArrayOf(len, Gen.Elements(
                "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_".ToCharArray()))
            .Select(chars => new string(chars)));
    }

    /// <summary>
    /// Generates a valid Description: non-empty, trimmed, max 500 chars.
    /// Ensures at least one non-whitespace character to pass validation.
    /// </summary>
    private static Gen<string> ValidDescriptionGen()
    {
        return Gen.Choose(1, 100)
            .SelectMany(len => Gen.ArrayOf(len, Gen.Elements(
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 -_.,".ToCharArray()))
            .Select(chars => new string(chars)))
            .Where(s => !string.IsNullOrWhiteSpace(s));
    }

    /// <summary>
    /// Generates a valid price: decimal >= 0, max 2 decimal places.
    /// </summary>
    private static Gen<decimal> ValidPriceGen()
    {
        return Gen.Choose(0, 999999)
            .Select(cents => cents / 100m);
    }

    /// <summary>
    /// Generates a valid VAT rate: 0.00 to 99.99.
    /// </summary>
    private static Gen<decimal> ValidVatRateGen()
    {
        return Gen.Choose(0, 9999)
            .Select(hundredths => hundredths / 100m);
    }

    /// <summary>
    /// Creates an existing product with the given ProductCode and Description for matching tests.
    /// </summary>
    private static Product CreateExistingProduct(string productCode, string description, decimal sellingPrice = 50.00m, decimal costPrice = 25.00m)
    {
        return new Product
        {
            Id = ExistingProductId,
            BusinessId = TestBusinessId,
            ProductCode = productCode,
            Description = description,
            DefaultSellingPrice = sellingPrice,
            DefaultCostPrice = costPrice,
            DefaultVatRate = 15.00m,
            IsActive = true,
            LastUsedDate = DateTime.UtcNow.AddDays(-10),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
        };
    }

    #endregion

    #region Property 15: Auto-population matching priority

    /// <summary>
    /// Property 15: For any new line item with a ProductCode, the system SHALL first search for an
    /// existing Product with matching ProductCode (case-insensitive) for the same BusinessId.
    /// When a ProductCode is provided and matches, the Description-based search SHALL NOT be invoked.
    /// **Validates: Requirements 5.1, 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AutoPopulation_MatchesByProductCodeFirst()
    {
        var arb = Arb.From(
            from productCode in ValidProductCodeGen()
            from description in ValidDescriptionGen()
            from unitPrice in ValidPriceGen()
            from vatRate in ValidVatRateGen()
            select (productCode, description, unitPrice, vatRate));

        return Prop.ForAll(arb, async tuple =>
        {
            var (productCode, description, unitPrice, vatRate) = tuple;

            var (service, productRepoMock, priceHistoryRepoMock) = CreateService();

            var existingProduct = CreateExistingProduct(productCode, "Different description");

            // Setup: ProductCode match returns existing product
            productRepoMock
                .Setup(r => r.GetByProductCodeAndBusinessIdAsync(productCode.Trim(), TestBusinessId))
                .ReturnsAsync(existingProduct);

            productRepoMock
                .Setup(r => r.UpdateAsync(It.IsAny<Product>()))
                .Returns(Task.CompletedTask);

            await service.AutoPopulateFromLineItemAsync(productCode, description, unitPrice, vatRate, "test-user");

            // Assert: ProductCode search was invoked
            productRepoMock.Verify(
                r => r.GetByProductCodeAndBusinessIdAsync(productCode.Trim(), TestBusinessId),
                Times.Once);

            // Assert: Description search was NOT invoked (ProductCode match takes priority)
            productRepoMock.Verify(
                r => r.GetByDescriptionAndBusinessIdAsync(It.IsAny<string>(), It.IsAny<int>()),
                Times.Never);

            // Assert: No new product was created (match was found)
            productRepoMock.Verify(r => r.InsertAsync(It.IsAny<Product>()), Times.Never);
        });
    }

    /// <summary>
    /// Property 15: For any new line item without a ProductCode but with a Description, the system
    /// SHALL search for an existing Product with an exact Description match (case-insensitive)
    /// for the same BusinessId.
    /// **Validates: Requirements 5.1, 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AutoPopulation_MatchesByDescriptionWhenNoProductCode()
    {
        var arb = Arb.From(
            from description in ValidDescriptionGen()
            from unitPrice in ValidPriceGen()
            from vatRate in ValidVatRateGen()
            select (description, unitPrice, vatRate));

        return Prop.ForAll(arb, async tuple =>
        {
            var (description, unitPrice, vatRate) = tuple;

            var (service, productRepoMock, priceHistoryRepoMock) = CreateService();

            var existingProduct = CreateExistingProduct("EXISTING-CODE", description);

            // Setup: No ProductCode provided, so Description match is used
            productRepoMock
                .Setup(r => r.GetByDescriptionAndBusinessIdAsync(description.Trim(), TestBusinessId))
                .ReturnsAsync(existingProduct);

            productRepoMock
                .Setup(r => r.UpdateAsync(It.IsAny<Product>()))
                .Returns(Task.CompletedTask);

            // Call with null/empty ProductCode
            await service.AutoPopulateFromLineItemAsync(null, description, unitPrice, vatRate, "test-user");

            // Assert: ProductCode search was NOT invoked (no ProductCode provided)
            productRepoMock.Verify(
                r => r.GetByProductCodeAndBusinessIdAsync(It.IsAny<string>(), It.IsAny<int>()),
                Times.Never);

            // Assert: Description search WAS invoked
            productRepoMock.Verify(
                r => r.GetByDescriptionAndBusinessIdAsync(description.Trim(), TestBusinessId),
                Times.Once);

            // Assert: No new product was created (match was found)
            productRepoMock.Verify(r => r.InsertAsync(It.IsAny<Product>()), Times.Never);
        });
    }

    #endregion

    #region Property 16: LastUsedDate update on match

    /// <summary>
    /// Property 16: For any line item that matches an existing Product (by ProductCode),
    /// the system SHALL update the Product's LastUsedDate to the current UTC time.
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AutoPopulation_UpdatesLastUsedDateOnProductCodeMatch()
    {
        var arb = Arb.From(
            from productCode in ValidProductCodeGen()
            from description in ValidDescriptionGen()
            from unitPrice in ValidPriceGen()
            from vatRate in ValidVatRateGen()
            select (productCode, description, unitPrice, vatRate));

        return Prop.ForAll(arb, async tuple =>
        {
            var (productCode, description, unitPrice, vatRate) = tuple;

            var (service, productRepoMock, priceHistoryRepoMock) = CreateService();

            var existingProduct = CreateExistingProduct(productCode, "Some description");
            var originalLastUsedDate = existingProduct.LastUsedDate;

            productRepoMock
                .Setup(r => r.GetByProductCodeAndBusinessIdAsync(productCode.Trim(), TestBusinessId))
                .ReturnsAsync(existingProduct);

            Product? updatedProduct = null;
            productRepoMock
                .Setup(r => r.UpdateAsync(It.IsAny<Product>()))
                .Callback<Product>(p => updatedProduct = p)
                .Returns(Task.CompletedTask);

            var beforeUtc = DateTime.UtcNow;
            await service.AutoPopulateFromLineItemAsync(productCode, description, unitPrice, vatRate, "test-user");
            var afterUtc = DateTime.UtcNow;

            // Assert: UpdateAsync was called
            productRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Product>()), Times.Once);

            // Assert: LastUsedDate was updated to current UTC time
            Assert.NotNull(updatedProduct);
            Assert.NotNull(updatedProduct!.LastUsedDate);
            Assert.True(updatedProduct.LastUsedDate >= beforeUtc && updatedProduct.LastUsedDate <= afterUtc,
                $"LastUsedDate {updatedProduct.LastUsedDate} not within [{beforeUtc}, {afterUtc}]");
        });
    }

    /// <summary>
    /// Property 16: For any line item that matches an existing Product (by Description),
    /// the system SHALL update the Product's LastUsedDate to the current UTC time.
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AutoPopulation_UpdatesLastUsedDateOnDescriptionMatch()
    {
        var arb = Arb.From(
            from description in ValidDescriptionGen()
            from unitPrice in ValidPriceGen()
            from vatRate in ValidVatRateGen()
            select (description, unitPrice, vatRate));

        return Prop.ForAll(arb, async tuple =>
        {
            var (description, unitPrice, vatRate) = tuple;

            var (service, productRepoMock, priceHistoryRepoMock) = CreateService();

            var existingProduct = CreateExistingProduct("EXISTING-CODE", description);

            productRepoMock
                .Setup(r => r.GetByDescriptionAndBusinessIdAsync(description.Trim(), TestBusinessId))
                .ReturnsAsync(existingProduct);

            Product? updatedProduct = null;
            productRepoMock
                .Setup(r => r.UpdateAsync(It.IsAny<Product>()))
                .Callback<Product>(p => updatedProduct = p)
                .Returns(Task.CompletedTask);

            var beforeUtc = DateTime.UtcNow;
            await service.AutoPopulateFromLineItemAsync(null, description, unitPrice, vatRate, "test-user");
            var afterUtc = DateTime.UtcNow;

            // Assert: UpdateAsync was called
            productRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Product>()), Times.Once);

            // Assert: LastUsedDate was updated to current UTC time
            Assert.NotNull(updatedProduct);
            Assert.NotNull(updatedProduct!.LastUsedDate);
            Assert.True(updatedProduct.LastUsedDate >= beforeUtc && updatedProduct.LastUsedDate <= afterUtc,
                $"LastUsedDate {updatedProduct.LastUsedDate} not within [{beforeUtc}, {afterUtc}]");
        });
    }

    #endregion

    #region Property 17: Auto-creation when no match exists and ProductCode is present

    /// <summary>
    /// Property 17: For any line item with a ProductCode that does not match any existing Product
    /// for the same BusinessId, the system SHALL create a new Product with: ProductCode from the
    /// line item, Description from the line item, DefaultSellingPrice from UnitPrice,
    /// DefaultCostPrice=0.00, DefaultVatRate from VatRate (or 0.00), IsActive=true,
    /// and LastUsedDate set to current UTC time.
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AutoPopulation_CreatesNewProductWhenNoMatchAndProductCodePresent()
    {
        var arb = Arb.From(
            from productCode in ValidProductCodeGen()
            from description in ValidDescriptionGen()
            from unitPrice in ValidPriceGen()
            from vatRate in ValidVatRateGen()
            select (productCode, description, unitPrice, vatRate));

        return Prop.ForAll(arb, async tuple =>
        {
            var (productCode, description, unitPrice, vatRate) = tuple;

            var (service, productRepoMock, priceHistoryRepoMock) = CreateService();

            // No match by ProductCode
            productRepoMock
                .Setup(r => r.GetByProductCodeAndBusinessIdAsync(productCode.Trim(), TestBusinessId))
                .ReturnsAsync((Product?)null);

            // No match by Description
            productRepoMock
                .Setup(r => r.GetByDescriptionAndBusinessIdAsync(It.IsAny<string>(), TestBusinessId))
                .ReturnsAsync((Product?)null);

            Product? insertedProduct = null;
            productRepoMock
                .Setup(r => r.InsertAsync(It.IsAny<Product>()))
                .Callback<Product>(p => insertedProduct = p)
                .ReturnsAsync(GeneratedProductId);

            ProductPriceHistory? insertedHistory = null;
            priceHistoryRepoMock
                .Setup(r => r.InsertAsync(It.IsAny<ProductPriceHistory>()))
                .Callback<ProductPriceHistory>(h => insertedHistory = h)
                .Returns(Task.CompletedTask);

            var beforeUtc = DateTime.UtcNow;
            await service.AutoPopulateFromLineItemAsync(productCode, description, unitPrice, vatRate, "test-user");
            var afterUtc = DateTime.UtcNow;

            // Assert: A new product was created
            productRepoMock.Verify(r => r.InsertAsync(It.IsAny<Product>()), Times.Once);
            Assert.NotNull(insertedProduct);

            // Assert: ProductCode from line item (trimmed)
            Assert.Equal(productCode.Trim(), insertedProduct!.ProductCode);

            // Assert: Description from line item (trimmed)
            Assert.Equal(description.Trim(), insertedProduct.Description);

            // Assert: DefaultSellingPrice from UnitPrice
            Assert.Equal(unitPrice, insertedProduct.DefaultSellingPrice);

            // Assert: DefaultCostPrice = 0.00
            Assert.Equal(0.00m, insertedProduct.DefaultCostPrice);

            // Assert: DefaultVatRate from VatRate
            Assert.Equal(vatRate, insertedProduct.DefaultVatRate);

            // Assert: IsActive = true
            Assert.True(insertedProduct.IsActive);

            // Assert: LastUsedDate set to current UTC time
            Assert.NotNull(insertedProduct.LastUsedDate);
            Assert.True(insertedProduct.LastUsedDate >= beforeUtc && insertedProduct.LastUsedDate <= afterUtc,
                $"LastUsedDate {insertedProduct.LastUsedDate} not within [{beforeUtc}, {afterUtc}]");

            // Assert: BusinessId is stamped
            Assert.Equal(TestBusinessId, insertedProduct.BusinessId);

            // Assert: Initial price history was created
            priceHistoryRepoMock.Verify(r => r.InsertAsync(It.IsAny<ProductPriceHistory>()), Times.Once);
            Assert.NotNull(insertedHistory);
            Assert.Equal(GeneratedProductId, insertedHistory!.ProductId);
            Assert.Equal(unitPrice, insertedHistory.SellingPrice);
            Assert.Equal(0.00m, insertedHistory.CostPrice);
            Assert.Equal("test-user", insertedHistory.ChangedByUserId);
        });
    }

    #endregion

    #region Property 18: No auto-creation without ProductCode

    /// <summary>
    /// Property 18: For any line item without a ProductCode and without a Description match,
    /// the system SHALL NOT create a new Product record, and the total product count SHALL
    /// remain unchanged.
    /// **Validates: Requirements 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AutoPopulation_DoesNotCreateProductWithoutProductCode()
    {
        var arb = Arb.From(
            from description in ValidDescriptionGen()
            from unitPrice in ValidPriceGen()
            from vatRate in ValidVatRateGen()
            select (description, unitPrice, vatRate));

        return Prop.ForAll(arb, async tuple =>
        {
            var (description, unitPrice, vatRate) = tuple;

            var (service, productRepoMock, priceHistoryRepoMock) = CreateService();

            // No match by Description
            productRepoMock
                .Setup(r => r.GetByDescriptionAndBusinessIdAsync(description.Trim(), TestBusinessId))
                .ReturnsAsync((Product?)null);

            // Call with null ProductCode — no match found
            await service.AutoPopulateFromLineItemAsync(null, description, unitPrice, vatRate, "test-user");

            // Assert: No product was created
            productRepoMock.Verify(r => r.InsertAsync(It.IsAny<Product>()), Times.Never);

            // Assert: No price history was created
            priceHistoryRepoMock.Verify(r => r.InsertAsync(It.IsAny<ProductPriceHistory>()), Times.Never);

            // Assert: No update was called (no match found)
            productRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Product>()), Times.Never);
        });
    }

    /// <summary>
    /// Property 18: For any line item with an empty/whitespace ProductCode and without a Description match,
    /// the system SHALL NOT create a new Product record.
    /// **Validates: Requirements 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AutoPopulation_DoesNotCreateProductWithEmptyProductCode()
    {
        var arb = Arb.From(
            from description in ValidDescriptionGen()
            from unitPrice in ValidPriceGen()
            from vatRate in ValidVatRateGen()
            from emptyCode in Gen.OneOf(
                Gen.Constant(""),
                Gen.Constant("   "),
                Gen.Constant("\t"))
            select (emptyCode, description, unitPrice, vatRate));

        return Prop.ForAll(arb, async tuple =>
        {
            var (emptyCode, description, unitPrice, vatRate) = tuple;

            var (service, productRepoMock, priceHistoryRepoMock) = CreateService();

            // No match by Description
            productRepoMock
                .Setup(r => r.GetByDescriptionAndBusinessIdAsync(description.Trim(), TestBusinessId))
                .ReturnsAsync((Product?)null);

            // Call with empty/whitespace ProductCode — no match found
            await service.AutoPopulateFromLineItemAsync(emptyCode, description, unitPrice, vatRate, "test-user");

            // Assert: No product was created
            productRepoMock.Verify(r => r.InsertAsync(It.IsAny<Product>()), Times.Never);

            // Assert: No price history was created
            priceHistoryRepoMock.Verify(r => r.InsertAsync(It.IsAny<ProductPriceHistory>()), Times.Never);
        });
    }

    #endregion

    #region Property 19: Existing product prices preserved on auto-population match

    /// <summary>
    /// Property 19: For any line item that matches an existing Product where the line item's UnitPrice
    /// differs from the Product's DefaultSellingPrice, the system SHALL NOT update the Product's
    /// DefaultSellingPrice or DefaultCostPrice. Only LastUsedDate SHALL be updated.
    /// **Validates: Requirements 5.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AutoPopulation_PreservesExistingPricesOnMatch()
    {
        var arb = Arb.From(
            from productCode in ValidProductCodeGen()
            from description in ValidDescriptionGen()
            from existingSellingPrice in ValidPriceGen()
            from existingCostPrice in ValidPriceGen()
            from lineItemUnitPrice in ValidPriceGen()
            from vatRate in ValidVatRateGen()
            // Ensure the prices differ to test the preservation logic
            where lineItemUnitPrice != existingSellingPrice
            select (productCode, description, existingSellingPrice, existingCostPrice, lineItemUnitPrice, vatRate));

        return Prop.ForAll(arb, async tuple =>
        {
            var (productCode, description, existingSellingPrice, existingCostPrice, lineItemUnitPrice, vatRate) = tuple;

            var (service, productRepoMock, priceHistoryRepoMock) = CreateService();

            var existingProduct = CreateExistingProduct(productCode, description, existingSellingPrice, existingCostPrice);

            productRepoMock
                .Setup(r => r.GetByProductCodeAndBusinessIdAsync(productCode.Trim(), TestBusinessId))
                .ReturnsAsync(existingProduct);

            Product? updatedProduct = null;
            productRepoMock
                .Setup(r => r.UpdateAsync(It.IsAny<Product>()))
                .Callback<Product>(p => updatedProduct = p)
                .Returns(Task.CompletedTask);

            await service.AutoPopulateFromLineItemAsync(productCode, description, lineItemUnitPrice, vatRate, "test-user");

            // Assert: UpdateAsync was called (to update LastUsedDate)
            productRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Product>()), Times.Once);
            Assert.NotNull(updatedProduct);

            // Assert: DefaultSellingPrice was NOT changed
            Assert.Equal(existingSellingPrice, updatedProduct!.DefaultSellingPrice);

            // Assert: DefaultCostPrice was NOT changed
            Assert.Equal(existingCostPrice, updatedProduct.DefaultCostPrice);

            // Assert: LastUsedDate WAS updated
            Assert.NotNull(updatedProduct.LastUsedDate);

            // Assert: No price history was inserted (prices not changed)
            priceHistoryRepoMock.Verify(r => r.InsertAsync(It.IsAny<ProductPriceHistory>()), Times.Never);

            // Assert: No new product was created
            productRepoMock.Verify(r => r.InsertAsync(It.IsAny<Product>()), Times.Never);
        });
    }

    /// <summary>
    /// Property 19: For any line item that matches an existing Product by Description where the
    /// line item's UnitPrice differs from the Product's DefaultSellingPrice, the system SHALL NOT
    /// update the Product's DefaultSellingPrice or DefaultCostPrice. Only LastUsedDate SHALL be updated.
    /// **Validates: Requirements 5.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AutoPopulation_PreservesExistingPricesOnDescriptionMatch()
    {
        var arb = Arb.From(
            from description in ValidDescriptionGen()
            from existingSellingPrice in ValidPriceGen()
            from existingCostPrice in ValidPriceGen()
            from lineItemUnitPrice in ValidPriceGen()
            from vatRate in ValidVatRateGen()
            where lineItemUnitPrice != existingSellingPrice
            select (description, existingSellingPrice, existingCostPrice, lineItemUnitPrice, vatRate));

        return Prop.ForAll(arb, async tuple =>
        {
            var (description, existingSellingPrice, existingCostPrice, lineItemUnitPrice, vatRate) = tuple;

            var (service, productRepoMock, priceHistoryRepoMock) = CreateService();

            var existingProduct = CreateExistingProduct("EXISTING-CODE", description, existingSellingPrice, existingCostPrice);

            // No ProductCode provided, match by Description
            productRepoMock
                .Setup(r => r.GetByDescriptionAndBusinessIdAsync(description.Trim(), TestBusinessId))
                .ReturnsAsync(existingProduct);

            Product? updatedProduct = null;
            productRepoMock
                .Setup(r => r.UpdateAsync(It.IsAny<Product>()))
                .Callback<Product>(p => updatedProduct = p)
                .Returns(Task.CompletedTask);

            await service.AutoPopulateFromLineItemAsync(null, description, lineItemUnitPrice, vatRate, "test-user");

            // Assert: UpdateAsync was called (to update LastUsedDate)
            productRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Product>()), Times.Once);
            Assert.NotNull(updatedProduct);

            // Assert: DefaultSellingPrice was NOT changed
            Assert.Equal(existingSellingPrice, updatedProduct!.DefaultSellingPrice);

            // Assert: DefaultCostPrice was NOT changed
            Assert.Equal(existingCostPrice, updatedProduct.DefaultCostPrice);

            // Assert: LastUsedDate WAS updated
            Assert.NotNull(updatedProduct.LastUsedDate);

            // Assert: No price history was inserted (prices not changed)
            priceHistoryRepoMock.Verify(r => r.InsertAsync(It.IsAny<ProductPriceHistory>()), Times.Never);
        });
    }

    #endregion
}
