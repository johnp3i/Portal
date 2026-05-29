using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: invoice-line-product-type-reverse-charge, Property 5: ProductTypeId accepts only valid values

/// <summary>
/// Property-based tests for ProductTypeId validation in ProductService.
/// For any ProductTypeId value that is not NULL, 1, or 2, the system SHALL reject the value
/// via service-layer validation. Valid values (1, 2) are accepted for creation and updates.
/// NULL is rejected for new products (per requirement 2.2) but accepted for updates (legacy products).
/// **Validates: Requirements 8.3**
/// </summary>
public class ProductTypeIdValidValuesPropertyTests
{
    private const int AuthenticatedBusinessId = 42;

    #region Test Infrastructure

    private static (ProductService Service, Mock<ProductRepository> ProductRepo, Mock<ProductPriceHistoryRepository> PriceHistoryRepo)
        CreateService()
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(AuthenticatedBusinessId);

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

    private static Product CreateValidProduct(int seed, int? productTypeId)
    {
        return new Product
        {
            ProductCode = $"PROD-{Math.Abs(seed) % 99999:D5}",
            Description = $"Test Product {Math.Abs(seed) % 10000}",
            DefaultSellingPrice = (Math.Abs(seed) % 10000) / 100m,
            DefaultCostPrice = (Math.Abs(seed) % 5000) / 100m,
            DefaultVatRate = 15.00m,
            ProductTypeId = productTypeId
        };
    }

    #endregion

    #region Property 5a: Invalid ProductTypeId values are rejected on creation

    /// <summary>
    /// Property 5a: For any integer ProductTypeId that is NOT 1 or 2, CreateProductAsync SHALL
    /// throw an ArgumentException rejecting the invalid value.
    /// **Validates: Requirements 8.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvalidProductTypeId_RejectedOnCreate(int rawValue, PositiveInt codeSeed)
    {
        // Filter to only values outside {1, 2}
        if (rawValue == 1 || rawValue == 2)
            return true.ToProperty().Label("Skipped — valid value");

        var (service, productRepo, priceHistoryRepo) = CreateService();

        var product = CreateValidProduct(codeSeed.Get, rawValue);

        // Mock: no duplicate exists
        productRepo.Setup(r => r.GetByProductCodeAndBusinessIdAsync(
                It.IsAny<string>(), AuthenticatedBusinessId))
            .ReturnsAsync((Product?)null);

        var threw = false;
        var correctMessage = false;

        try
        {
            service.CreateProductAsync(product, "user-1").GetAwaiter().GetResult();
        }
        catch (ArgumentException ex)
        {
            threw = true;
            correctMessage = ex.Message.Contains("Product Type must be Services (1) or Goods (2)");
        }

        // Verify no persistence occurred
        productRepo.Verify(r => r.InsertAsync(It.IsAny<Product>()), Times.Never);

        return (threw && correctMessage).ToProperty()
            .Label($"ProductTypeId={rawValue}, Threw={threw}, CorrectMessage={correctMessage}");
    }

    #endregion

    #region Property 5b: Invalid ProductTypeId values are rejected on update

    /// <summary>
    /// Property 5b: For any integer ProductTypeId that is NOT 1 or 2, UpdateProductAsync SHALL
    /// throw an ArgumentException rejecting the invalid value.
    /// **Validates: Requirements 8.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvalidProductTypeId_RejectedOnUpdate(int rawValue, PositiveInt codeSeed)
    {
        // Filter to only values outside {1, 2}
        if (rawValue == 1 || rawValue == 2)
            return true.ToProperty().Label("Skipped — valid value");

        var (service, productRepo, _) = CreateService();

        var product = CreateValidProduct(codeSeed.Get, rawValue);
        product.Id = Math.Abs(codeSeed.Get) % 1000 + 1;

        var threw = false;
        var correctMessage = false;

        try
        {
            service.UpdateProductAsync(product, "user-1").GetAwaiter().GetResult();
        }
        catch (ArgumentException ex)
        {
            threw = true;
            correctMessage = ex.Message.Contains("Product Type must be Services (1) or Goods (2)");
        }

        // Verify no persistence occurred
        productRepo.Verify(r => r.UpdateAsync(It.IsAny<Product>()), Times.Never);

        return (threw && correctMessage).ToProperty()
            .Label($"ProductTypeId={rawValue}, Threw={threw}, CorrectMessage={correctMessage}");
    }

    #endregion

    #region Property 5c: Valid ProductTypeId values (1, 2) are accepted on creation

    /// <summary>
    /// Property 5c: For ProductTypeId values of 1 or 2, CreateProductAsync SHALL accept the value
    /// and successfully create the product (assuming all other fields are valid).
    /// **Validates: Requirements 8.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidProductTypeId_AcceptedOnCreate(PositiveInt codeSeed, bool useServices)
    {
        var productTypeId = useServices ? 1 : 2;

        var (service, productRepo, priceHistoryRepo) = CreateService();

        var product = CreateValidProduct(codeSeed.Get, productTypeId);

        // Mock: no duplicate exists
        productRepo.Setup(r => r.GetByProductCodeAndBusinessIdAsync(
                It.IsAny<string>(), AuthenticatedBusinessId))
            .ReturnsAsync((Product?)null);

        // Mock: insert returns new ID
        productRepo.Setup(r => r.InsertAsync(It.IsAny<Product>()))
            .ReturnsAsync(1);

        priceHistoryRepo.Setup(r => r.InsertAsync(It.IsAny<ProductPriceHistory>()))
            .Returns(Task.CompletedTask);

        var result = service.CreateProductAsync(product, "user-1").GetAwaiter().GetResult();

        // Property: creation succeeds with valid ProductTypeId
        var succeeded = result.Success;

        return succeeded.ToProperty()
            .Label($"ProductTypeId={productTypeId}, Success={result.Success}");
    }

