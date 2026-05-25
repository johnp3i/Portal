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

// Feature: product-catalog, Property 1: Product creation persists with correct defaults
// Feature: product-catalog, Property 2: Duplicate ProductCode rejection
// Feature: product-catalog, Property 3: Invalid input rejection
// Feature: product-catalog, Property 5: Product creation includes initial price history

/// <summary>
/// Property-based tests for product creation logic in ProductService.
/// Tests Properties 1, 2, 3, and 5 from the product-catalog design document.
/// Uses FsCheck.Xunit with Moq to mock repositories and ICurrentTenantService.
/// **Validates: Requirements 2.1, 2.3, 2.7, 2.8**
/// </summary>
public class ProductCreationPropertyTests
{
    private const int TestBusinessId = 1;
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
    /// Generates an invalid string: empty, null, or whitespace-only.
    /// </summary>
    private static Gen<string> InvalidStringGen()
    {
        return Gen.OneOf(
            Gen.Constant(""),
            Gen.Constant("   "),
            Gen.Constant("\t"),
            Gen.Constant("  \t  "),
            Gen.Constant("\n"));
    }

    #endregion

    #region Property 1: Product creation persists with correct defaults

    /// <summary>
    /// Property 1: For any valid ProductCode (non-empty, ≤50 chars), Description (non-empty, ≤500 chars),
    /// DefaultSellingPrice (≥0), DefaultCostPrice (≥0), and DefaultVatRate (0–99.99), creating a product
    /// SHALL result in a persisted record with IsActive=true and CreatedAtUtc set to the current UTC time.
    /// **Validates: Requirements 2.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProductCreation_PersistsWithCorrectDefaults()
    {
        var arb = Arb.From(
            from productCode in ValidProductCodeGen()
            from description in ValidDescriptionGen()
            from sellingPrice in ValidPriceGen()
            from costPrice in ValidPriceGen()
            from vatRate in ValidVatRateGen()
            select (productCode, description, sellingPrice, costPrice, vatRate));

        return Prop.ForAll(arb, async tuple =>
        {
            var (productCode, description, sellingPrice, costPrice, vatRate) = tuple;

            var (service, productRepoMock, priceHistoryRepoMock) = CreateService();

            // No existing product with this code
            productRepoMock
                .Setup(r => r.GetByProductCodeAndBusinessIdAsync(It.IsAny<string>(), TestBusinessId))
                .ReturnsAsync((Product?)null);

            Product? insertedProduct = null;
            productRepoMock
                .Setup(r => r.InsertAsync(It.IsAny<Product>()))
                .Callback<Product>(p => insertedProduct = p)
                .ReturnsAsync(GeneratedProductId);

            priceHistoryRepoMock
                .Setup(r => r.InsertAsync(It.IsAny<ProductPriceHistory>()))
                .Returns(Task.CompletedTask);

            var product = new Product
            {
                ProductCode = productCode,
                Description = description,
                DefaultSellingPrice = sellingPrice,
                DefaultCostPrice = costPrice,
                DefaultVatRate = vatRate
            };

            var beforeUtc = DateTime.UtcNow;
            var result = await service.CreateProductAsync(product, "test-user");
            var afterUtc = DateTime.UtcNow;

            // Assert: operation succeeded
            Assert.True(result.Success, $"Expected success but got: {result.Message}");

            // Assert: product was inserted
            Assert.NotNull(insertedProduct);

            // Assert: IsActive is true
            Assert.True(insertedProduct!.IsActive);

            // Assert: BusinessId is stamped
            Assert.Equal(TestBusinessId, insertedProduct.BusinessId);

            // Assert: CreatedAtUtc is set to current UTC time (within tolerance)
            Assert.True(insertedProduct.CreatedAtUtc >= beforeUtc && insertedProduct.CreatedAtUtc <= afterUtc,
                $"CreatedAtUtc {insertedProduct.CreatedAtUtc} not within [{beforeUtc}, {afterUtc}]");

            // Assert: prices are preserved
            Assert.Equal(sellingPrice, insertedProduct.DefaultSellingPrice);
            Assert.Equal(costPrice, insertedProduct.DefaultCostPrice);
            Assert.Equal(vatRate, insertedProduct.DefaultVatRate);

            // Assert: ProductCode and Description are trimmed
            Assert.Equal(productCode.Trim(), insertedProduct.ProductCode);
            Assert.Equal(description.Trim(), insertedProduct.Description);
        });
    }

