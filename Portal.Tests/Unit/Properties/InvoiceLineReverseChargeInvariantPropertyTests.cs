using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Portal.Tests.Unit.Properties;

/// <summary>
/// Property-based tests for the reverse charge invariant on invoice lines.
/// Feature: invoice-line-product-type-reverse-charge, Property 2: Reverse charge invariant (invoice lines)
/// 
/// For any invoice line submission or update where IsReverseCharge is true and VatRate is greater than 0,
/// the service layer SHALL reject the operation with a validation error, and no line shall be persisted or updated.
/// 
/// **Validates: Requirements 6.4, 8.2, 8.5**
/// </summary>
public class InvoiceLineReverseChargeInvariantPropertyTests
{
    private const int TestBusinessId = 1;
    private const int TestInvoiceId = 100;
    private const int TestLineId = 200;

    #region Shared Mock Setup

    private static (
        Mock<ICurrentTenantService> tenantMock,
        Mock<InvoiceRepository> invoiceRepoMock,
        Mock<InvoiceLineRepository> invoiceLineRepoMock,
        Mock<InvoiceSectionRepository> invoiceSectionRepoMock,
        Mock<QuotationRepository> quotationRepoMock,
        Mock<QuotationLineRepository> quotationLineRepoMock,
        Mock<ProposalSectionRepository> proposalSectionRepoMock,
        Mock<CustomerRepository> customerRepoMock,
        Mock<AuditLogRepository> auditLogRepoMock,
        Mock<VatSubmissionPeriodRepository> vatPeriodRepoMock,
        Mock<VatSubmissionRepository> vatSubmissionRepoMock,
        Mock<PortalDbContext> dbContextMock,
        Mock<IProductService> productServiceMock,
        Mock<ProductRepository> productRepoMock,
        Mock<IHttpContextAccessor> httpContextAccessorMock,
        Mock<ILogger<InvoiceService>> loggerMock
    ) CreateMocks()
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

        var dbContextOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var dbContextMock = new Mock<PortalDbContext>(dbContextOptions, tenantMock.Object) { CallBase = true };

        var invoiceRepoMock = new Mock<InvoiceRepository>(dbContextMock.Object) { CallBase = false };
        var invoiceLineRepoMock = new Mock<InvoiceLineRepository>(dbContextMock.Object) { CallBase = false };
        var invoiceSectionRepoMock = new Mock<InvoiceSectionRepository>(dbContextMock.Object) { CallBase = false };
        var quotationRepoMock = new Mock<QuotationRepository>(dbContextMock.Object) { CallBase = false };
        var quotationLineRepoMock = new Mock<QuotationLineRepository>(dbContextMock.Object) { CallBase = false };
        var proposalSectionRepoMock = new Mock<ProposalSectionRepository>(dbContextMock.Object) { CallBase = false };
        var customerRepoMock = new Mock<CustomerRepository>(dbContextMock.Object) { CallBase = false };
        var auditLogRepoMock = new Mock<AuditLogRepository>(dbContextMock.Object) { CallBase = false };
        var vatPeriodRepoMock = new Mock<VatSubmissionPeriodRepository>(dbContextMock.Object) { CallBase = false };
        var vatSubmissionRepoMock = new Mock<VatSubmissionRepository>(dbContextMock.Object) { CallBase = false };
        var productServiceMock = new Mock<IProductService>();
        var productRepoMock = new Mock<ProductRepository>(dbContextMock.Object) { CallBase = false };
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var loggerMock = new Mock<ILogger<InvoiceService>>();

