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
/// Property-based tests for Invoice VAT Period Reassignment logic.
/// Feature: invoice-vat-period-assignment
/// </summary>
public class InvoiceVatPeriodReassignmentPropertyTests
{
    private const int TestBusinessId = 1;

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
            httpContextAccessorMock.Object,
            loggerMock.Object);
    }

    /// <summary>
    /// Feature: invoice-vat-period-assignment, Property 6: Reassignment rejects submitted target periods
    /// **Validates: Requirements 3.6**
    ///
    /// For any reassignment request where the target VatSubmissionPeriod has a VatSubmission
    /// with IsSubmitted = true, the service SHALL reject the request and leave the invoice's
    /// VatSubmissionPeriodId unchanged.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Reassignment_Rejects_Submitted_Target_Periods()
    {
        var invoiceIdGen = Gen.Choose(1, 10000);
        var targetPeriodIdGen = Gen.Choose(1, 10000);
        var taxAmountGen = Gen.Choose(1, 9999999).Select(i => Math.Round((decimal)i / 100m, 2));

        return Prop.ForAll(
            invoiceIdGen.ToArbitrary(),
            targetPeriodIdGen.ToArbitrary(),
            taxAmountGen.ToArbitrary(),
            (invoiceId, targetPeriodId, taxAmount) =>
            {
                // Ensure current period differs from target to avoid "already assigned" rejection
                var effectiveCurrentPeriodId = (int?)(targetPeriodId + 1);

                var (tenantMock, invoiceRepoMock, invoiceLineRepoMock, invoiceSectionRepoMock,
                    quotationRepoMock, quotationLineRepoMock, proposalSectionRepoMock,
                    customerRepoMock, auditLogRepoMock, vatPeriodRepoMock, vatSubmissionRepoMock, dbContextMock) = CreateMocks();

                // Setup: invoice exists and is not deleted
                var invoice = new Invoice
                {
                    Id = invoiceId,
                    BusinessId = TestBusinessId,
                    CustomerId = 1,
                    InvoiceStatusTypeId = 2,
                    InvoiceFinancialStatusTypeId = 1,
                    InvoiceNumber = $"INV-{TestBusinessId}-{invoiceId:D5}",
                    InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
                    DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
                    Subtotal = taxAmount * 5,
                    TaxAmount = taxAmount,
                    TotalAmount = taxAmount * 6,
                    CurrencyCode = "EUR",
                    IsDeleted = false,
                    VatSubmissionPeriodId = effectiveCurrentPeriodId,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                invoiceRepoMock
                    .Setup(r => r.GetByIdAndBusinessIdAsync(invoiceId, TestBusinessId))
                    .ReturnsAsync(invoice);

                // Setup: target period exists and belongs to same business
                var targetPeriod = new VatSubmissionPeriod
                {
                    Id = targetPeriodId,
                    BusinessId = TestBusinessId,
                    PeriodStartDate = new DateOnly(2024, 3, 1),
                    PeriodEndDate = new DateOnly(2024, 5, 31),
                    PeriodLabel = "Mar-May 2024",
                    CreatedAtUtc = DateTime.UtcNow
                };

                vatPeriodRepoMock
                    .Setup(r => r.GetByIdAndBusinessIdAsync(targetPeriodId, TestBusinessId))
                    .ReturnsAsync(targetPeriod);

                // Setup: target period has a submitted VatSubmission (IsSubmitted = true)
                var submission = new VatSubmission
                {
                    Id = 1,
                    BusinessId = TestBusinessId,
                    VatSubmissionPeriodId = targetPeriodId,
                    TotalOutputVat = 5000m,
                    TotalInputVat = 2000m,
                    NetVatPayable = 3000m,
                    IsSubmitted = true,
                    SubmittedAtUtc = DateTime.UtcNow.AddDays(-1),
                    CreatedAtUtc = DateTime.UtcNow
                };

                vatSubmissionRepoMock
                    .Setup(r => r.GetByPeriodIdAndBusinessIdAsync(targetPeriodId, TestBusinessId))
                    .ReturnsAsync(submission);

                // Setup: UpdateVatPeriodAsync (should NOT be called)
                invoiceRepoMock
                    .Setup(r => r.UpdateVatPeriodAsync(It.IsAny<int>(), It.IsAny<int?>()))
                    .Returns(Task.CompletedTask);

                // Setup: audit log (should NOT be called)
                auditLogRepoMock
                    .Setup(r => r.InsertAsync(It.IsAny<AuditLog>()))
                    .Returns(Task.CompletedTask);

                var service = CreateService(tenantMock, invoiceRepoMock, invoiceLineRepoMock,
                    invoiceSectionRepoMock, quotationRepoMock, quotationLineRepoMock,
                    proposalSectionRepoMock, customerRepoMock, auditLogRepoMock,
                    vatPeriodRepoMock, vatSubmissionRepoMock, dbContextMock);

                // Act
                var result = service.ReassignVatPeriodAsync(invoiceId, targetPeriodId).GetAwaiter().GetResult();

                // Assert: request is rejected
                var isRejected = !result.Success;
                var hasCorrectMessage = result.Message != null &&
                    result.Message.Contains("already been submitted");

                // Assert: UpdateVatPeriodAsync was never called (invoice unchanged)
                var updateNeverCalled = true;
                try
                {
                    invoiceRepoMock.Verify(
                        r => r.UpdateVatPeriodAsync(It.IsAny<int>(), It.IsAny<int?>()),
                        Times.Never);
                }
                catch
                {
                    updateNeverCalled = false;
                }

                return isRejected
                    .Label("Reassignment should be rejected for submitted target period")
                    .And(hasCorrectMessage
                        .Label($"Error message should mention 'already been submitted' but was: '{result.Message}'"))
                    .And(updateNeverCalled
                        .Label("UpdateVatPeriodAsync should never be called when target period is submitted"));
            });
    }

    /// <summary>
    /// Feature: invoice-vat-period-assignment, Property 8: Successful reassignment updates period and timestamp
    /// **Validates: Requirements 3.9**
    ///
    /// For any valid reassignment request (all validations pass), the invoice's VatSubmissionPeriodId
    /// SHALL equal the target period's Id and UpdatedAtUtc SHALL be greater than or equal to the time
    /// the request was initiated.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SuccessfulReassignment_UpdatesPeriodAndTimestamp()
    {
        // Generate random invoice IDs (positive)
        var invoiceIdGen = Gen.Choose(1, 10000);
        // Generate random target period IDs (positive, different from source)
        var targetPeriodIdGen = Gen.Choose(1, 10000);
        // Generate random source period IDs (positive, will be different from target)
        var sourcePeriodIdGen = Gen.Choose(1, 10000);

        return Prop.ForAll(
            invoiceIdGen.ToArbitrary(),
            targetPeriodIdGen.ToArbitrary(),
            sourcePeriodIdGen.ToArbitrary(),
            (invoiceId, targetPeriodId, sourcePeriodId) =>
            {
                // Ensure source and target are different
                if (sourcePeriodId == targetPeriodId)
                    targetPeriodId = sourcePeriodId + 1;

                var (tenantMock, invoiceRepoMock, invoiceLineRepoMock, invoiceSectionRepoMock,
                    quotationRepoMock, quotationLineRepoMock, proposalSectionRepoMock,
                    customerRepoMock, auditLogRepoMock, vatPeriodRepoMock, vatSubmissionRepoMock, dbContextMock) = CreateMocks();

                // Set up a valid scenario: invoice exists, not deleted, target period exists,
                // same business, not submitted, not already assigned to target
                var invoice = new Invoice
                {
                    Id = invoiceId,
                    BusinessId = TestBusinessId,
                    InvoiceNumber = $"INV-{TestBusinessId}-{invoiceId:D5}",
                    InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
                    DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
                    InvoiceStatusTypeId = 2,
                    InvoiceFinancialStatusTypeId = 1,
                    Subtotal = 100m,
                    TaxAmount = 15m,
                    TotalAmount = 115m,
                    CurrencyCode = "EUR",
                    IsDeleted = false,
                    VatSubmissionPeriodId = sourcePeriodId,
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-10),
                    UpdatedAtUtc = DateTime.UtcNow.AddDays(-5)
                };

                var targetPeriod = new VatSubmissionPeriod
                {
                    Id = targetPeriodId,
                    BusinessId = TestBusinessId,
                    PeriodStartDate = DateOnly.FromDateTime(DateTime.Today),
                    PeriodEndDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(2)),
                    PeriodLabel = "Test Period",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
                };

                // Mock: invoice exists and belongs to business
                invoiceRepoMock
                    .Setup(r => r.GetByIdAndBusinessIdAsync(invoiceId, TestBusinessId))
                    .ReturnsAsync(invoice);

                // Mock: target period exists and belongs to same business
                vatPeriodRepoMock
                    .Setup(r => r.GetByIdAndBusinessIdAsync(targetPeriodId, TestBusinessId))
                    .ReturnsAsync(targetPeriod);

                // Mock: target period is NOT submitted (no submission record)
                vatSubmissionRepoMock
                    .Setup(r => r.GetByPeriodIdAndBusinessIdAsync(targetPeriodId, TestBusinessId))
                    .ReturnsAsync((VatSubmission?)null);

                // Mock: UpdateVatPeriodAsync succeeds
                invoiceRepoMock
                    .Setup(r => r.UpdateVatPeriodAsync(It.IsAny<int>(), It.IsAny<int?>()))
                    .Returns(Task.CompletedTask);

                // Mock: audit log insert succeeds
                auditLogRepoMock
                    .Setup(r => r.InsertAsync(It.IsAny<AuditLog>()))
                    .Returns(Task.CompletedTask);

                var service = CreateService(tenantMock, invoiceRepoMock, invoiceLineRepoMock,
                    invoiceSectionRepoMock, quotationRepoMock, quotationLineRepoMock,
                    proposalSectionRepoMock, customerRepoMock, auditLogRepoMock,
                    vatPeriodRepoMock, vatSubmissionRepoMock, dbContextMock);

                // Record time before the call
                var timeBefore = DateTime.UtcNow;

                var result = service.ReassignVatPeriodAsync(invoiceId, targetPeriodId).GetAwaiter().GetResult();

                // Verify: result is successful
                var successProperty = result.Success
                    .Label($"Expected success but got failure: {result.Message}");

                // Verify: UpdateVatPeriodAsync was called with correct invoiceId and targetPeriodId
                var updateCalledProperty = true;
                try
                {
                    invoiceRepoMock.Verify(
                        r => r.UpdateVatPeriodAsync(invoiceId, targetPeriodId),
                        Times.Once);
                }
                catch
                {
                    updateCalledProperty = false;
                }

                var updateProperty = updateCalledProperty
                    .Label($"UpdateVatPeriodAsync should be called with invoiceId={invoiceId}, targetPeriodId={targetPeriodId}");

                // Verify: audit log was written with correct action and timestamp >= timeBefore
                var auditCalledProperty = true;
                try
                {
                    auditLogRepoMock.Verify(
                        r => r.InsertAsync(It.Is<AuditLog>(a =>
                            a.Action == "VatPeriodReassigned" &&
                            a.TableName == "Invoice" &&
                            a.RecordId == invoiceId.ToString() &&
                            a.NewValues == $"VatSubmissionPeriodId={targetPeriodId}" &&
                            a.Timestamp >= timeBefore)),
                        Times.Once);
                }
                catch
                {
                    auditCalledProperty = false;
                }

                var auditProperty = auditCalledProperty
                    .Label("Audit log should be written with VatPeriodReassigned action and correct timestamp");

                return successProperty
                    .And(updateProperty)
                    .And(auditProperty);
            });
    }

    /// <summary>
    /// Feature: invoice-vat-period-assignment, Property 11: Projected impact is arithmetic over current totals
    /// **Validates: Requirements 4.4**
    ///
    /// For any reassignment impact computation:
    ///   source projected = source current TotalOutputVat - invoice.TaxAmount
    ///   target projected = target current TotalOutputVat + invoice.TaxAmount
    ///
    /// Test scenario:
    /// - Invoice with VatSubmissionPeriodId = sourcePeriodId
    /// - Source VatSubmission with known TotalOutputVat
    /// - Target VatSubmission with known TotalOutputVat
    /// - Call GetReassignmentImpactAsync → verify arithmetic
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProjectedImpact_IsArithmeticOverCurrentTotals()
    {
        var scenarioGen =
            from taxAmount in Gen.Choose(1, 9999999).Select(i => Math.Round((decimal)i / 100m, 2))
            from sourceTotal in Gen.Choose(0, 9999999).Select(i => Math.Round((decimal)i / 100m, 2))
            from targetTotal in Gen.Choose(0, 9999999).Select(i => Math.Round((decimal)i / 100m, 2))
            from invoiceId in Gen.Choose(1, 10000)
            from sourcePeriodId in Gen.Choose(1, 5000)
            from targetPeriodOffset in Gen.Choose(1, 5000)
            select (taxAmount, sourceTotal, targetTotal, invoiceId, sourcePeriodId, targetPeriodOffset);

        return Prop.ForAll(
            scenarioGen.ToArbitrary(),
            scenario =>
            {
                var (taxAmount, sourceTotal, targetTotal, invoiceId, sourcePeriodId, targetPeriodOffset) = scenario;
                var targetPeriodId = sourcePeriodId + targetPeriodOffset; // guaranteed distinct

                var (tenantMock, invoiceRepoMock, invoiceLineRepoMock, invoiceSectionRepoMock,
                    quotationRepoMock, quotationLineRepoMock, proposalSectionRepoMock,
                    customerRepoMock, auditLogRepoMock, vatPeriodRepoMock, vatSubmissionRepoMock, dbContextMock) = CreateMocks();

                // Seed a BusinessProfile into the in-memory context so the currency symbol lookup works
                var dbContextOptions = new DbContextOptionsBuilder<PortalDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                    .Options;
                var realDbContext = new PortalDbContext(dbContextOptions, tenantMock.Object);
                realDbContext.BusinessProfiles.Add(new BusinessProfile
                {
                    Id = 1,
                    BusinessId = TestBusinessId,
                    CompanyRegistrationNumber = "CRN-001",
                    VatRegistrationNumber = "VAT-001",
                    VatRegistrationDate = new DateOnly(2020, 1, 1),
                    VatPeriodLengthInMonths = 3,
                    AddressLine1 = "1 Test Street",
                    City = "Dublin",
                    PostalCode = "D01",
                    Country = "Ireland",
                    Email = "test@test.com",
                    CurrencySymbol = "€"
                });
                realDbContext.SaveChanges();

                // Invoice assigned to sourcePeriod
                var invoice = new Invoice
                {
                    Id = invoiceId,
                    BusinessId = TestBusinessId,
                    CustomerId = 1,
                    InvoiceStatusTypeId = 2,
                    InvoiceFinancialStatusTypeId = 1,
                    InvoiceNumber = $"INV-{invoiceId:D5}",
                    InvoiceDate = new DateOnly(2024, 4, 15),
                    DueDate = new DateOnly(2024, 5, 15),
                    Subtotal = taxAmount * 5m,
                    TaxAmount = taxAmount,
                    TotalAmount = taxAmount * 6m,
                    CurrencyCode = "EUR",
                    IsDeleted = false,
                    VatSubmissionPeriodId = sourcePeriodId,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                invoiceRepoMock
                    .Setup(r => r.GetByIdAndBusinessIdAsync(invoiceId, TestBusinessId))
                    .ReturnsAsync(invoice);

                // Source period
                var sourcePeriod = new VatSubmissionPeriod
                {
                    Id = sourcePeriodId,
                    BusinessId = TestBusinessId,
                    PeriodStartDate = new DateOnly(2024, 4, 1),
                    PeriodEndDate = new DateOnly(2024, 6, 30),
                    PeriodLabel = "Apr-Jun 2024",
                    CreatedAtUtc = DateTime.UtcNow
                };

                // Target period
                var targetPeriod = new VatSubmissionPeriod
                {
                    Id = targetPeriodId,
                    BusinessId = TestBusinessId,
                    PeriodStartDate = new DateOnly(2024, 7, 1),
                    PeriodEndDate = new DateOnly(2024, 9, 30),
                    PeriodLabel = "Jul-Sep 2024",
                    CreatedAtUtc = DateTime.UtcNow
                };

                vatPeriodRepoMock
                    .Setup(r => r.GetByIdAndBusinessIdAsync(sourcePeriodId, TestBusinessId))
                    .ReturnsAsync(sourcePeriod);

                vatPeriodRepoMock
                    .Setup(r => r.GetByIdAndBusinessIdAsync(targetPeriodId, TestBusinessId))
                    .ReturnsAsync(targetPeriod);

                // Source submission with known TotalOutputVat
                var sourceSubmission = new VatSubmission
                {
                    Id = 1,
                    BusinessId = TestBusinessId,
                    VatSubmissionPeriodId = sourcePeriodId,
                    TotalOutputVat = sourceTotal,
                    TotalInputVat = 0m,
                    NetVatPayable = sourceTotal,
                    IsSubmitted = false,
                    CreatedAtUtc = DateTime.UtcNow
                };

                // Target submission with known TotalOutputVat
                var targetSubmission = new VatSubmission
                {
                    Id = 2,
                    BusinessId = TestBusinessId,
                    VatSubmissionPeriodId = targetPeriodId,
                    TotalOutputVat = targetTotal,
                    TotalInputVat = 0m,
                    NetVatPayable = targetTotal,
                    IsSubmitted = false,
                    CreatedAtUtc = DateTime.UtcNow
                };

                vatSubmissionRepoMock
                    .Setup(r => r.GetByPeriodIdAndBusinessIdAsync(sourcePeriodId, TestBusinessId))
                    .ReturnsAsync(sourceSubmission);

                vatSubmissionRepoMock
                    .Setup(r => r.GetByPeriodIdAndBusinessIdAsync(targetPeriodId, TestBusinessId))
                    .ReturnsAsync(targetSubmission);

                // Build service using the real dbContext (for BusinessProfiles query)
                var productServiceMock2 = new Mock<IProductService>();
                var productRepositoryMock2 = new Mock<ProductRepository>(realDbContext);
                var httpContextAccessorMock2 = new Mock<IHttpContextAccessor>();
                var loggerMock2 = new Mock<ILogger<InvoiceService>>();

                var service = new InvoiceService(
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
                    realDbContext,
                    productServiceMock2.Object,
                    productRepositoryMock2.Object,
                    httpContextAccessorMock2.Object,
                    loggerMock2.Object);

                // Act
                var result = service.GetReassignmentImpactAsync(invoiceId, targetPeriodId).GetAwaiter().GetResult();

                var succeeded = result.Success;
                var impact = result.Data;

                // source projected = sourceTotal - taxAmount
                var expectedSourceProjected = sourceTotal - taxAmount;
                var sourceProjectedCorrect = impact != null
                    && impact.SourcePeriodProjectedOutputVat == expectedSourceProjected;

                // target projected = targetTotal + taxAmount
                var expectedTargetProjected = targetTotal + taxAmount;
                var targetProjectedCorrect = impact != null
                    && impact.TargetPeriodProjectedOutputVat == expectedTargetProjected;

                // TaxAmount in the DTO must match the invoice's TaxAmount
                var taxAmountCorrect = impact != null && impact.TaxAmount == taxAmount;

                return succeeded
                    .Label($"GetReassignmentImpactAsync should succeed but returned: {result.Message}")
                    .And(sourceProjectedCorrect
                        .Label($"SourcePeriodProjectedOutputVat should be {expectedSourceProjected} ({sourceTotal} - {taxAmount}) but was {impact?.SourcePeriodProjectedOutputVat}"))
                    .And(targetProjectedCorrect
                        .Label($"TargetPeriodProjectedOutputVat should be {expectedTargetProjected} ({targetTotal} + {taxAmount}) but was {impact?.TargetPeriodProjectedOutputVat}"))
                    .And(taxAmountCorrect
                        .Label($"TaxAmount in DTO should be {taxAmount} but was {impact?.TaxAmount}"));
            });
    }
}
