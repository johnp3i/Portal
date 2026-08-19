using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;

namespace Portal.Tests.PropertyBased;

// Feature: invoice-line-product-type-reverse-charge, Property 1: Reverse charge invariant (quotation lines)

/// <summary>
/// Property-based tests for QuotationService.AddLineAsync and UpdateLineAsync reverse charge validation.
/// Validates that quotation lines with IsReverseCharge=true and VatRate > 0 are rejected
/// with ArgumentException and no persistence occurs, while valid combinations succeed.
/// **Validates: Requirements 5.3, 5.6, 8.1, 8.5**
/// </summary>
public class ReverseChargeQuotationLinePropertyTests
{
    private const int TestBusinessId = 1;
    private const int TestQuotationId = 100;
    private const int TestLineId = 200;

    #region Shared Mock Setup

    private static (
        Mock<ICurrentTenantService> tenantMock,
        Mock<QuotationRepository> quotationRepoMock,
        Mock<QuotationLineRepository> lineRepoMock,
        Mock<AuditLogRepository> auditLogRepoMock,
        Mock<CustomerRepository> customerRepoMock,
        Mock<ILineItemCatalogService> lineItemCatalogServiceMock,
        Mock<IProductService> productServiceMock,
        Mock<IHttpContextAccessor> httpContextAccessorMock,
        Mock<ILogger<QuotationService>> loggerMock,
        Mock<PortalDbContext> dbContextMock
    ) CreateMocks()
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

        var dbContextOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var dbContextMock = new Mock<PortalDbContext>(dbContextOptions, tenantMock.Object) { CallBase = true };

        var quotationRepoMock = new Mock<QuotationRepository>(dbContextMock.Object) { CallBase = false };
        var lineRepoMock = new Mock<QuotationLineRepository>(dbContextMock.Object) { CallBase = false };
        var auditLogRepoMock = new Mock<AuditLogRepository>(dbContextMock.Object) { CallBase = false };
        var customerRepoMock = new Mock<CustomerRepository>(dbContextMock.Object) { CallBase = false };

        var lineItemCatalogServiceMock = new Mock<ILineItemCatalogService>();
        var productServiceMock = new Mock<IProductService>();
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var loggerMock = new Mock<ILogger<QuotationService>>();

