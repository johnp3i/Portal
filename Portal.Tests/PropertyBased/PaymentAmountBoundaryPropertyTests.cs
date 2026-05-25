using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Infrastructure.Data;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: revenue-control, Property 2: Payment amount boundary validation

/// <summary>
/// Property-based tests for PaymentService.RecordPaymentAsync amount boundary validation.
/// Validates that payments with amount ≤ 0 or amount > OutstandingBalance are rejected,
/// and no Payment record is created in rejection cases.
/// **Validates: Requirements 1.3, 1.4**
/// </summary>
public class PaymentAmountBoundaryPropertyTests
{
    private const int InvoiceStatusIssued = 2;
    private const int TestBusinessId = 1;
    private const int TestInvoiceId = 100;
    private const string TestUserId = "test-user-id";

    /// <summary>
    /// Creates a PaymentService with mocked dependencies configured for amount validation testing.
    /// The invoice is always valid (Issued status) so we isolate amount boundary checks.
    /// </summary>
    private static (PaymentService Service, Mock<PaymentRepository> PaymentRepoMock) CreateServiceWithMocks(
        decimal invoiceTotalAmount, decimal totalAlreadyPaid)
    {
        var mockInvoiceRepo = new Mock<InvoiceRepository>(MockBehavior.Strict, new object[] { null! });
        mockInvoiceRepo
            .Setup(r => r.GetByIdAndBusinessIdAsync(TestInvoiceId, TestBusinessId))
            .ReturnsAsync(new Invoice
            {
                Id = TestInvoiceId,
                BusinessId = TestBusinessId,
                InvoiceStatusTypeId = InvoiceStatusIssued,
                TotalAmount = invoiceTotalAmount,
                InvoiceNumber = "INV-00001",
                CurrencyCode = "EUR",
                InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow),
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                CustomerId = 1,
                InvoiceFinancialStatusTypeId = 1,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });

        var mockPaymentRepo = new Mock<PaymentRepository>(MockBehavior.Strict, new object[] { null! });
        mockPaymentRepo
            .Setup(r => r.GetTotalPaidAsync(TestInvoiceId, TestBusinessId))
            .ReturnsAsync(totalAlreadyPaid);

        // InsertAsync should never be called in rejection cases
        mockPaymentRepo
            .Setup(r => r.InsertAsync(It.IsAny<Payment>()))
            .ReturnsAsync(1);

        var mockFinancialStatusEngine = new Mock<IFinancialStatusEngine>();
        mockFinancialStatusEngine
            .Setup(e => e.RecalculateStatusAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        // Create an in-memory DbContext to support BusinessProfiles query in GetCurrencySymbolAsync
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"PaymentAmountTest_{Guid.NewGuid()}")
            .Options;

        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

        var dbContext = new PortalDbContext(options, mockTenantService.Object);

        // Seed a BusinessProfile so GetCurrencySymbolAsync can resolve the currency symbol
        dbContext.BusinessProfiles.Add(new BusinessProfile
        {
            Id = 1,
            BusinessId = TestBusinessId,
            CurrencySymbol = "€",
            CompanyRegistrationNumber = "TEST123",
            VatRegistrationNumber = "VAT123",
            VatRegistrationDate = DateOnly.FromDateTime(DateTime.UtcNow),
            VatPeriodLengthInMonths = 2,
            AddressLine1 = "Test Address",
            City = "Test City",
            PostalCode = "12345",
            Country = "Test Country",
            Email = "test@test.com"
        });
        dbContext.SaveChanges();

        var service = new PaymentService(
            mockPaymentRepo.Object,
            mockInvoiceRepo.Object,
            mockFinancialStatusEngine.Object,
            dbContext);

