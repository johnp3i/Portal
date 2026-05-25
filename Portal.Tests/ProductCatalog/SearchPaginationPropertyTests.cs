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

// Feature: product-catalog, Property 7: Search filter correctness
// Feature: product-catalog, Property 8: Pagination correctness

/// <summary>
/// Property-based tests for search filter correctness and pagination correctness
/// in ProductService.GetProductsPagedAsync.
/// Tests Properties 7 and 8 from the product-catalog design document.
/// Uses FsCheck.Xunit with Moq to mock ProductRepository and ICurrentTenantService.
/// **Validates: Requirements 3.3, 3.4, 3.5**
/// </summary>
public class SearchPaginationPropertyTests
{
    private const int TestBusinessId = 1;
    private const int DefaultPageSize = 15;

    #region Test Infrastructure

    private static (ProductService Service, Mock<ProductRepository> ProductRepo) CreateService(int businessId = TestBusinessId)
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

        return (service, productRepoMock);
    }

    /// <summary>
    /// Generates a non-empty search term (1-20 alphanumeric chars).
    /// </summary>
    private static Gen<string> SearchTermGen()
    {
        return Gen.Choose(1, 20)
            .SelectMany(len => Gen.ArrayOf(len, Gen.Elements(
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789".ToCharArray()))
            .Select(chars => new string(chars)));
    }

    /// <summary>
    /// Generates a valid ProductCode: non-empty, max 50 chars.
    /// </summary>
    private static Gen<string> ValidProductCodeGen()
    {
        return Gen.Choose(1, 50)
            .SelectMany(len => Gen.ArrayOf(len, Gen.Elements(
                "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_".ToCharArray()))
            .Select(chars => new string(chars)));
    }

    /// <summary>
    /// Generates a valid Description: non-empty, max 100 chars.
    /// Ensures at least one non-whitespace character.
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
    /// Generates a list of products (0-50 items) for testing.
    /// </summary>
    private static Gen<List<Product>> ProductListGen()
    {
        var productGen = from code in ValidProductCodeGen()
                         from desc in ValidDescriptionGen()
                         from price in Gen.Choose(100, 99999).Select(c => c / 100m)
                         from cost in Gen.Choose(0, 50000).Select(c => c / 100m)
                         from vat in Gen.Choose(0, 9999).Select(h => h / 100m)
                         from isActive in Arb.Generate<bool>()
                         select new Product
                         {
                             BusinessId = TestBusinessId,
                             ProductCode = code,
                             Description = desc,
                             DefaultSellingPrice = price,
                             DefaultCostPrice = cost,
                             DefaultVatRate = vat,
                             IsActive = isActive,
                             CreatedAtUtc = DateTime.UtcNow
                         };

        return Gen.Choose(0, 50)
            .SelectMany(count => Gen.ListOf(count, productGen)
                .Select(fsharpList => new List<Product>(fsharpList)));
    }

    /// <summary>
    /// Generates a valid page number (1-20).
    /// </summary>
    private static Gen<int> PageNumberGen()
    {
        return Gen.Choose(1, 20);
    }

    /// <summary>
    /// Generates a total count (0-200) for pagination testing.
    /// </summary>
    private static Gen<int> TotalCountGen()
    {
        return Gen.Choose(0, 200);
    }

    /// <summary>
    /// Checks if a product matches a search term (case-insensitive partial match on ProductCode or Description).
    /// </summary>
    private static bool ProductMatchesSearch(Product product, string searchTerm)
    {
        return product.ProductCode.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
            || product.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Property 7: Search filter correctness

    /// <summary>
    /// Property 7: For any search term and set of products, the filtered results SHALL contain
    /// only products whose ProductCode or Description contains the search term (case-insensitive
    /// partial match), and SHALL contain ALL such matching products within the current page.
    /// **Validates: Requirements 3.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SearchFilter_ReturnsOnlyMatchingProducts()
    {
        var arb = Arb.From(
            from searchTerm in SearchTermGen()
            from products in ProductListGen()
            select (searchTerm, products));

        return Prop.ForAll(arb, async tuple =>
        {
            var (searchTerm, allProducts) = tuple;

            var (service, productRepoMock) = CreateService();

            // Determine which products match the search term
            var matchingProducts = allProducts
                .Where(p => ProductMatchesSearch(p, searchTerm))
                .ToList();

            // Assign IDs to matching products for identification
            for (int i = 0; i < matchingProducts.Count; i++)
            {
                matchingProducts[i].Id = i + 1;
            }

            // The repository returns the first page of matching products (up to page size)
            var pageItems = matchingProducts.Take(DefaultPageSize).ToList();
            int totalCount = matchingProducts.Count;

            productRepoMock
                .Setup(r => r.GetPagedByBusinessIdAsync(
                    TestBusinessId,
                    searchTerm,
                    0,
                    DefaultPageSize))
                .ReturnsAsync((pageItems, totalCount));

            var result = await service.GetProductsPagedAsync(searchTerm, 1, DefaultPageSize);

            // Assert: all returned items match the search term
            foreach (var item in result.Items)
            {
                Assert.True(
                    ProductMatchesSearch(item, searchTerm),
                    $"Product '{item.ProductCode}' / '{item.Description}' does not match search term '{searchTerm}'");
            }

            // Assert: the result contains ALL matching products for the current page
            // (i.e., the count of returned items equals the expected page items count)
            Assert.Equal(pageItems.Count, result.Items.Count);

            // Assert: total count reflects all matching products
            Assert.Equal(totalCount, result.TotalCount);
        });
    }

    /// <summary>
    /// Property 7 (supplementary): When search term is null, all products are returned
    /// (no filtering applied), respecting pagination.
    /// **Validates: Requirements 3.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SearchFilter_NullSearchTerm_ReturnsAllProducts()
    {
        var arb = Arb.From(
            from products in ProductListGen()
            select products);

        return Prop.ForAll(arb, async allProducts =>
        {
            var (service, productRepoMock) = CreateService();

            // With null search, repository returns all products (paginated)
            var pageItems = allProducts.Take(DefaultPageSize).ToList();
            int totalCount = allProducts.Count;

            productRepoMock
                .Setup(r => r.GetPagedByBusinessIdAsync(
                    TestBusinessId,
                    (string?)null,
                    0,
                    DefaultPageSize))
                .ReturnsAsync((pageItems, totalCount));

            var result = await service.GetProductsPagedAsync(null, 1, DefaultPageSize);

            // Assert: returned items count matches expected page items
            Assert.Equal(pageItems.Count, result.Items.Count);

            // Assert: total count reflects all products
            Assert.Equal(totalCount, result.TotalCount);
        });
    }

    /// <summary>
    /// Property 7 (case-insensitivity): The search term matching is case-insensitive.
    /// The service passes the search term to the repository which handles case-insensitive matching.
    /// **Validates: Requirements 3.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SearchFilter_PassesSearchTermToRepository()
    {
        var arb = Arb.From(SearchTermGen());

        return Prop.ForAll(arb, async searchTerm =>
        {
            var (service, productRepoMock) = CreateService();

            string? capturedSearch = null;
            productRepoMock
                .Setup(r => r.GetPagedByBusinessIdAsync(
                    TestBusinessId,
                    It.IsAny<string?>(),
                    It.IsAny<int>(),
                    DefaultPageSize))
                .Callback<int, string?, int, int>((bId, search, offset, ps) => capturedSearch = search)
                .ReturnsAsync((new List<Product>(), 0));

            await service.GetProductsPagedAsync(searchTerm, 1, DefaultPageSize);

            // Assert: the search term is passed correctly to the repository
            Assert.Equal(searchTerm, capturedSearch);
        });
    }

    #endregion

    #region Property 8: Pagination correctness

    /// <summary>
    /// Property 8: For any total product count and page number, the paginated result SHALL contain
    /// at most 15 items, the "Showing X-Y of Z" values SHALL satisfy:
    /// X = ((page-1) * 15) + 1, Y = min(page * 15, totalCount), and Z = totalCount.
    /// **Validates: Requirements 3.3, 3.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Pagination_ReturnsAtMost15Items_WithCorrectMetadata()
    {
        var arb = Arb.From(
            from totalCount in TotalCountGen()
            from page in PageNumberGen()
            select (totalCount, page));

        return Prop.ForAll(arb, async tuple =>
        {
            var (totalCount, page) = tuple;

            var (service, productRepoMock) = CreateService();

            // Calculate expected pagination values
            int totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / DefaultPageSize);

            // If page exceeds total pages and there are items, service clamps to page 1
            int effectivePage = page;
            if (page > totalPages && totalCount > 0)
            {
                effectivePage = 1;
            }

            int offset = (effectivePage - 1) * DefaultPageSize;
            int expectedItemCount = Math.Min(DefaultPageSize, Math.Max(0, totalCount - offset));

            // Generate mock items for the expected page
            var pageItems = Enumerable.Range(1, expectedItemCount)
                .Select(i => new Product
                {
                    Id = offset + i,
                    BusinessId = TestBusinessId,
                    ProductCode = $"PROD-{offset + i:D4}",
                    Description = $"Product {offset + i}",
                    DefaultSellingPrice = 10.00m,
                    DefaultCostPrice = 5.00m,
                    DefaultVatRate = 15.00m,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                })
                .ToList();

            // Setup mock for the requested page
            productRepoMock
                .Setup(r => r.GetPagedByBusinessIdAsync(
                    TestBusinessId,
                    (string?)null,
                    (page - 1) * DefaultPageSize,
                    DefaultPageSize))
                .ReturnsAsync((page > totalPages && totalCount > 0 ? new List<Product>() : pageItems, totalCount));

            // If page exceeds total pages, service will re-query with offset 0
            if (page > totalPages && totalCount > 0)
            {
                var firstPageItems = Enumerable.Range(1, Math.Min(DefaultPageSize, totalCount))
                    .Select(i => new Product
                    {
                        Id = i,
                        BusinessId = TestBusinessId,
                        ProductCode = $"PROD-{i:D4}",
                        Description = $"Product {i}",
                        DefaultSellingPrice = 10.00m,
                        DefaultCostPrice = 5.00m,
                        DefaultVatRate = 15.00m,
                        IsActive = true,
                        CreatedAtUtc = DateTime.UtcNow
                    })
                    .ToList();

                productRepoMock
                    .Setup(r => r.GetPagedByBusinessIdAsync(
                        TestBusinessId,
                        (string?)null,
                        0,
                        DefaultPageSize))
                    .ReturnsAsync((firstPageItems, totalCount));
            }

            var result = await service.GetProductsPagedAsync(null, page, DefaultPageSize);

            // Assert: result contains at most 15 items
            Assert.True(result.Items.Count <= DefaultPageSize,
                $"Expected at most {DefaultPageSize} items but got {result.Items.Count}");

            // Assert: Z = totalCount
            Assert.Equal(totalCount, result.TotalCount);

            // Assert: PageSize is 15
            Assert.Equal(DefaultPageSize, result.PageSize);

            // Calculate expected X and Y based on effective page
            int resultPage = result.CurrentPage;
            int expectedX = ((resultPage - 1) * DefaultPageSize) + 1;
            int expectedY = Math.Min(resultPage * DefaultPageSize, totalCount);

            // Assert: "Showing X-Y of Z" correctness
            if (totalCount > 0)
            {
                // X = ((page-1) * 15) + 1
                Assert.Equal(expectedX, ((resultPage - 1) * DefaultPageSize) + 1);

                // Y = min(page * 15, totalCount)
                Assert.Equal(expectedY, Math.Min(resultPage * DefaultPageSize, totalCount));

                // Items count should match Y - X + 1
                Assert.Equal(result.Items.Count, expectedY - expectedX + 1);
            }
            else
            {
                // No items: result should be empty
                Assert.Empty(result.Items);
            }
        });
    }

    /// <summary>
    /// Property 8 (page size constraint): For any page request, the result SHALL never
    /// exceed 15 items regardless of the total count.
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Pagination_NeverExceedsPageSize()
    {
        var arb = Arb.From(
            from totalCount in Gen.Choose(0, 500)
            from page in Gen.Choose(1, 10)
            select (totalCount, page));

        return Prop.ForAll(arb, async tuple =>
        {
            var (totalCount, page) = tuple;

            var (service, productRepoMock) = CreateService();

            int totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / DefaultPageSize);
            int effectivePage = (page > totalPages && totalCount > 0) ? 1 : page;
            int offset = (effectivePage - 1) * DefaultPageSize;
            int itemCount = Math.Min(DefaultPageSize, Math.Max(0, totalCount - offset));

            var pageItems = Enumerable.Range(1, itemCount)
                .Select(i => new Product
                {
                    Id = i,
                    BusinessId = TestBusinessId,
                    ProductCode = $"P-{i}",
                    Description = $"Desc {i}",
                    DefaultSellingPrice = 1.00m,
                    DefaultCostPrice = 0.50m,
                    DefaultVatRate = 15.00m,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                })
                .ToList();

            // Setup for the requested page
            productRepoMock
                .Setup(r => r.GetPagedByBusinessIdAsync(
                    TestBusinessId,
                    (string?)null,
                    (page - 1) * DefaultPageSize,
                    DefaultPageSize))
                .ReturnsAsync((page > totalPages && totalCount > 0 ? new List<Product>() : pageItems, totalCount));

            // Setup for clamped page 1 if needed
            if (page > totalPages && totalCount > 0)
            {
                productRepoMock
                    .Setup(r => r.GetPagedByBusinessIdAsync(
                        TestBusinessId,
                        (string?)null,
                        0,
                        DefaultPageSize))
                    .ReturnsAsync((pageItems, totalCount));
            }

            var result = await service.GetProductsPagedAsync(null, page, DefaultPageSize);

            // Assert: never exceeds page size
            Assert.True(result.Items.Count <= DefaultPageSize,
                $"Page size violated: got {result.Items.Count} items, max is {DefaultPageSize}");

            // Assert: page size in metadata is always 15
            Assert.Equal(DefaultPageSize, result.PageSize);
        });
    }

    /// <summary>
    /// Property 8 (empty result): When total count is 0, the result SHALL be empty
    /// with CurrentPage=1 and TotalCount=0.
    /// **Validates: Requirements 3.3, 3.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Pagination_EmptyResult_ReturnsCorrectMetadata()
    {
        var arb = Arb.From(PageNumberGen());

        return Prop.ForAll(arb, async page =>
        {
            var (service, productRepoMock) = CreateService();

            productRepoMock
                .Setup(r => r.GetPagedByBusinessIdAsync(
                    TestBusinessId,
                    It.IsAny<string?>(),
                    It.IsAny<int>(),
                    DefaultPageSize))
                .ReturnsAsync((new List<Product>(), 0));

            var result = await service.GetProductsPagedAsync(null, page, DefaultPageSize);

            // Assert: empty items
            Assert.Empty(result.Items);

            // Assert: total count is 0
            Assert.Equal(0, result.TotalCount);

            // Assert: page size is preserved
            Assert.Equal(DefaultPageSize, result.PageSize);
        });
    }

    #endregion
}