        return (tenantMock, quotationRepoMock, lineRepoMock, auditLogRepoMock,
            customerRepoMock, lineItemCatalogServiceMock, productServiceMock,
            httpContextAccessorMock, loggerMock, dbContextMock);
    }

    private static QuotationService CreateService(
        Mock<ICurrentTenantService> tenantMock,
        Mock<QuotationRepository> quotationRepoMock,
        Mock<QuotationLineRepository> lineRepoMock,
        Mock<AuditLogRepository> auditLogRepoMock,
        Mock<CustomerRepository> customerRepoMock,
        Mock<ILineItemCatalogService> lineItemCatalogServiceMock,
        Mock<IProductService> productServiceMock,
        Mock<IHttpContextAccessor> httpContextAccessorMock,
        Mock<ILogger<QuotationService>> loggerMock)
    {
        return new QuotationService(
            quotationRepoMock.Object,
            lineRepoMock.Object,
            auditLogRepoMock.Object,
            customerRepoMock.Object,
            new Mock<ProposalSectionRepository>(MockBehavior.Loose, new object[] { null }).Object,
            new Mock<ProductPriceTierRepository>(MockBehavior.Loose, new object[] { null }).Object,
            tenantMock.Object,
            lineItemCatalogServiceMock.Object,
            productServiceMock.Object,
            httpContextAccessorMock.Object,
            loggerMock.Object);
    }

    private static void SetupDraftQuotation(Mock<QuotationRepository> quotationRepoMock)
    {
        quotationRepoMock
            .Setup(r => r.GetByIdAndBusinessIdAsync(TestQuotationId, TestBusinessId))
            .ReturnsAsync(new Quotation
            {
                Id = TestQuotationId,
                BusinessId = TestBusinessId,
                QuotationStatusTypeId = 1, // Draft
                CustomerId = 1,
                Reference = "QUO-1-00001",
                Subtotal = 0m,
                TaxAmount = 0m,
                TotalAmount = 0m,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
    }

    private static void SetupExistingLine(Mock<QuotationLineRepository> lineRepoMock)
    {
        lineRepoMock
            .Setup(r => r.GetByIdAsync(TestLineId))
            .ReturnsAsync(new QuotationLine
            {
                Id = TestLineId,
                QuotationId = TestQuotationId,
                Description = "Existing line",
                Quantity = 1m,
                UnitPrice = 100m,
                VatRate = 0m,
                Discount = 0m,
                DiscountType = "Percentage",
                LineTotal = 100m,
                SortOrder = 1,
                IsReverseCharge = false
            });
    }

    #endregion

    #region Property 1a: Reverse charge with VatRate > 0 is rejected (AddLineAsync)

    /// <summary>
    /// Property 1a: For any quotation line submission where IsReverseCharge=true and VatRate > 0,
    /// the service layer SHALL reject the submission with ArgumentException,
    /// and no line shall be persisted.
    /// **Validates: Requirements 5.3, 5.6, 8.1, 8.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AddLineAsync_ReverseChargeWithPositiveVatRate_ThrowsArgumentException()
    {
        // Generate random vatRate values > 0 (between 0.01 and 99.99)
        var positiveVatRateGen = Gen.Choose(1, 9999).Select(i => (decimal)i / 100m);

        return Prop.ForAll(
            positiveVatRateGen.ToArbitrary(),
            (vatRate) =>
            {
                var (tenantMock, quotationRepoMock, lineRepoMock, auditLogRepoMock,
                    customerRepoMock, lineItemCatalogServiceMock, productServiceMock,
                    httpContextAccessorMock, loggerMock, dbContextMock) = CreateMocks();

                SetupDraftQuotation(quotationRepoMock);

                var service = CreateService(tenantMock, quotationRepoMock, lineRepoMock,
                    auditLogRepoMock, customerRepoMock, lineItemCatalogServiceMock,
                    productServiceMock, httpContextAccessorMock, loggerMock);

                // Act & Assert: should throw ArgumentException
                var threw = false;
                try
                {
                    service.AddLineAsync(
                        quotationId: TestQuotationId,
                        description: "Test line",
                        quantity: 1m,
                        unitPrice: 100m,
                        vatRate: vatRate,
                        isReverseCharge: true
                    ).GetAwaiter().GetResult();
                }
                catch (ArgumentException)
                {
                    threw = true;
                }

                // Verify no persistence occurred
                var insertNeverCalled = true;
                try
                {
                    lineRepoMock.Verify(
                        r => r.InsertAsync(It.IsAny<QuotationLine>()),
                        Times.Never);
                }
                catch
                {
                    insertNeverCalled = false;
                }

                return threw
                    .Label($"AddLineAsync should throw ArgumentException for isReverseCharge=true with vatRate={vatRate}")
                    .And(insertNeverCalled
                        .Label("InsertAsync should never be called when reverse charge validation fails"));
            });
    }

    #endregion

    #region Property 1b: Reverse charge with VatRate > 0 is rejected (UpdateLineAsync)

    /// <summary>
    /// Property 1b: For any quotation line update where IsReverseCharge=true and VatRate > 0,
    /// the service layer SHALL reject the update with ArgumentException,
    /// and no line shall be updated.
    /// **Validates: Requirements 5.3, 5.6, 8.1, 8.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UpdateLineAsync_ReverseChargeWithPositiveVatRate_ThrowsArgumentException()
    {
        // Generate random vatRate values > 0 (between 0.01 and 99.99)
        var positiveVatRateGen = Gen.Choose(1, 9999).Select(i => (decimal)i / 100m);

        return Prop.ForAll(
            positiveVatRateGen.ToArbitrary(),
            (vatRate) =>
            {
                var (tenantMock, quotationRepoMock, lineRepoMock, auditLogRepoMock,
                    customerRepoMock, lineItemCatalogServiceMock, productServiceMock,
                    httpContextAccessorMock, loggerMock, dbContextMock) = CreateMocks();

                SetupDraftQuotation(quotationRepoMock);
                SetupExistingLine(lineRepoMock);

                var service = CreateService(tenantMock, quotationRepoMock, lineRepoMock,
                    auditLogRepoMock, customerRepoMock, lineItemCatalogServiceMock,
                    productServiceMock, httpContextAccessorMock, loggerMock);

                // Act & Assert: should throw ArgumentException
                var threw = false;
                try
                {
                    service.UpdateLineAsync(
                        lineId: TestLineId,
                        description: "Updated line",
                        quantity: 1m,
                        unitPrice: 100m,
                        vatRate: vatRate,
                        isReverseCharge: true
                    ).GetAwaiter().GetResult();
                }
                catch (ArgumentException)
                {
                    threw = true;
                }

                // Verify no update occurred (validation happens before GetByIdAsync)
                var getByIdNeverCalled = true;
                try
                {
                    lineRepoMock.Verify(
                        r => r.GetByIdAsync(It.IsAny<int>()),
                        Times.Never);
                }
                catch
                {
                    getByIdNeverCalled = false;
                }

                return threw
                    .Label($"UpdateLineAsync should throw ArgumentException for isReverseCharge=true with vatRate={vatRate}")
                    .And(getByIdNeverCalled
                        .Label("GetByIdAsync should never be called when reverse charge validation fails (validation is first)"));
            });
    }

    #endregion

    #region Property 1c: Valid combinations succeed (AddLineAsync)

    /// <summary>
    /// Property 1c: For any valid combination (IsReverseCharge=false with any vatRate,
    /// or IsReverseCharge=true with vatRate=0), the service layer SHALL accept the submission
    /// and persist the line.
    /// **Validates: Requirements 5.3, 5.6, 8.1, 8.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AddLineAsync_ValidCombinations_Succeeds()
    {
        // Valid combinations:
        // 1. isReverseCharge=true with vatRate=0
        // 2. isReverseCharge=false with any vatRate >= 0
        var validCombinationGen = Gen.OneOf(
            // RC=true, vatRate=0
            Gen.Constant((true, 0m)),
            // RC=false, vatRate >= 0
            Gen.Choose(0, 9999).Select(i => (false, (decimal)i / 100m))
        );

        return Prop.ForAll(
            validCombinationGen.ToArbitrary(),
            (combination) =>
            {
                var (isReverseCharge, vatRate) = combination;

                var (tenantMock, quotationRepoMock, lineRepoMock, auditLogRepoMock,
                    customerRepoMock, lineItemCatalogServiceMock, productServiceMock,
                    httpContextAccessorMock, loggerMock, dbContextMock) = CreateMocks();

                SetupDraftQuotation(quotationRepoMock);

                // Setup: GetByQuotationIdAsync returns empty list (for SortOrder calculation)
                lineRepoMock
                    .Setup(r => r.GetByQuotationIdAsync(TestQuotationId))
                    .ReturnsAsync(new List<QuotationLine>());

                // Setup: InsertAsync succeeds
                lineRepoMock
                    .Setup(r => r.InsertAsync(It.IsAny<QuotationLine>()))
                    .Returns(Task.CompletedTask);

                // Setup: quotation update for totals recomputation
                quotationRepoMock
                    .Setup(r => r.UpdateAsync(It.IsAny<Quotation>()))
                    .Returns(Task.CompletedTask);

                // Setup: product service auto-populate
                productServiceMock
                    .Setup(s => s.AutoPopulateFromLineItemAsync(
                        It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<decimal>(),
                        It.IsAny<decimal>(), It.IsAny<string>()))
                    .Returns(Task.CompletedTask);

                var service = CreateService(tenantMock, quotationRepoMock, lineRepoMock,
                    auditLogRepoMock, customerRepoMock, lineItemCatalogServiceMock,
                    productServiceMock, httpContextAccessorMock, loggerMock);

                // Act: should NOT throw
                var succeeded = false;
                try
                {
                    service.AddLineAsync(
                        quotationId: TestQuotationId,
                        description: "Test line",
                        quantity: 1m,
                        unitPrice: 100m,
                        vatRate: vatRate,
                        isReverseCharge: isReverseCharge
                    ).GetAwaiter().GetResult();

                    succeeded = true;
                }
                catch (ArgumentException)
                {
                    succeeded = false;
                }

                return succeeded
                    .Label($"AddLineAsync should succeed for isReverseCharge={isReverseCharge} with vatRate={vatRate}");
            });
    }

    #endregion
}