    #endregion

    #region Property 5d: NULL ProductTypeId is rejected for new products

    /// <summary>
    /// Property 5d: For new product creation, NULL ProductTypeId SHALL be rejected with
    /// an ArgumentException (per requirement 2.2 — new products require a type).
    /// **Validates: Requirements 8.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NullProductTypeId_RejectedOnCreate(PositiveInt codeSeed)
    {
        var (service, productRepo, _) = CreateService();

        var product = CreateValidProduct(codeSeed.Get, null);

        // Mock: no duplicate exists
        productRepo.Setup(r => r.GetByProductCodeAndBusinessIdAsync(
                It.IsAny<string>(), AuthenticatedBusinessId))
            .ReturnsAsync((Product?)null);

        var threw = false;
        var correctMessage = false;

        try
        {
            service.CreateProductAsync(product, "user-1").GetAwaiter().GetResult();
        }
        catch (ArgumentException ex)
        {
            threw = true;
            correctMessage = ex.Message.Contains("Product Type is required for new products");
        }

        // Verify no persistence occurred
        productRepo.Verify(r => r.InsertAsync(It.IsAny<Product>()), Times.Never);

        return (threw && correctMessage).ToProperty()
            .Label($"ProductTypeId=null, Threw={threw}, CorrectMessage={correctMessage}");
    }

    #endregion

    #region Property 5e: NULL ProductTypeId is accepted for updates (legacy products)

    /// <summary>
    /// Property 5e: For product updates, NULL ProductTypeId SHALL be accepted (legacy products
    /// created before this feature may not have a type assigned).
    /// **Validates: Requirements 8.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NullProductTypeId_AcceptedOnUpdate(PositiveInt codeSeed, PositiveInt productIdSeed)
    {
        var (service, productRepo, priceHistoryRepo) = CreateService();

        var productId = (Math.Abs(productIdSeed.Get) % 1000) + 1;
        var product = CreateValidProduct(codeSeed.Get, null);
        product.Id = productId;

        // Mock: product exists and belongs to this business
        var existingProduct = new Product
        {
            Id = productId,
            BusinessId = AuthenticatedBusinessId,
            ProductCode = product.ProductCode,
            Description = product.Description,
            DefaultSellingPrice = product.DefaultSellingPrice,
            DefaultCostPrice = product.DefaultCostPrice,
            DefaultVatRate = product.DefaultVatRate,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            ProductTypeId = null
        };

        productRepo.Setup(r => r.GetByIdAndBusinessIdAsync(productId, AuthenticatedBusinessId))
            .ReturnsAsync(existingProduct);

        productRepo.Setup(r => r.UpdateAsync(It.IsAny<Product>()))
            .Returns(Task.CompletedTask);

        var result = service.UpdateProductAsync(product, "user-1").GetAwaiter().GetResult();

        // Property: update succeeds with NULL ProductTypeId (legacy product)
        var succeeded = result.Success;

        return succeeded.ToProperty()
            .Label($"ProductId={productId}, ProductTypeId=null, Success={result.Success}");
    }

    #endregion

    #region Property 5f: Valid ProductTypeId values (1, 2) are accepted on update

    /// <summary>
    /// Property 5f: For product updates, ProductTypeId values of 1 or 2 SHALL be accepted.
    /// **Validates: Requirements 8.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidProductTypeId_AcceptedOnUpdate(PositiveInt codeSeed, PositiveInt productIdSeed, bool useServices)
    {
        var productTypeId = useServices ? 1 : 2;

        var (service, productRepo, priceHistoryRepo) = CreateService();

        var productId = (Math.Abs(productIdSeed.Get) % 1000) + 1;
        var product = CreateValidProduct(codeSeed.Get, productTypeId);
        product.Id = productId;

        // Mock: product exists and belongs to this business
        var existingProduct = new Product
        {
            Id = productId,
            BusinessId = AuthenticatedBusinessId,
            ProductCode = product.ProductCode,
            Description = product.Description,
            DefaultSellingPrice = product.DefaultSellingPrice,
            DefaultCostPrice = product.DefaultCostPrice,
            DefaultVatRate = product.DefaultVatRate,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            ProductTypeId = 1
        };

        productRepo.Setup(r => r.GetByIdAndBusinessIdAsync(productId, AuthenticatedBusinessId))
            .ReturnsAsync(existingProduct);

        productRepo.Setup(r => r.UpdateAsync(It.IsAny<Product>()))
            .Returns(Task.CompletedTask);

        var result = service.UpdateProductAsync(product, "user-1").GetAwaiter().GetResult();

        // Property: update succeeds with valid ProductTypeId
        var succeeded = result.Success;

        return succeeded.ToProperty()
            .Label($"ProductId={productId}, ProductTypeId={productTypeId}, Success={result.Success}");
    }

    #endregion
}
