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

// Feature: product-catalog, Property 20: Tenant isolation

/// <summary>
/// Property-based tests for tenant isolation in the Product Catalog module.
/// Validates that for any data access operation (query, create, update, deactivate),
/// the system SHALL only return or modify Products belonging to the authenticated user's BusinessId.
/// Products belonging to other BusinessIds SHALL be treated as non-existent.
/// New Products SHALL always be stamped with the authenticated BusinessId.
/// **Validates: Requirements 7.1, 7.2, 7.3, 7.4, 7.6, 7.7**
/// </summary>
public class TenantIsolationPropertyTests
{
    private const int AuthenticatedBusinessId = 42;
    private const int OtherBusinessId = 99;

    #region Test Infrastructure

    private static (ProductService Service, Mock<ProductRepository> ProductRepo, Mock<ProductPriceHistoryRepository> PriceHistoryRepo)
        CreateServiceWithBusinessId(int businessId)
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
    /// Generates a valid product code from a seed (non-empty, max 50 chars).
    /// </summary>
    private static string GenerateProductCode(int seed)
    {
        var code = $"PROD-{Math.Abs(seed) % 99999:D5}";
        return code;
    }

    /// <summary>
    /// Generates a valid description from a seed (non-empty, max 500 chars).
    /// </summary>
    private static string GenerateDescription(int seed)
    {
        return $"Product Description {Math.Abs(seed) % 10000}";
    }

    /// <summary>
    /// Generates a valid price from a seed (>= 0).
    /// </summary>
    private static decimal GeneratePrice(int seed)
    {
        return (Math.Abs(seed) % 100000) / 100m;
    }

    /// <summary>
    /// Generates a valid VAT rate from a seed (0.00 to 99.99).
    /// </summary>
    private static decimal GenerateVatRate(int seed)
    {
        return (Math.Abs(seed) % 10000) / 100m;
    }

    #endregion

    #region Property 20a: When BusinessId is 0 (unresolved), all operations return empty/fail