    #endregion

    #region Property 2: Duplicate ProductCode rejection

    /// <summary>
    /// Property 2: For any existing Product with a given ProductCode and BusinessId, attempting to create
    /// another Product with the same ProductCode (case-insensitive) and BusinessId SHALL return an error,
    /// and the total product count SHALL remain unchanged.
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DuplicateProductCode_IsRejected()
    {
        var arb = Arb.From(
            from productCode in ValidProductCodeGen()
            from description in ValidDescriptionGen()
            from sellingPrice in ValidPriceGen()
            from costPrice in ValidPriceGen()
            from vatRate in ValidVatRateGen()
            select (productCode, description, sellingPrice, costPrice, vatRate));

        return Prop.ForAll(arb, async tuple =>
        {
            var (productCode, description, sellingPrice, costPrice, vatRate) = tuple;

            var (service, productRepoMock, priceHistoryRepoMock) = CreateService();

            // Simulate an existing product with the same ProductCode (case-insensitive)
            var existingProduct = new Product
            {
                Id = 99,
                BusinessId = TestBusinessId,
                ProductCode = productCode,
                Description = "Existing product",
                DefaultSellingPrice = 10.00m,
                DefaultCostPrice = 5.00m,
                DefaultVatRate = 15.00m,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
            };

            productRepoMock
                .Setup(r => r.GetByProductCodeAndBusinessIdAsync(productCode.Trim(), TestBusinessId))
                .ReturnsAsync(existingProduct);

            var product = new Product
            {
                ProductCode = productCode,
                Description = description,
                DefaultSellingPrice = sellingPrice,
                DefaultCostPrice = costPrice,
                DefaultVatRate = vatRate
            };

            var result = await service.CreateProductAsync(product, "test-user");

            // Assert: operation failed with duplicate error
            Assert.False(result.Success);
            Assert.Contains("already exists", result.Message!, StringComparison.OrdinalIgnoreCase);

            // Assert: InsertAsync was never called (product count unchanged)
            productRepoMock.Verify(r => r.InsertAsync(It.IsAny<Product>()), Times.Never);

            // Assert: no price history was inserted
            priceHistoryRepoMock.Verify(r => r.InsertAsync(It.IsAny<ProductPriceHistory>()), Times.Never);
        });
    }

    #endregion

    #region Property 3: Invalid input rejection

    /// <summary>
    /// Property 3: For any ProductCode that is empty or composed entirely of whitespace,
    /// a create request SHALL return a validation error and the product state SHALL remain unchanged.
    /// **Validates: Requirements 2.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvalidProductCode_IsRejected()
    {
        var arb = Arb.From(
            from invalidCode in InvalidStringGen()
            from description in ValidDescriptionGen()
            from sellingPrice in ValidPriceGen()
            from costPrice in ValidPriceGen()
            from vatRate in ValidVatRateGen()
            select (invalidCode, description, sellingPrice, costPrice, vatRate));

        return Prop.ForAll(arb, async tuple =>
        {
            var (invalidCode, description, sellingPrice, costPrice, vatRate) = tuple;

            var (service, productRepoMock, priceHistoryRepoMock) = CreateService();

            var product = new Product
            {
                ProductCode = invalidCode,
                Description = description,
                DefaultSellingPrice = sellingPrice,
                DefaultCostPrice = costPrice,
                DefaultVatRate = vatRate
            };

            var result = await service.CreateProductAsync(product, "test-user");

            // Assert: operation failed with validation error
            Assert.False(result.Success);
            Assert.NotNull(result.Message);

            // Assert: no product was inserted (state unchanged)
            productRepoMock.Verify(r => r.InsertAsync(It.IsAny<Product>()), Times.Never);

            // Assert: no price history was inserted
            priceHistoryRepoMock.Verify(r => r.InsertAsync(It.IsAny<ProductPriceHistory>()), Times.Never);
        });
    }