        return (service, mockPaymentRepo);
    }

    #region Property 2a: Amounts ≤ 0 are rejected

    /// <summary>
    /// Property 2a: For any payment amount that is less than or equal to zero,
    /// the PaymentService SHALL reject the payment with "Payment amount must be greater than zero."
    /// and no Payment record SHALL be created.
    /// **Validates: Requirement 1.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NegativeOrZeroAmount_IsRejected(NegativeInt amountSeed)
    {
        // Generate amounts ≤ 0: use negative integers and zero
        var amount = amountSeed.Get * 1.00m; // Will be negative

        var (service, paymentRepoMock) = CreateServiceWithMocks(
            invoiceTotalAmount: 1000m,
            totalAlreadyPaid: 0m);

        var dto = new RecordPaymentDto
        {
            InvoiceId = TestInvoiceId,
            PaymentMethodTypeId = 1,
            PaymentDateUtc = DateTime.UtcNow,
            Amount = amount
        };

        var result = service.RecordPaymentAsync(dto, TestBusinessId, TestUserId).GetAwaiter().GetResult();

        var isRejected = !result.Success;
        var hasCorrectMessage = result.Message == "Payment amount must be greater than zero.";
        paymentRepoMock.Verify(r => r.InsertAsync(It.IsAny<Payment>()), Times.Never());

        return (isRejected && hasCorrectMessage).ToProperty()
            .Label($"Amount={amount}: Success={result.Success}, Message='{result.Message}'");
    }

    /// <summary>
    /// Property 2a (zero case): Amount exactly zero is rejected.
    /// **Validates: Requirement 1.3**
    /// </summary>
    [Fact]
    public async Task ZeroAmount_IsRejected()
    {
        var (service, paymentRepoMock) = CreateServiceWithMocks(
            invoiceTotalAmount: 1000m,
            totalAlreadyPaid: 0m);

        var dto = new RecordPaymentDto
        {
            InvoiceId = TestInvoiceId,
            PaymentMethodTypeId = 1,
            PaymentDateUtc = DateTime.UtcNow,
            Amount = 0m
        };

        var result = await service.RecordPaymentAsync(dto, TestBusinessId, TestUserId);

        Assert.False(result.Success);
        Assert.Equal("Payment amount must be greater than zero.", result.Message);
        paymentRepoMock.Verify(r => r.InsertAsync(It.IsAny<Payment>()), Times.Never());
    }

    #endregion

    #region Property 2b: Amounts exceeding outstanding balance are rejected

    /// <summary>
    /// Property 2b: For any payment amount that exceeds the invoice's Outstanding_Balance,
    /// the PaymentService SHALL reject the payment with "Amount exceeds outstanding balance..."
    /// and no Payment record SHALL be created.
    /// **Validates: Requirement 1.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AmountExceedingOutstandingBalance_IsRejected(
        PositiveInt totalAmountSeed,
        PositiveInt paidSeed,
        PositiveInt excessSeed)
    {
        // Generate a valid invoice total (between 100 and 100000)
        var invoiceTotal = (totalAmountSeed.Get % 99901 + 100) * 1.00m;

        // Generate total already paid (between 0 and invoiceTotal - 1, ensuring outstanding > 0)
        var totalPaid = (paidSeed.Get % (int)invoiceTotal) * 1.00m;

        var outstandingBalance = invoiceTotal - totalPaid;

        // Generate an amount that exceeds outstanding balance (outstanding + 1 to outstanding + 10000)
        var excess = (excessSeed.Get % 10000 + 1) * 0.01m;
        var paymentAmount = outstandingBalance + excess;

        var (service, paymentRepoMock) = CreateServiceWithMocks(
            invoiceTotalAmount: invoiceTotal,
            totalAlreadyPaid: totalPaid);

        var dto = new RecordPaymentDto
        {
            InvoiceId = TestInvoiceId,
            PaymentMethodTypeId = 1,
            PaymentDateUtc = DateTime.UtcNow,
            Amount = paymentAmount
        };

        var result = service.RecordPaymentAsync(dto, TestBusinessId, TestUserId).GetAwaiter().GetResult();

        var isRejected = !result.Success;
        var hasCorrectMessagePrefix = result.Message != null &&
            result.Message.StartsWith("Amount exceeds outstanding balance of");
        paymentRepoMock.Verify(r => r.InsertAsync(It.IsAny<Payment>()), Times.Never());

        return (isRejected && hasCorrectMessagePrefix).ToProperty()
            .Label($"InvoiceTotal={invoiceTotal}, Paid={totalPaid}, Outstanding={outstandingBalance}, " +
                   $"PaymentAmount={paymentAmount}: Success={result.Success}, Message='{result.Message}'");
    }

    #endregion
}
