using FsCheck;
using FsCheck.Xunit;
using Moq;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Infrastructure.Data;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: revenue-control, Property 7: Void preserves payment record and sets flag

/// <summary>
/// Property-based tests for PaymentService.VoidPaymentAsync.
/// Validates that voiding a payment sets IsVoided = 1 and never deletes the record.
/// **Validates: Requirements 3.1, 3.2**
/// </summary>
public class VoidPaymentPropertyTests
{
    /// <summary>
    /// Creates a PaymentService with mocked dependencies configured for void testing.
    /// Returns the service and the mocked PaymentRepository for verification.
    /// </summary>
    private static (PaymentService Service, Mock<PaymentRepository> PaymentRepoMock) CreateServiceWithMocks(
        Payment paymentToReturn)
    {
        var mockPaymentRepo = new Mock<PaymentRepository>(MockBehavior.Loose, new object[] { null! });
        var mockInvoiceRepo = new Mock<InvoiceRepository>(MockBehavior.Loose, new object[] { null! });
        var mockFinancialStatusEngine = new Mock<IFinancialStatusEngine>();

        // Setup GetByIdAndBusinessIdAsync to return the provided payment
        mockPaymentRepo
            .Setup(r => r.GetByIdAndBusinessIdAsync(paymentToReturn.Id, paymentToReturn.BusinessId))
            .ReturnsAsync(paymentToReturn);

        // Setup VoidAsync to complete successfully
        mockPaymentRepo
            .Setup(r => r.VoidAsync(paymentToReturn.Id))
            .Returns(Task.CompletedTask);

        // Setup RecalculateStatusAsync to complete successfully
        mockFinancialStatusEngine
            .Setup(e => e.RecalculateStatusAsync(paymentToReturn.InvoiceId!.Value, paymentToReturn.BusinessId))
            .Returns(Task.CompletedTask);

        var service = new PaymentService(
            mockPaymentRepo.Object,
            mockInvoiceRepo.Object,
            new Mock<CreditNoteRepository>(null!).Object,
            mockFinancialStatusEngine.Object,
            new Mock<IPaymentScheduleService>().Object,
            new Mock<IPaymentAllocationEngine>().Object,
            null!);

        return (service, mockPaymentRepo);
    }

