using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Portal.Tests.Unit.Properties;

/// <summary>
/// Property-based tests for InvoiceService VAT period assignment logic.
/// Feature: invoice-vat-period-assignment
/// </summary>
public class InvoiceVatPeriodAssignmentPropertyTests
{
    private const int TestBusinessId = 1;
    private const int TestCustomerId = 5;

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
        Mock<PortalDbContext> dbContextMock
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

        // Default setup: customer exists
        customerRepoMock
            .Setup(r => r.GetByIdAndBusinessIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new Customer { Id = TestCustomerId, BusinessId = TestBusinessId, Name = "Test Customer", IsActive = true });

        // Default setup: invoice insert returns an Id
        invoiceRepoMock
            .Setup(r => r.InsertAsync(It.IsAny<Invoice>()))
            .ReturnsAsync(1);

        // Default setup: invoice update succeeds
        invoiceRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Invoice>()))
            .Returns(Task.CompletedTask);

        // Default setup: get next sequential number
        invoiceRepoMock
            .Setup(r => r.GetNextSequentialNumberAsync(It.IsAny<int>()))
            .ReturnsAsync(1);

        // Default setup: invoice line insert returns an Id
        invoiceLineRepoMock
            .Setup(r => r.InsertAsync(It.IsAny<InvoiceLine>()))
            .ReturnsAsync(1);

        // Default setup: audit log insert succeeds
        auditLogRepoMock
            .Setup(r => r.InsertAsync(It.IsAny<AuditLog>()))
            .Returns(Task.CompletedTask);

        return (tenantMock, invoiceRepoMock, invoiceLineRepoMock, invoiceSectionRepoMock,
                quotationRepoMock, quotationLineRepoMock, proposalSectionRepoMock,
                customerRepoMock, auditLogRepoMock, vatPeriodRepoMock, vatSubmissionRepoMock, dbContextMock);
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
        Mock<PortalDbContext> dbContextMock)
    {
        var productServiceMock = new Mock<IProductService>();
        var productRepositoryMock = new Mock<ProductRepository>(dbContextMock.Object);
        var productPriceTierRepoMock = new Mock<ProductPriceTierRepository>(dbContextMock.Object) { CallBase = false };
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var loggerMock = new Mock<ILogger<InvoiceService>>();

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
            productRepositoryMock.Object,
            productPriceTierRepoMock.Object,
            httpContextAccessorMock.Object,
            loggerMock.Object);
    }

    /// <summary>
    /// Feature: invoice-vat-period-assignment, Property 4: Auto-assignment selects natural unsubmitted period
    /// **Validates: Requirements 2.1, 2.2**
    ///
    /// For any newly created invoice whose InvoiceDate falls within a period's date range and that
    /// period either has no VatSubmission record or has one with IsSubmitted = false, the
    /// VatSubmissionPeriodId SHALL be set to that period's Id.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AutoAssignment_Selects_Natural_Unsubmitted_Period()
    {
        // Combine generators into a single tuple: (periodId, dayOffset, periodDuration, hasSubmission)
        var scenarioGen = from periodId in Gen.Choose(1, 1000)
                          from dayOffset in Gen.Choose(0, 1095)
                          from periodDuration in Gen.Choose(28, 92)
                          from hasSubmission in Gen.Elements(true, false)
                          select (periodId, dayOffset, periodDuration, hasSubmission);

        return Prop.ForAll(
            scenarioGen.ToArbitrary(),
            scenario =>
            {
                var (periodId, dayOffset, periodDuration, hasSubmission) = scenario;
                var invoiceDate = DateOnly.FromDateTime(new DateTime(2023, 1, 1).AddDays(dayOffset));

                var (tenantMock, invoiceRepoMock, invoiceLineRepoMock, invoiceSectionRepoMock,
                    quotationRepoMock, quotationLineRepoMock, proposalSectionRepoMock,
                    customerRepoMock, auditLogRepoMock, vatPeriodRepoMock, vatSubmissionRepoMock, dbContextMock) = CreateMocks();

                // Set up a period that contains the invoice date
                var periodStart = invoiceDate.AddDays(-10);
                var periodEnd = periodStart.AddDays(periodDuration);

                var naturalPeriod = new VatSubmissionPeriod
                {
                    Id = periodId,
                    BusinessId = TestBusinessId,
                    PeriodStartDate = periodStart,
                    PeriodEndDate = periodEnd,
                    PeriodLabel = $"Period {periodId}",
                    CreatedAtUtc = DateTime.UtcNow
                };

                // Mock: GetByDateAndBusinessIdAsync returns the natural period
                vatPeriodRepoMock
                    .Setup(r => r.GetByDateAndBusinessIdAsync(invoiceDate, TestBusinessId))
                    .ReturnsAsync(naturalPeriod);

                // Mock: VatSubmission for this period — either no submission or unsubmitted
                if (hasSubmission)
                {
                    // Submission exists but IsSubmitted = false
                    vatSubmissionRepoMock
                        .Setup(r => r.GetByPeriodIdAndBusinessIdAsync(periodId, TestBusinessId))
                        .ReturnsAsync(new VatSubmission
                        {
                            Id = 1,
                            BusinessId = TestBusinessId,
                            VatSubmissionPeriodId = periodId,
                            IsSubmitted = false,
                            TotalOutputVat = 0,
                            TotalInputVat = 0,
                            NetVatPayable = 0,
                            CreatedAtUtc = DateTime.UtcNow
                        });
                }
                else
                {
                    // No submission record exists
                    vatSubmissionRepoMock
                        .Setup(r => r.GetByPeriodIdAndBusinessIdAsync(periodId, TestBusinessId))
                        .ReturnsAsync((VatSubmission?)null);
                }

                // Capture the invoice passed to InsertAsync to verify VatSubmissionPeriodId
                Invoice? capturedInvoice = null;
                invoiceRepoMock
                    .Setup(r => r.InsertAsync(It.IsAny<Invoice>()))
                    .Callback<Invoice>(inv => capturedInvoice = inv)
                    .ReturnsAsync(1);

                var service = CreateService(tenantMock, invoiceRepoMock, invoiceLineRepoMock,
                    invoiceSectionRepoMock, quotationRepoMock, quotationLineRepoMock,
                    proposalSectionRepoMock, customerRepoMock, auditLogRepoMock,
                    vatPeriodRepoMock, vatSubmissionRepoMock, dbContextMock);

                // Create a line item for the invoice
                var lines = new List<CreateInvoiceLineDto>
                {
                    new CreateInvoiceLineDto
                    {
                        Description = "Test item",
                        Quantity = 1,
                        UnitPrice = 100m,
                        VatRate = 21m,
                        Discount = 0,
                        DiscountType = "Fixed",
                        CostPrice = null,
                        ReferenceUrl = null,
                        Subtitle = null,
                        SectionIndex = null
                    }
                };

                // Act: Create the invoice
                service.CreateInvoiceAsync(
                    TestCustomerId, invoiceDate, invoiceDate.AddDays(30),
                    null, true, lines, null).GetAwaiter().GetResult();

                // Assert: The invoice's VatSubmissionPeriodId should be set to the natural period's Id
                return (capturedInvoice != null)
                    .Label("Invoice was captured")
                    .And((capturedInvoice!.VatSubmissionPeriodId == periodId)
                        .Label($"VatSubmissionPeriodId should be {periodId} but was {capturedInvoice.VatSubmissionPeriodId}"));
            });
    }
}