    /// <summary>
    /// Property 3: For any Description that is empty or composed entirely of whitespace,
    /// a create request SHALL return a validation error and the product state SHALL remain unchanged.
    /// **Validates: Requirements 2.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvalidDescription_IsRejected()
    {
        var arb = Arb.From(
            from productCode in ValidProductCodeGen()
            from invalidDescription in InvalidStringGen()
            from sellingPrice in ValidPriceGen()
            from costPrice in ValidPriceGen()
            from vatRate in ValidVatRateGen()
            select (productCode, invalidDescription, sellingPrice, costPrice, vatRate));

        return Prop.ForAll(arb, async tuple =>
        {
            var (productCode, invalidDescription, sellingPrice, costPrice, vatRate) = tuple;

            var (service, productRepoMock, priceHistoryRepoMock) = CreateService();

            var product = new Product
            {
                ProductCode = productCode,
                Description = invalidDescription,
                DefaultSellingPrice = sellingPrice,
                DefaultCostPrice = costPrice,
                DefaultVatRate = vatRate
            };

            var result = await service.CreateProductAsync(product, "test-user");

            // Assert: operation failed with validation error
            Assert.False(result.Success);
            Assert.NotNull(result.Message);

            // Assert: no product was inserted (state unchanged)
            productRepoMock.Verify(r => r.InsertAsync(It.IsAny<Product>()), Times.Never);

            // Assert: no price history was inserted
            priceHistoryRepoMock.Verify(r => r.InsertAsync(It.IsAny<ProductPriceHistory>()), Times.Never);
        });
    }

    #endregion

    #region Property 5: Product creation includes initial price history

    /// <summary>
    /// Property 5: For any newly created Product, the system SHALL insert an initial ProductPriceHistory
    /// record with SellingPrice matching DefaultSellingPrice, CostPrice matching DefaultCostPrice,
    /// and EffectiveFromUtc set to the current UTC time.
    /// **Validates: Requirements 2.8**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProductCreation_IncludesInitialPriceHistory()
    {
        var arb = Arb.From(
            from productCode in ValidProductCodeGen()
            from description in ValidDescriptionGen()
            from sellingPrice in ValidPriceGen()
            from costPrice in ValidPriceGen()
            from vatRate in ValidVatRateGen()
            select (productCode, description, sellingPrice, costPrice, vatRate));

        return Prop.ForAll(arb, async tuple =>
        {
            var (productCode, description, sellingPrice, costPrice, vatRate) = tuple;

            var (service, productRepoMock, priceHistoryRepoMock) = CreateService();

            // No existing product with this code
            productRepoMock
                .Setup(r => r.GetByProductCodeAndBusinessIdAsync(It.IsAny<string>(), TestBusinessId))
                .ReturnsAsync((Product?)null);

            productRepoMock
                .Setup(r => r.InsertAsync(It.IsAny<Product>()))
                .ReturnsAsync(GeneratedProductId);

            ProductPriceHistory? insertedHistory = null;
            priceHistoryRepoMock
                .Setup(r => r.InsertAsync(It.IsAny<ProductPriceHistory>()))
                .Callback<ProductPriceHistory>(h => insertedHistory = h)
                .Returns(Task.CompletedTask);

            var product = new Product
            {
                ProductCode = productCode,
                Description = description,
                DefaultSellingPrice = sellingPrice,
                DefaultCostPrice = costPrice,
                DefaultVatRate = vatRate
            };

            var beforeUtc = DateTime.UtcNow;
            var result = await service.CreateProductAsync(product, "test-user");
            var afterUtc = DateTime.UtcNow;

            // Assert: operation succeeded
            Assert.True(result.Success, $"Expected success but got: {result.Message}");

            // Assert: price history was inserted exactly once
            priceHistoryRepoMock.Verify(r => r.InsertAsync(It.IsAny<ProductPriceHistory>()), Times.Once);

            // Assert: price history record exists
            Assert.NotNull(insertedHistory);

            // Assert: ProductId matches the generated ID
            Assert.Equal(GeneratedProductId, insertedHistory!.ProductId);

            // Assert: SellingPrice matches DefaultSellingPrice
            Assert.Equal(sellingPrice, insertedHistory.SellingPrice);

            // Assert: CostPrice matches DefaultCostPrice
            Assert.Equal(costPrice, insertedHistory.CostPrice);

            // Assert: EffectiveFromUtc is set to current UTC time (within tolerance)
            Assert.True(insertedHistory.EffectiveFromUtc >= beforeUtc && insertedHistory.EffectiveFromUtc <= afterUtc,
                $"EffectiveFromUtc {insertedHistory.EffectiveFromUtc} not within [{beforeUtc}, {afterUtc}]");

            // Assert: ChangedByUserId is set
            Assert.Equal("test-user", insertedHistory.ChangedByUserId);
        });
    }

    #endregion
}