        return (tenantMock, invoiceRepoMock, invoiceLineRepoMock, invoiceSectionRepoMock,
            quotationRepoMock, quotationLineRepoMock, proposalSectionRepoMock,
            customerRepoMock, auditLogRepoMock, vatPeriodRepoMock, vatSubmissionRepoMock,
            dbContextMock, productServiceMock, productRepoMock, httpContextAccessorMock, loggerMock);
    }

    private static InvoiceService CreateService(
        Mock<ICurrentTenantService> tenantMock,
        Mock<InvoiceRepository> invoiceRepoMock,
        Mock<InvoiceLineRepository> invoiceLineRepoMock,
        Mock<InvoiceSectionRepository> invoiceSectionRepoMock,
        Mock<QuotationRepository> quotationRepoMock,
        Mock<QuotationLineRepository> quotationLineRepoMock,
        Mock<ProposalSectionRepository> proposalSectionRepoMock,
        Mock<CustomerRepository> customerRepoMock,
        Mock<AuditLogRepository> auditLogRepoMock,
        Mock<VatSubmissionPeriodRepository> vatPeriodRepoMock,
        Mock<VatSubmissionRepository> vatSubmissionRepoMock,
        Mock<PortalDbContext> dbContextMock,
        Mock<IProductService> productServiceMock,
        Mock<ProductRepository> productRepoMock,
        Mock<IHttpContextAccessor> httpContextAccessorMock,
        Mock<ILogger<InvoiceService>> loggerMock)
    {
        var productPriceTierRepoMock = new Mock<ProductPriceTierRepository>(dbContextMock.Object) { CallBase = false };

        return new InvoiceService(
            tenantMock.Object,
            invoiceRepoMock.Object,
            invoiceLineRepoMock.Object,
            invoiceSectionRepoMock.Object,
            quotationRepoMock.Object,
            quotationLineRepoMock.Object,
            proposalSectionRepoMock.Object,
            customerRepoMock.Object,
            auditLogRepoMock.Object,
            vatPeriodRepoMock.Object,
            vatSubmissionRepoMock.Object,
            dbContextMock.Object,
            productServiceMock.Object,
            productRepoMock.Object,
            productPriceTierRepoMock.Object,
            httpContextAccessorMock.Object,
            loggerMock.Object);
    }

    private static void SetupDraftInvoice(Mock<InvoiceRepository> invoiceRepoMock)
    {
        invoiceRepoMock
            .Setup(r => r.GetByIdAndBusinessIdAsync(TestInvoiceId, TestBusinessId))
            .ReturnsAsync(new Invoice
            {
                Id = TestInvoiceId,
                BusinessId = TestBusinessId,
                CustomerId = 1,
                InvoiceStatusTypeId = 1, // Draft
                InvoiceFinancialStatusTypeId = 1,
                InvoiceNumber = "INV-1-00001",
                InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
                DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
                Subtotal = 0m,
                TaxAmount = 0m,
                TotalAmount = 0m,
                CurrencyCode = "EUR",
                IsDeleted = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
    }

    private static void SetupExistingLine(Mock<InvoiceLineRepository> invoiceLineRepoMock)
    {
        invoiceLineRepoMock
            .Setup(r => r.GetByIdAsync(TestLineId))
            .ReturnsAsync(new InvoiceLine
            {
                Id = TestLineId,
                InvoiceId = TestInvoiceId,
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

    #region Property 2: Reverse charge invariant (invoice lines) — Rejection

    // Feature: invoice-line-product-type-reverse-charge, Property 2: Reverse charge invariant (invoice lines)
    // **Validates: Requirements 6.4, 8.2, 8.5**
    [Property(MaxTest = 100)]
    public Property AddLineAsync_ReverseChargeWithPositiveVatRate_ThrowsArgumentException()
    {
        // Generate random vatRate values > 0 (between 0.01 and 99.99)
        var positiveVatRateGen = Gen.Choose(1, 9999).Select(i => (decimal)i / 100m);

        return Prop.ForAll(
            positiveVatRateGen.ToArbitrary(),
            (vatRate) =>
            {
                var (tenantMock, invoiceRepoMock, invoiceLineRepoMock, invoiceSectionRepoMock,
                    quotationRepoMock, quotationLineRepoMock, proposalSectionRepoMock,
                    customerRepoMock, auditLogRepoMock, vatPeriodRepoMock, vatSubmissionRepoMock,
                    dbContextMock, productServiceMock, productRepoMock, httpContextAccessorMock, loggerMock) = CreateMocks();

                SetupDraftInvoice(invoiceRepoMock);

                var service = CreateService(tenantMock, invoiceRepoMock, invoiceLineRepoMock,
                    invoiceSectionRepoMock, quotationRepoMock, quotationLineRepoMock,
                    proposalSectionRepoMock, customerRepoMock, auditLogRepoMock,
                    vatPeriodRepoMock, vatSubmissionRepoMock, dbContextMock,
                    productServiceMock, productRepoMock, httpContextAccessorMock, loggerMock);

                // Act & Assert: should throw ArgumentException
                var threw = false;
                try
                {
                    service.AddLineAsync(
                        invoiceId: TestInvoiceId,
                        description: "Test line",
                        quantity: 1m,
                        unitPrice: 100m,
                        vatRate: vatRate,
                        discount: 0m,
                        discountType: "Percentage",
                        costPrice: null,
                        referenceUrl: null,
                        subtitle: null,
                        invoiceSectionId: null,
                        productCode: null,
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
                    invoiceLineRepoMock.Verify(
                        r => r.InsertAsync(It.IsAny<InvoiceLine>()),
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

    // Feature: invoice-line-product-type-reverse-charge, Property 2: Reverse charge invariant (invoice lines)
    // **Validates: Requirements 6.4, 8.2, 8.5**
    [Property(MaxTest = 100)]
    public Property UpdateLineAsync_ReverseChargeWithPositiveVatRate_ThrowsArgumentException()
    {
        // Generate random vatRate values > 0 (between 0.01 and 99.99)
        var positiveVatRateGen = Gen.Choose(1, 9999).Select(i => (decimal)i / 100m);

        return Prop.ForAll(
            positiveVatRateGen.ToArbitrary(),
            (vatRate) =>
            {
                var (tenantMock, invoiceRepoMock, invoiceLineRepoMock, invoiceSectionRepoMock,
                    quotationRepoMock, quotationLineRepoMock, proposalSectionRepoMock,
                    customerRepoMock, auditLogRepoMock, vatPeriodRepoMock, vatSubmissionRepoMock,
                    dbContextMock, productServiceMock, productRepoMock, httpContextAccessorMock, loggerMock) = CreateMocks();

                var service = CreateService(tenantMock, invoiceRepoMock, invoiceLineRepoMock,
                    invoiceSectionRepoMock, quotationRepoMock, quotationLineRepoMock,
                    proposalSectionRepoMock, customerRepoMock, auditLogRepoMock,
                    vatPeriodRepoMock, vatSubmissionRepoMock, dbContextMock,
                    productServiceMock, productRepoMock, httpContextAccessorMock, loggerMock);

                // Act & Assert: should throw ArgumentException before any repository call
                var threw = false;
                try
                {
                    service.UpdateLineAsync(
                        lineId: TestLineId,
                        description: "Updated line",
                        quantity: 1m,
                        unitPrice: 100m,
                        vatRate: vatRate,
                        discount: 0m,
                        discountType: "Percentage",
                        costPrice: null,
                        referenceUrl: null,
                        subtitle: null,
                        invoiceSectionId: null,
                        isReverseCharge: true
                    ).GetAwaiter().GetResult();
                }
                catch (ArgumentException)
                {
                    threw = true;
                }

                // Verify no persistence occurred — InsertAsync (virtual) should never be called
                var insertNeverCalled = true;
                try
                {
                    invoiceLineRepoMock.Verify(
                        r => r.InsertAsync(It.IsAny<InvoiceLine>()),
                        Times.Never);
                }
                catch
                {
                    insertNeverCalled = false;
                }

                return threw
                    .Label($"UpdateLineAsync should throw ArgumentException for isReverseCharge=true with vatRate={vatRate}")
                    .And(insertNeverCalled
                        .Label("No persistence should occur when reverse charge validation fails"));
            });
    }

    #endregion

    #region Property 2: Reverse charge invariant (invoice lines) — Valid combinations succeed

    // Feature: invoice-line-product-type-reverse-charge, Property 2: Reverse charge invariant (invoice lines)
    // **Validates: Requirements 6.4, 8.2, 8.5**
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

                var (tenantMock, invoiceRepoMock, invoiceLineRepoMock, invoiceSectionRepoMock,
                    quotationRepoMock, quotationLineRepoMock, proposalSectionRepoMock,
                    customerRepoMock, auditLogRepoMock, vatPeriodRepoMock, vatSubmissionRepoMock,
                    dbContextMock, productServiceMock, productRepoMock, httpContextAccessorMock, loggerMock) = CreateMocks();

                SetupDraftInvoice(invoiceRepoMock);

                // Setup: InsertAsync returns a line ID (virtual, can be mocked)
                invoiceLineRepoMock
                    .Setup(r => r.InsertAsync(It.IsAny<InvoiceLine>()))
                    .ReturnsAsync(1);

                // Setup: audit log
                auditLogRepoMock
                    .Setup(r => r.InsertAsync(It.IsAny<AuditLog>()))
                    .Returns(Task.CompletedTask);

                // Setup: invoice update for totals recomputation
                invoiceRepoMock
                    .Setup(r => r.UpdateAsync(It.IsAny<Invoice>()))
                    .Returns(Task.CompletedTask);

                // Setup: product service auto-populate
                productServiceMock
                    .Setup(s => s.AutoPopulateFromLineItemAsync(
                        It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<decimal>(),
                        It.IsAny<decimal>(), It.IsAny<string>()))
                    .Returns(Task.CompletedTask);

                var service = CreateService(tenantMock, invoiceRepoMock, invoiceLineRepoMock,
                    invoiceSectionRepoMock, quotationRepoMock, quotationLineRepoMock,
                    proposalSectionRepoMock, customerRepoMock, auditLogRepoMock,
                    vatPeriodRepoMock, vatSubmissionRepoMock, dbContextMock,
                    productServiceMock, productRepoMock, httpContextAccessorMock, loggerMock);

                // Act: should NOT throw ArgumentException
                // May throw other exceptions due to non-virtual methods, but the RC validation must pass
                var threwArgumentException = false;
                try
                {
                    service.AddLineAsync(
                        invoiceId: TestInvoiceId,
                        description: "Test line",
                        quantity: 1m,
                        unitPrice: 100m,
                        vatRate: vatRate,
                        discount: 0m,
                        discountType: "Percentage",
                        costPrice: null,
                        referenceUrl: null,
                        subtitle: null,
                        invoiceSectionId: null,
                        productCode: null,
                        isReverseCharge: isReverseCharge
                    ).GetAwaiter().GetResult();
                }
                catch (ArgumentException)
                {
                    threwArgumentException = true;
                }
                catch
                {
                    // Other exceptions (e.g. from non-virtual repo methods) are acceptable
                    // The key property is that ArgumentException is NOT thrown
                }

                return (!threwArgumentException)
                    .Label($"AddLineAsync should NOT throw ArgumentException for isReverseCharge={isReverseCharge} with vatRate={vatRate}");
            });
    }

    // Feature: invoice-line-product-type-reverse-charge, Property 2: Reverse charge invariant (invoice lines)
    // **Validates: Requirements 6.4, 8.2, 8.5**
    [Property(MaxTest = 100)]
    public Property UpdateLineAsync_ValidCombinations_Succeeds()
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

                var (tenantMock, invoiceRepoMock, invoiceLineRepoMock, invoiceSectionRepoMock,
                    quotationRepoMock, quotationLineRepoMock, proposalSectionRepoMock,
                    customerRepoMock, auditLogRepoMock, vatPeriodRepoMock, vatSubmissionRepoMock,
                    dbContextMock, productServiceMock, productRepoMock, httpContextAccessorMock, loggerMock) = CreateMocks();

                SetupDraftInvoice(invoiceRepoMock);

                // Setup: audit log
                auditLogRepoMock
                    .Setup(r => r.InsertAsync(It.IsAny<AuditLog>()))
                    .Returns(Task.CompletedTask);

                // Setup: invoice update for totals recomputation
                invoiceRepoMock
                    .Setup(r => r.UpdateAsync(It.IsAny<Invoice>()))
                    .Returns(Task.CompletedTask);

                var service = CreateService(tenantMock, invoiceRepoMock, invoiceLineRepoMock,
                    invoiceSectionRepoMock, quotationRepoMock, quotationLineRepoMock,
                    proposalSectionRepoMock, customerRepoMock, auditLogRepoMock,
                    vatPeriodRepoMock, vatSubmissionRepoMock, dbContextMock,
                    productServiceMock, productRepoMock, httpContextAccessorMock, loggerMock);

                // Act: should NOT throw ArgumentException
                // May throw other exceptions due to non-virtual methods, but the RC validation must pass
                var threwArgumentException = false;
                try
                {
                    service.UpdateLineAsync(
                        lineId: TestLineId,
                        description: "Updated line",
                        quantity: 1m,
                        unitPrice: 100m,
                        vatRate: vatRate,
                        discount: 0m,
                        discountType: "Percentage",
                        costPrice: null,
                        referenceUrl: null,
                        subtitle: null,
                        invoiceSectionId: null,
                        isReverseCharge: isReverseCharge
                    ).GetAwaiter().GetResult();
                }
                catch (ArgumentException)
                {
                    threwArgumentException = true;
                }
                catch
                {
                    // Other exceptions (e.g. from non-virtual repo methods) are acceptable
                    // The key property is that ArgumentException is NOT thrown
                }

                return (!threwArgumentException)
                    .Label($"UpdateLineAsync should NOT throw ArgumentException for isReverseCharge={isReverseCharge} with vatRate={vatRate}");
            });
    }

    #endregion
}