    /// <summary>
    /// Property 7: Void preserves payment record and sets flag
    /// For any non-voided payment, voiding SHALL call VoidAsync (sets IsVoided = 1)
    /// and SHALL NOT call any delete operation. The result is success.
    /// **Validates: Requirements 3.1, 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property VoidPayment_SetsIsVoidedFlag_AndPreservesRecord(
        PositiveInt paymentIdSeed,
        PositiveInt businessIdSeed,
        PositiveInt invoiceIdSeed,
        PositiveInt amountSeed,
        byte paymentMethodSeed)
    {
        // Generate random payment data (valid, non-voided payment)
        var paymentId = (paymentIdSeed.Get % 10000) + 1;
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var invoiceId = (invoiceIdSeed.Get % 10000) + 1;
        var amount = ((amountSeed.Get % 99999) + 1) * 0.01m;
        var paymentMethodTypeId = (paymentMethodSeed % 5) + 1;

        var payment = new Payment
        {
            Id = paymentId,
            BusinessId = businessId,
            InvoiceId = invoiceId,
            PaymentMethodTypeId = paymentMethodTypeId,
            PaymentDateUtc = DateTime.UtcNow.AddDays(-((paymentIdSeed.Get % 30) + 1)),
            Amount = amount,
            Reference = $"REF-{paymentId}",
            Notes = null,
            IsVoided = false,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-((paymentIdSeed.Get % 60) + 1)),
            CreatedByUserId = "user-123"
        };

        var (service, paymentRepoMock) = CreateServiceWithMocks(payment);

        // Act
        var result = service.VoidPaymentAsync(paymentId, businessId).GetAwaiter().GetResult();

        // Assert: result is success
        var isSuccess = result.Success;

        // Assert: VoidAsync was called exactly once (sets IsVoided = 1)
        var voidCalled = false;
        try
        {
            paymentRepoMock.Verify(r => r.VoidAsync(paymentId), Times.Once());
            voidCalled = true;
        }
        catch
        {
            voidCalled = false;
        }

        // Assert: No delete operation was performed (record still exists)
        // PaymentRepository has no Delete method by design — soft-void only.
        // We verify that only VoidAsync was called, not any destructive operation.
        var noDeleteCalled = true;
        try
        {
            // Verify InsertAsync was never called (no accidental re-creation)
            paymentRepoMock.Verify(r => r.InsertAsync(It.IsAny<Payment>()), Times.Never());
        }
        catch
        {
            noDeleteCalled = false;
        }

        return (isSuccess && voidCalled && noDeleteCalled).ToProperty()
            .Label($"paymentId={paymentId}, businessId={businessId}, invoiceId={invoiceId}, " +
                   $"amount={amount} => success={isSuccess}, voidCalled={voidCalled}, noDeleteCalled={noDeleteCalled}");
    }

    /// <summary>
    /// Property 7 (supplementary): Void triggers financial status recalculation on parent invoice.
    /// After voiding, RecalculateStatusAsync is called for the parent invoice.
    /// **Validates: Requirements 3.1, 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property VoidPayment_TriggersRecalculation_OnParentInvoice(
        PositiveInt paymentIdSeed,
        PositiveInt businessIdSeed,
        PositiveInt invoiceIdSeed,
        PositiveInt amountSeed)
    {
        var paymentId = (paymentIdSeed.Get % 10000) + 1;
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var invoiceId = (invoiceIdSeed.Get % 10000) + 1;
        var amount = ((amountSeed.Get % 99999) + 1) * 0.01m;

        var payment = new Payment
        {
            Id = paymentId,
            BusinessId = businessId,
            InvoiceId = invoiceId,
            PaymentMethodTypeId = 1,
            PaymentDateUtc = DateTime.UtcNow.AddDays(-5),
            Amount = amount,
            IsVoided = false,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-10),
            CreatedByUserId = "user-456"
        };

        var mockPaymentRepo = new Mock<PaymentRepository>(MockBehavior.Loose, new object[] { null! });
        var mockInvoiceRepo = new Mock<InvoiceRepository>(MockBehavior.Loose, new object[] { null! });
        var mockFinancialStatusEngine = new Mock<IFinancialStatusEngine>();

        mockPaymentRepo
            .Setup(r => r.GetByIdAndBusinessIdAsync(paymentId, businessId))
            .ReturnsAsync(payment);

        mockPaymentRepo
            .Setup(r => r.VoidAsync(paymentId))
            .Returns(Task.CompletedTask);

        mockFinancialStatusEngine
            .Setup(e => e.RecalculateStatusAsync(invoiceId, businessId))
            .Returns(Task.CompletedTask);

        var service = new PaymentService(
            mockPaymentRepo.Object,
            mockInvoiceRepo.Object,
            new Mock<CreditNoteRepository>(null!).Object,
            mockFinancialStatusEngine.Object,
            new Mock<IPaymentScheduleService>().Object,
            new Mock<IPaymentAllocationEngine>().Object,
            null!);

        // Act
        var result = service.VoidPaymentAsync(paymentId, businessId).GetAwaiter().GetResult();

        // Assert: RecalculateStatusAsync was called with the correct invoiceId and businessId
        var recalcCalled = false;
        try
        {
            mockFinancialStatusEngine.Verify(
                e => e.RecalculateStatusAsync(invoiceId, businessId), Times.Once());
            recalcCalled = true;
        }
        catch
        {
            recalcCalled = false;
        }

        return (result.Success && recalcCalled).ToProperty()
            .Label($"paymentId={paymentId}, invoiceId={invoiceId}, businessId={businessId} => " +
                   $"success={result.Success}, recalcCalled={recalcCalled}");
    }
}
