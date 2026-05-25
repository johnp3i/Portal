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

// Feature: product-catalog, Property 4: Price update creates history record
// Feature: product-catalog, Property 6: Deactivation sets IsActive to false

/// <summary>
/// Property-based tests for product update and deactivation logic in ProductService.
/// Tests Properties 4 and 6 from the product-catalog design document.
/// Uses FsCheck.Xunit with Moq to mock repositories and ICurrentTenantService.
/// **Validates: Requirements 1.6, 2.5, 2.6, 5.8**
/// </summary>
public class ProductUpdatePropertyTests
{
    private const int TestBusinessId = 1;

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
    /// Generates a product ID (positive integer).
    /// </summary>
    private static Gen<int> ProductIdGen()
    {
        return Gen.Choose(1, 10000);
    }

    #endregion

    #region Property 4: Price update creates history record

    /// <summary>
    /// Property 4: For any product update where DefaultSellingPrice or DefaultCostPrice changes,
    /// the system SHALL insert a new ProductPriceHistory record with the new SellingPrice, new CostPrice,
    /// EffectiveFromUtc equal to the current UTC time, and ChangedByUserId equal to the authenticated user's identifier.
    /// **Validates: Requirements 1.6, 2.5, 5.8**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PriceUpdate_CreatesHistoryRecord_WhenSellingPriceChanges()
    {
        var arb = Arb.From(
            from productId in ProductIdGen()
            from productCode in ValidProductCodeGen()
            from description in ValidDescriptionGen()
            from originalSellingPrice in ValidPriceGen()
            from newSellingPrice in ValidPriceGen()
            from costPrice in ValidPriceGen()
            from vatRate in ValidVatRateGen()
            where originalSellingPrice != newSellingPrice
            select (productId, productCode, description, originalSellingPrice, newSellingPrice, costPrice, vatRate));

        return Prop.ForAll(arb, async tuple =>
        {
            var (productId, productCode, description, originalSellingPrice, newSellingPrice, costPrice, vatRate) = tuple;

            var (service, productRepoMock, priceHistoryRepoMock) = CreateService();

            // Setup existing product with original selling price
            var existingProduct = new Product
            {
                Id = productId,
                BusinessId = TestBusinessId,
                ProductCode = productCode,
                Description = description,
                DefaultSellingPrice = originalSellingPrice,
                DefaultCostPrice = costPrice,
                DefaultVatRate = vatRate,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-10)
            };

            productRepoMock
                .Setup(r => r.GetByIdAndBusinessIdAsync(productId, TestBusinessId))
                .ReturnsAsync(existingProduct);

            productRepoMock
                .Setup(r => r.UpdateAsync(It.IsAny<Product>()))
                .Returns(Task.CompletedTask);

            ProductPriceHistory? insertedHistory = null;
            priceHistoryRepoMock
                .Setup(r => r.InsertAsync(It.IsAny<ProductPriceHistory>()))
                .Callback<ProductPriceHistory>(h => insertedHistory = h)
                .Returns(Task.CompletedTask);

            // Update product with new selling price
            var updatedProduct = new Product
            {
                Id = productId,
                ProductCode = productCode,
                Description = description,
                DefaultSellingPrice = newSellingPrice,
                DefaultCostPrice = costPrice,
                DefaultVatRate = vatRate
            };

            var beforeUtc = DateTime.UtcNow;
            var result = await service.UpdateProductAsync(updatedProduct, "update-user");
            var afterUtc = DateTime.UtcNow;

            // Assert: operation succeeded
            Assert.True(result.Success, $"Expected success but got: {result.Message}");

            // Assert: price history was inserted exactly once
            priceHistoryRepoMock.Verify(r => r.InsertAsync(It.IsAny<ProductPriceHistory>()), Times.Once);

            // Assert: price history record exists
            Assert.NotNull(insertedHistory);

            // Assert: ProductId matches
            Assert.Equal(productId, insertedHistory!.ProductId);

            // Assert: SellingPrice matches the NEW selling price
            Assert.Equal(newSellingPrice, insertedHistory.SellingPrice);

            // Assert: CostPrice matches the current cost price
            Assert.Equal(costPrice, insertedHistory.CostPrice);

            // Assert: EffectiveFromUtc is set to current UTC time (within tolerance)
            Assert.True(insertedHistory.EffectiveFromUtc >= beforeUtc && insertedHistory.EffectiveFromUtc <= afterUtc,
                $"EffectiveFromUtc {insertedHistory.EffectiveFromUtc} not within [{beforeUtc}, {afterUtc}]");

            // Assert: ChangedByUserId equals the authenticated user's identifier
            Assert.Equal("update-user", insertedHistory.ChangedByUserId);
        });
    }

    /// <summary>
    /// Property 4: For any product update where DefaultCostPrice changes (selling price unchanged),
    /// the system SHALL insert a new ProductPriceHistory record with the new CostPrice.
    /// **Validates: Requirements 1.6, 2.5, 5.8**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PriceUpdate_CreatesHistoryRecord_WhenCostPriceChanges()
    {
        var arb = Arb.From(
            from productId in ProductIdGen()
            from productCode in ValidProductCodeGen()
            from description in ValidDescriptionGen()
            from sellingPrice in ValidPriceGen()
            from originalCostPrice in ValidPriceGen()
            from newCostPrice in ValidPriceGen()
            from vatRate in ValidVatRateGen()
            where originalCostPrice != newCostPrice
            select (productId, productCode, description, sellingPrice, originalCostPrice, newCostPrice, vatRate));

        return Prop.ForAll(arb, async tuple =>
        {
            var (productId, productCode, description, sellingPrice, originalCostPrice, newCostPrice, vatRate) = tuple;

            var (service, productRepoMock, priceHistoryRepoMock) = CreateService();

            // Setup existing product with original cost price
            var existingProduct = new Product
            {
                Id = productId,
                BusinessId = TestBusinessId,
                ProductCode = productCode,
                Description = description,
                DefaultSellingPrice = sellingPrice,
                DefaultCostPrice = originalCostPrice,
                DefaultVatRate = vatRate,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-10)
            };

            productRepoMock
                .Setup(r => r.GetByIdAndBusinessIdAsync(productId, TestBusinessId))
                .ReturnsAsync(existingProduct);

            productRepoMock
                .Setup(r => r.UpdateAsync(It.IsAny<Product>()))
                .Returns(Task.CompletedTask);

            ProductPriceHistory? insertedHistory = null;
            priceHistoryRepoMock
                .Setup(r => r.InsertAsync(It.IsAny<ProductPriceHistory>()))
                .Callback<ProductPriceHistory>(h => insertedHistory = h)
                .Returns(Task.CompletedTask);

            // Update product with new cost price (selling price unchanged)
            var updatedProduct = new Product
            {
                Id = productId,
                ProductCode = productCode,
                Description = description,
                DefaultSellingPrice = sellingPrice,
                DefaultCostPrice = newCostPrice,
                DefaultVatRate = vatRate
            };

            var beforeUtc = DateTime.UtcNow;
            var result = await service.UpdateProductAsync(updatedProduct, "cost-update-user");
            var afterUtc = DateTime.UtcNow;

            // Assert: operation succeeded
            Assert.True(result.Success, $"Expected success but got: {result.Message}");

            // Assert: price history was inserted exactly once
            priceHistoryRepoMock.Verify(r => r.InsertAsync(It.IsAny<ProductPriceHistory>()), Times.Once);

            // Assert: price history record exists
            Assert.NotNull(insertedHistory);

            // Assert: ProductId matches
            Assert.Equal(productId, insertedHistory!.ProductId);

            // Assert: SellingPrice matches the current selling price
            Assert.Equal(sellingPrice, insertedHistory.SellingPrice);

            // Assert: CostPrice matches the NEW cost price
            Assert.Equal(newCostPrice, insertedHistory.CostPrice);

            // Assert: EffectiveFromUtc is set to current UTC time (within tolerance)
            Assert.True(insertedHistory.EffectiveFromUtc >= beforeUtc && insertedHistory.EffectiveFromUtc <= afterUtc,
                $"EffectiveFromUtc {insertedHistory.EffectiveFromUtc} not within [{beforeUtc}, {afterUtc}]");

            // Assert: ChangedByUserId equals the authenticated user's identifier
            Assert.Equal("cost-update-user", insertedHistory.ChangedByUserId);
        });
    }

    /// <summary>
    /// Property 4 (negative case): For any product update where neither DefaultSellingPrice nor
    /// DefaultCostPrice changes, the system SHALL NOT insert a ProductPriceHistory record.
    /// **Validates: Requirements 1.6, 2.5, 5.8**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PriceUpdate_DoesNotCreateHistoryRecord_WhenPricesUnchanged()
    {
        var arb = Arb.From(
            from productId in ProductIdGen()
            from productCode in ValidProductCodeGen()
            from description in ValidDescriptionGen()
            from newDescription in ValidDescriptionGen()
            from sellingPrice in ValidPriceGen()
            from costPrice in ValidPriceGen()
            from vatRate in ValidVatRateGen()
            select (productId, productCode, description, newDescription, sellingPrice, costPrice, vatRate));

        return Prop.ForAll(arb, async tuple =>
        {
            var (productId, productCode, description, newDescription, sellingPrice, costPrice, vatRate) = tuple;

            var (service, productRepoMock, priceHistoryRepoMock) = CreateService();

            // Setup existing product
            var existingProduct = new Product
            {
                Id = productId,
                BusinessId = TestBusinessId,
                ProductCode = productCode,
                Description = description,
                DefaultSellingPrice = sellingPrice,
                DefaultCostPrice = costPrice,
                DefaultVatRate = vatRate,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-10)
            };

            productRepoMock
                .Setup(r => r.GetByIdAndBusinessIdAsync(productId, TestBusinessId))
                .ReturnsAsync(existingProduct);

            productRepoMock
                .Setup(r => r.UpdateAsync(It.IsAny<Product>()))
                .Returns(Task.CompletedTask);

            // Update product with same prices but different description
            var updatedProduct = new Product
            {
                Id = productId,
                ProductCode = productCode,
                Description = newDescription,
                DefaultSellingPrice = sellingPrice,
                DefaultCostPrice = costPrice,
                DefaultVatRate = vatRate
            };

            var result = await service.UpdateProductAsync(updatedProduct, "no-price-change-user");

            // Assert: operation succeeded
            Assert.True(result.Success, $"Expected success but got: {result.Message}");

            // Assert: NO price history was inserted (prices unchanged)
            priceHistoryRepoMock.Verify(r => r.InsertAsync(It.IsAny<ProductPriceHistory>()), Times.Never);
        });
    }

    #endregion

    #region Property 6: Deactivation sets IsActive to false

    /// <summary>
    /// Property 6: For any active Product, submitting a deactivate request SHALL result in
    /// IsActive being set to false, with all other product fields remaining unchanged.
    /// **Validates: Requirements 2.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Deactivation_SetsIsActiveToFalse_ForActiveProduct()
    {
        var arb = Arb.From(
            from productId in ProductIdGen()
            from productCode in ValidProductCodeGen()
            from description in ValidDescriptionGen()
            from sellingPrice in ValidPriceGen()
            from costPrice in ValidPriceGen()
            from vatRate in ValidVatRateGen()
            select (productId, productCode, description, sellingPrice, costPrice, vatRate));

        return Prop.ForAll(arb, async tuple =>
        {
            var (productId, productCode, description, sellingPrice, costPrice, vatRate) = tuple;

            var (service, productRepoMock, priceHistoryRepoMock) = CreateService();

            // Setup existing active product
            var existingProduct = new Product
            {
                Id = productId,
                BusinessId = TestBusinessId,
                ProductCode = productCode,
                Description = description,
                DefaultSellingPrice = sellingPrice,
                DefaultCostPrice = costPrice,
                DefaultVatRate = vatRate,
                IsActive = true,
                LastUsedDate = DateTime.UtcNow.AddDays(-5),
                CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
            };

            productRepoMock
                .Setup(r => r.GetByIdAndBusinessIdAsync(productId, TestBusinessId))
                .ReturnsAsync(existingProduct);

            productRepoMock
                .Setup(r => r.DeactivateAsync(productId, TestBusinessId))
                .Returns(Task.CompletedTask);

            var result = await service.DeactivateProductAsync(productId);

            // Assert: operation succeeded
            Assert.True(result.Success, $"Expected success but got: {result.Message}");

            // Assert: DeactivateAsync was called with correct parameters
            productRepoMock.Verify(r => r.DeactivateAsync(productId, TestBusinessId), Times.Once);

            // Assert: no price history was inserted (deactivation does not affect prices)
            priceHistoryRepoMock.Verify(r => r.InsertAsync(It.IsAny<ProductPriceHistory>()), Times.Never);

            // Assert: UpdateAsync was NOT called (deactivation uses dedicated method)
            productRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Product>()), Times.Never);
        });
    }

    /// <summary>
    /// Property 6 (supplementary): Deactivation of a non-existent product returns failure.
    /// The system treats products not belonging to the business as non-existent.
    /// **Validates: Requirements 2.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Deactivation_ReturnsFailure_WhenProductNotFound()
    {
        var arb = Arb.From(ProductIdGen());

        return Prop.ForAll(arb, async productId =>
        {
            var (service, productRepoMock, priceHistoryRepoMock) = CreateService();

            // Product does not exist for this business
            productRepoMock
                .Setup(r => r.GetByIdAndBusinessIdAsync(productId, TestBusinessId))
                .ReturnsAsync((Product?)null);

            var result = await service.DeactivateProductAsync(productId);

            // Assert: operation failed
            Assert.False(result.Success);
            Assert.Contains("not found", result.Message!, StringComparison.OrdinalIgnoreCase);

            // Assert: DeactivateAsync was never called
            productRepoMock.Verify(r => r.DeactivateAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        });
    }

    #endregion
}