    /// <summary>
    /// Property 20a: When the authenticated user's BusinessId cannot be resolved (returns 0),
    /// all product operations SHALL return zero results or fail gracefully.
    /// **Validates: Requirements 7.1, 7.2, 7.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UnresolvedBusinessId_AllOperationsReturnEmptyOrFail(
        PositiveInt productIdSeed,
        PositiveInt codeSeed,
        PositiveInt descSeed,
        PositiveInt priceSeed)
    {
        var (service, productRepo, _) = CreateServiceWithBusinessId(0);

        // GetProductByIdAsync should return null
        var getResult = service.GetProductByIdAsync(productIdSeed.Get).Result;
        var getReturnsNull = getResult == null;

        // GetProductsPagedAsync should return empty
        var pagedResult = service.GetProductsPagedAsync("anything", 1).Result;
        var pagedReturnsEmpty = pagedResult.Items.Count == 0 && pagedResult.TotalCount == 0;

        // GetKpisAsync should return zeroed KPIs
        var kpiResult = service.GetKpisAsync().Result;
        var kpiReturnsZero = kpiResult.TotalProducts == 0
                          && kpiResult.ActiveProducts == 0
                          && kpiResult.AverageSellingPrice == 0;

        // GetTopProductsByUsageAsync should return empty
        var topResult = service.GetTopProductsByUsageAsync().Result;
        var topReturnsEmpty = topResult.Count == 0;

        // CreateProductAsync should fail
        var createProduct = new Product
        {
            ProductCode = GenerateProductCode(codeSeed.Get),
            Description = GenerateDescription(descSeed.Get),
            DefaultSellingPrice = GeneratePrice(priceSeed.Get),
            DefaultCostPrice = GeneratePrice(priceSeed.Get + 1),
            DefaultVatRate = 15.00m
        };
        var createResult = service.CreateProductAsync(createProduct, "user-1").Result;
        var createFails = !createResult.Success;

        // UpdateProductAsync should fail
        var updateProduct = new Product
        {
            Id = productIdSeed.Get,
            ProductCode = GenerateProductCode(codeSeed.Get),
            Description = GenerateDescription(descSeed.Get),
            DefaultSellingPrice = GeneratePrice(priceSeed.Get),
            DefaultCostPrice = GeneratePrice(priceSeed.Get + 1),
            DefaultVatRate = 15.00m
        };
        var updateResult = service.UpdateProductAsync(updateProduct, "user-1").Result;
        var updateFails = !updateResult.Success;

        // DeactivateProductAsync should fail
        var deactivateResult = service.DeactivateProductAsync(productIdSeed.Get).Result;
        var deactivateFails = !deactivateResult.Success;

        // GetPriceHistoryAsync should return empty
        var historyResult = service.GetPriceHistoryAsync(productIdSeed.Get).Result;
        var historyReturnsEmpty = historyResult.Count == 0;

        var allPropertiesHold = getReturnsNull
                             && pagedReturnsEmpty
                             && kpiReturnsZero
                             && topReturnsEmpty
                             && createFails
                             && updateFails
                             && deactivateFails
                             && historyReturnsEmpty;

        return allPropertiesHold.ToProperty()
            .Label($"Get={getReturnsNull}, Paged={pagedReturnsEmpty}, KPI={kpiReturnsZero}, " +
                   $"Top={topReturnsEmpty}, Create={createFails}, Update={updateFails}, " +
                   $"Deactivate={deactivateFails}, History={historyReturnsEmpty}");
    }

    #endregion

    #region Property 20b: Create always stamps the authenticated BusinessId

    /// <summary>
    /// Property 20b: When a new Product is created, the system SHALL always stamp the record
    /// with the authenticated user's BusinessId, regardless of what BusinessId is provided in the input.
    /// **Validates: Requirements 7.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CreateProduct_AlwaysStampsAuthenticatedBusinessId(
        PositiveInt codeSeed,
        PositiveInt descSeed,
        PositiveInt priceSeed,
        PositiveInt inputBusinessIdSeed)
    {
        var (service, productRepo, priceHistoryRepo) = CreateServiceWithBusinessId(AuthenticatedBusinessId);

        var productCode = GenerateProductCode(codeSeed.Get);
        var inputBusinessId = (Math.Abs(inputBusinessIdSeed.Get) % 1000) + 1; // Some arbitrary business ID

        Product? capturedProduct = null;

        // Mock: no duplicate exists
        productRepo.Setup(r => r.GetByProductCodeAndBusinessIdAsync(
                It.IsAny<string>(), AuthenticatedBusinessId))
            .ReturnsAsync((Product?)null);

        // Capture the product that gets inserted
        productRepo.Setup(r => r.InsertAsync(It.IsAny<Product>()))
            .Callback<Product>(p => capturedProduct = p)
            .ReturnsAsync(1);

        priceHistoryRepo.Setup(r => r.InsertAsync(It.IsAny<ProductPriceHistory>()))
            .Returns(Task.CompletedTask);

        var product = new Product
        {
            BusinessId = inputBusinessId, // Intentionally set to a different business
            ProductCode = productCode,
            Description = GenerateDescription(descSeed.Get),
            DefaultSellingPrice = GeneratePrice(priceSeed.Get),
            DefaultCostPrice = GeneratePrice(priceSeed.Get + 1),
            DefaultVatRate = GenerateVatRate(priceSeed.Get + 2),
            ProductTypeId = 1 // Required for new products
        };

        var result = service.CreateProductAsync(product, "user-1").Result;

        // Property: the product was stamped with the authenticated BusinessId
        var stampedCorrectly = capturedProduct != null
                            && capturedProduct.BusinessId == AuthenticatedBusinessId;

        // Property: the operation succeeded
        var operationSucceeded = result.Success;

        var allPropertiesHold = stampedCorrectly && operationSucceeded;

        return allPropertiesHold.ToProperty()
            .Label($"InputBusinessId={inputBusinessId}, StampedBusinessId={capturedProduct?.BusinessId}, " +
                   $"AuthenticatedBusinessId={AuthenticatedBusinessId}, Success={result.Success}");
    }

    #endregion

    #region Property 20c: GetProductByIdAsync returns null for products belonging to other businesses

    /// <summary>
    /// Property 20c: When GetProductByIdAsync is called for a product that belongs to a different
    /// BusinessId, the system SHALL return null (treat as non-existent).
    /// **Validates: Requirements 7.1, 7.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetProductById_ReturnsNullForOtherBusinessProducts(
        PositiveInt productIdSeed)
    {
        var (service, productRepo, _) = CreateServiceWithBusinessId(AuthenticatedBusinessId);

        var productId = productIdSeed.Get;

        // Mock: repository returns null when queried with authenticated business ID
        // (simulating that the product belongs to a different business)
        productRepo.Setup(r => r.GetByIdAndBusinessIdAsync(productId, AuthenticatedBusinessId))
            .ReturnsAsync((Product?)null);

        var result = service.GetProductByIdAsync(productId).Result;

        // Property: result is null for products not belonging to authenticated business
        var returnsNull = result == null;

        return returnsNull.ToProperty()
            .Label($"ProductId={productId}, Result={result?.Id.ToString() ?? "null"}");
    }

    /// <summary>
    /// Property 20c (positive case): When GetProductByIdAsync is called for a product that belongs
    /// to the authenticated BusinessId, the system SHALL return the product.
    /// **Validates: Requirements 7.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetProductById_ReturnsProductForOwnBusiness(
        PositiveInt productIdSeed,
        PositiveInt codeSeed)
    {
        var (service, productRepo, _) = CreateServiceWithBusinessId(AuthenticatedBusinessId);

        var productId = productIdSeed.Get;
        var ownProduct = new Product
        {
            Id = productId,
            BusinessId = AuthenticatedBusinessId,
            ProductCode = GenerateProductCode(codeSeed.Get),
            Description = "Own product",
            DefaultSellingPrice = 100m,
            DefaultCostPrice = 50m,
            DefaultVatRate = 15m,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        // Mock: repository returns the product when queried with authenticated business ID
        productRepo.Setup(r => r.GetByIdAndBusinessIdAsync(productId, AuthenticatedBusinessId))
            .ReturnsAsync(ownProduct);

        var result = service.GetProductByIdAsync(productId).Result;

        // Property: result is the product belonging to authenticated business
        var returnsProduct = result != null && result.BusinessId == AuthenticatedBusinessId;

        return returnsProduct.ToProperty()
            .Label($"ProductId={productId}, ResultBusinessId={result?.BusinessId}");
    }

    #endregion

    #region Property 20d: Update fails for products belonging to other businesses

    /// <summary>
    /// Property 20d: When UpdateProductAsync is called for a product that belongs to a different
    /// BusinessId, the system SHALL return a failure result (treat as not found).
    /// **Validates: Requirements 7.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpdateProduct_FailsForOtherBusinessProducts(
        PositiveInt productIdSeed,
        PositiveInt codeSeed,
        PositiveInt priceSeed)
    {
        var (service, productRepo, _) = CreateServiceWithBusinessId(AuthenticatedBusinessId);

        var productId = productIdSeed.Get;

        // Mock: repository returns null when queried with authenticated business ID
        // (product belongs to a different business)
        productRepo.Setup(r => r.GetByIdAndBusinessIdAsync(productId, AuthenticatedBusinessId))
            .ReturnsAsync((Product?)null);

        var product = new Product
        {
            Id = productId,
            ProductCode = GenerateProductCode(codeSeed.Get),
            Description = GenerateDescription(codeSeed.Get),
            DefaultSellingPrice = GeneratePrice(priceSeed.Get),
            DefaultCostPrice = GeneratePrice(priceSeed.Get + 1),
            DefaultVatRate = 15.00m
        };

        var result = service.UpdateProductAsync(product, "user-1").Result;

        // Property: update fails with "Product not found."
        var updateFails = !result.Success && result.Message == "Product not found.";

        // Property: repository UpdateAsync was never called
        productRepo.Verify(r => r.UpdateAsync(It.IsAny<Product>()), Times.Never);
        var neverUpdated = true; // If Verify didn't throw, this holds

        var allPropertiesHold = updateFails && neverUpdated;

        return allPropertiesHold.ToProperty()
            .Label($"ProductId={productId}, Success={result.Success}, Message={result.Message}");
    }

    #endregion

    #region Property 20e: Deactivate fails for products belonging to other businesses

    /// <summary>
    /// Property 20e: When DeactivateProductAsync is called for a product that belongs to a different
    /// BusinessId, the system SHALL return a failure result (treat as not found).
    /// **Validates: Requirements 7.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DeactivateProduct_FailsForOtherBusinessProducts(
        PositiveInt productIdSeed)
    {
        var (service, productRepo, _) = CreateServiceWithBusinessId(AuthenticatedBusinessId);

        var productId = productIdSeed.Get;

        // Mock: repository returns null when queried with authenticated business ID
        // (product belongs to a different business)
        productRepo.Setup(r => r.GetByIdAndBusinessIdAsync(productId, AuthenticatedBusinessId))
            .ReturnsAsync((Product?)null);

        var result = service.DeactivateProductAsync(productId).Result;

        // Property: deactivate fails with "Product not found."
        var deactivateFails = !result.Success && result.Message == "Product not found.";

        // Property: repository DeactivateAsync was never called
        productRepo.Verify(r => r.DeactivateAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        var neverDeactivated = true; // If Verify didn't throw, this holds

        var allPropertiesHold = deactivateFails && neverDeactivated;

        return allPropertiesHold.ToProperty()
            .Label($"ProductId={productId}, Success={result.Success}, Message={result.Message}");
    }

    #endregion

    #region Property 20f: Queries are always scoped to authenticated BusinessId

    /// <summary>
    /// Property 20f: For any paged product query, the repository is always called with the
    /// authenticated BusinessId, ensuring tenant-scoped filtering.
    /// **Validates: Requirements 7.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetProductsPaged_AlwaysQueriesWithAuthenticatedBusinessId(
        NonNull<string> searchTerm,
        PositiveInt pageSeed)
    {
        var (service, productRepo, _) = CreateServiceWithBusinessId(AuthenticatedBusinessId);

        int capturedBusinessId = 0;

        productRepo.Setup(r => r.GetPagedByBusinessIdAsync(
                It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>()))
            .Callback<int, string?, int, int>((bizId, _, _, _) => capturedBusinessId = bizId)
            .ReturnsAsync((new List<Product>(), 0));

        var page = (Math.Abs(pageSeed.Get) % 10) + 1;
        _ = service.GetProductsPagedAsync(searchTerm.Get, page).Result;

        // Property: the repository was called with the authenticated BusinessId
        var queriedWithCorrectBusinessId = capturedBusinessId == AuthenticatedBusinessId;

        return queriedWithCorrectBusinessId.ToProperty()
            .Label($"CapturedBusinessId={capturedBusinessId}, AuthenticatedBusinessId={AuthenticatedBusinessId}");
    }

    /// <summary>
    /// Property 20f (KPIs): For any KPI query, the repository is always called with the
    /// authenticated BusinessId, ensuring tenant-scoped filtering.
    /// **Validates: Requirements 7.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetKpis_AlwaysQueriesWithAuthenticatedBusinessId(PositiveInt seed)
    {
        var (service, productRepo, _) = CreateServiceWithBusinessId(AuthenticatedBusinessId);

        int capturedBusinessId = 0;

        productRepo.Setup(r => r.GetKpiDataAsync(It.IsAny<int>()))
            .Callback<int>(bizId => capturedBusinessId = bizId)
            .ReturnsAsync(new ProductKpiDto());

        _ = service.GetKpisAsync().Result;

        // Property: the repository was called with the authenticated BusinessId
        var queriedWithCorrectBusinessId = capturedBusinessId == AuthenticatedBusinessId;

        return queriedWithCorrectBusinessId.ToProperty()
            .Label($"CapturedBusinessId={capturedBusinessId}, AuthenticatedBusinessId={AuthenticatedBusinessId}");
    }

    #endregion

    #region Property 20g: Price history is tenant-isolated

    /// <summary>
    /// Property 20g: GetPriceHistoryAsync verifies product ownership before returning history.
    /// If the product does not belong to the authenticated business, empty list is returned.
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetPriceHistory_ReturnsEmptyForOtherBusinessProducts(
        PositiveInt productIdSeed)
    {
        var (service, productRepo, priceHistoryRepo) = CreateServiceWithBusinessId(AuthenticatedBusinessId);

        var productId = productIdSeed.Get;

        // Mock: product does not belong to authenticated business
        productRepo.Setup(r => r.GetByIdAndBusinessIdAsync(productId, AuthenticatedBusinessId))
            .ReturnsAsync((Product?)null);

        var result = service.GetPriceHistoryAsync(productId).Result;

        // Property: returns empty list for products not belonging to authenticated business
        var returnsEmpty = result.Count == 0;

        // Property: price history repository was never called
        priceHistoryRepo.Verify(r => r.GetByProductIdAsync(It.IsAny<int>()), Times.Never);
        var neverQueried = true;

        var allPropertiesHold = returnsEmpty && neverQueried;

        return allPropertiesHold.ToProperty()
            .Label($"ProductId={productId}, HistoryCount={result.Count}");
    }

    /// <summary>
    /// Property 20g (positive case): GetPriceHistoryAsync returns history for products
    /// belonging to the authenticated business.
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetPriceHistory_ReturnsDataForOwnBusinessProducts(
        PositiveInt productIdSeed,
        PositiveInt historyCountSeed)
    {
        var (service, productRepo, priceHistoryRepo) = CreateServiceWithBusinessId(AuthenticatedBusinessId);

        var productId = productIdSeed.Get;
        var historyCount = (Math.Abs(historyCountSeed.Get) % 5) + 1;

        // Mock: product belongs to authenticated business
        productRepo.Setup(r => r.GetByIdAndBusinessIdAsync(productId, AuthenticatedBusinessId))
            .ReturnsAsync(new Product
            {
                Id = productId,
                BusinessId = AuthenticatedBusinessId,
                ProductCode = "TEST-001",
                Description = "Test Product",
                DefaultSellingPrice = 100m,
                DefaultCostPrice = 50m,
                DefaultVatRate = 15m,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });

        // Mock: price history exists
        var historyRecords = Enumerable.Range(1, historyCount)
            .Select(i => new ProductPriceHistory
            {
                Id = i,
                ProductId = productId,
                SellingPrice = 100m + i,
                CostPrice = 50m + i,
                EffectiveFromUtc = DateTime.UtcNow.AddDays(-i),
                ChangedByUserId = "user-1",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-i)
            })
            .ToList();

        priceHistoryRepo.Setup(r => r.GetByProductIdAsync(productId))
            .ReturnsAsync(historyRecords);

        var result = service.GetPriceHistoryAsync(productId).Result;

        // Property: returns the expected history records
        var returnsHistory = result.Count == historyCount;

        return returnsHistory.ToProperty()
            .Label($"ProductId={productId}, ExpectedCount={historyCount}, ActualCount={result.Count}");
    }

    #endregion

    #region Property 20h: Auto-population stamps authenticated BusinessId on new products

    /// <summary>
    /// Property 20h: When auto-population creates a new Product, it SHALL always be stamped
    /// with the authenticated user's BusinessId.
    /// **Validates: Requirements 7.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AutoPopulate_NewProductStampedWithAuthenticatedBusinessId(
        PositiveInt codeSeed,
        PositiveInt priceSeed)
    {
        var (service, productRepo, priceHistoryRepo) = CreateServiceWithBusinessId(AuthenticatedBusinessId);

        var productCode = GenerateProductCode(codeSeed.Get);
        var unitPrice = GeneratePrice(priceSeed.Get);
        var vatRate = GenerateVatRate(priceSeed.Get + 1);

        Product? capturedProduct = null;

        // Mock: no existing product matches
        productRepo.Setup(r => r.GetByProductCodeAndBusinessIdAsync(
                It.IsAny<string>(), AuthenticatedBusinessId))
            .ReturnsAsync((Product?)null);

        productRepo.Setup(r => r.GetByDescriptionAndBusinessIdAsync(
                It.IsAny<string>(), AuthenticatedBusinessId))
            .ReturnsAsync((Product?)null);

        // Capture the product that gets inserted
        productRepo.Setup(r => r.InsertAsync(It.IsAny<Product>()))
            .Callback<Product>(p => capturedProduct = p)
            .ReturnsAsync(1);

        priceHistoryRepo.Setup(r => r.InsertAsync(It.IsAny<ProductPriceHistory>()))
            .Returns(Task.CompletedTask);

        service.AutoPopulateFromLineItemAsync(productCode, "Some Description", unitPrice, vatRate, "user-1").Wait();

        // Property: the new product was stamped with the authenticated BusinessId
        var stampedCorrectly = capturedProduct != null
                            && capturedProduct.BusinessId == AuthenticatedBusinessId;

        return stampedCorrectly.ToProperty()
            .Label($"CapturedBusinessId={capturedProduct?.BusinessId}, " +
                   $"AuthenticatedBusinessId={AuthenticatedBusinessId}");
    }

    #endregion
}
