using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Entities.Billing;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Configuration;
using Portal.Web.Models.Stripe;
using Portal.Web.Services;
using Portal.Web.Services.Billing;
using Portal.Web.Services.Stripe;
using Xunit;

namespace Portal.Tests.Unit.Services;

/// <summary>
/// Unit tests for BillingService.GenerateInvoicePdfAsync verifying PDF model construction logic.
/// Since PuppeteerSharp cannot run in unit tests, we capture the model passed to IViewRenderService
/// via Moq callback to verify invoice number usage, issuer fields, VAT, and reverse charge.
/// Validates Requirements 4.1, 4.2, 4.3, 5.1, 9.7.
/// </summary>
public class BillingServiceInvoicePdfTests
{
    private const int TestBusinessId = 1;
    private const int TestInvoiceId = 42;
    private const string TestInvoiceNumber = "BILI-INV-2026-0001";

    private readonly Mock<SubscriptionRepository> _subscriptionRepoMock;
    private readonly Mock<BillingInvoiceRepository> _billingInvoiceRepoMock;
    private readonly Mock<BillingPaymentRepository> _billingPaymentRepoMock;
    private readonly Mock<IPlanRepository> _planRepoMock;
    private readonly Mock<IBusinessService> _businessServiceMock;
    private readonly Mock<IViewRenderService> _viewRenderServiceMock;
    private readonly Mock<IVatCalculationService> _vatCalculationServiceMock;
    private readonly Mock<ILogger<BillingService>> _loggerMock;
    private readonly InvoiceSettings _invoiceSettings;

    private BillingInvoicePdfModel? _capturedModel;

    public BillingServiceInvoicePdfTests()
    {
        _subscriptionRepoMock = new Mock<SubscriptionRepository>(MockBehavior.Loose, null!);
        _billingInvoiceRepoMock = new Mock<BillingInvoiceRepository>(MockBehavior.Loose, null!);
        _billingPaymentRepoMock = new Mock<BillingPaymentRepository>(MockBehavior.Loose, null!);
        _planRepoMock = new Mock<IPlanRepository>();
        _businessServiceMock = new Mock<IBusinessService>();
        _viewRenderServiceMock = new Mock<IViewRenderService>();
        _vatCalculationServiceMock = new Mock<IVatCalculationService>();
        _loggerMock = new Mock<ILogger<BillingService>>();

        _invoiceSettings = new InvoiceSettings
        {
            CompanyName = "3 Inventors Ltd",
            CompanyAddress = "Nicosia, Cyprus",
            CompanyCountryCode = "CY",
            CompanyVatNumber = "CY10439718W",
            CompanyEmail = "invoices@3inventors.com",
            PlatformCode = "BILI"
        };

        // Capture the model passed to RenderViewToStringAsync
        _viewRenderServiceMock
            .Setup(v => v.RenderViewToStringAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Callback<string, object>((_, model) => _capturedModel = model as BillingInvoicePdfModel)
            .ReturnsAsync("<html></html>");
    }

    private BillingService CreateService()
    {
        var options = Options.Create(_invoiceSettings);

        return new BillingService(
            _subscriptionRepoMock.Object,
            _billingInvoiceRepoMock.Object,
            _billingPaymentRepoMock.Object,
            _planRepoMock.Object,
            _businessServiceMock.Object,
            _viewRenderServiceMock.Object,
            _vatCalculationServiceMock.Object,
            options,
            _loggerMock.Object);
    }

    private void SetupDefaultMocks(string? invoiceNumber = TestInvoiceNumber, string? customerCountry = "CY", string? vatNumber = null)
    {
        var invoice = new BillingInvoice
        {
            Id = TestInvoiceId,
            BusinessId = TestBusinessId,
            StripeInvoiceId = "si_test_123",
            AmountEur = 49.99m,
            PeriodStart = new DateTime(2026, 1, 1),
            PeriodEnd = new DateTime(2026, 1, 31),
            Status = "paid",
            PaidAtUtc = new DateTime(2026, 1, 15, 10, 0, 0),
            InvoiceNumber = invoiceNumber,
            IsEmailSent = true,
            CreatedAtUtc = new DateTime(2026, 1, 15, 10, 0, 0)
        };

        _billingInvoiceRepoMock
            .Setup(r => r.GetByIdAsync(TestInvoiceId, TestBusinessId))
            .ReturnsAsync(invoice);

        _billingPaymentRepoMock
            .Setup(r => r.GetByInvoiceIdAsync(TestInvoiceId))
            .ReturnsAsync(new List<BillingPayment>
            {
                new BillingPayment
                {
                    Id = 1,
                    InvoiceId = TestInvoiceId,
                    AmountEur = 49.99m,
                    Method = "card",
                    PaidAtUtc = new DateTime(2026, 1, 15, 10, 0, 0),
                    CreatedAtUtc = DateTime.UtcNow
                }
            });

        _subscriptionRepoMock
            .Setup(r => r.GetByBusinessIdAsync(TestBusinessId))
            .ReturnsAsync(new Subscription
            {
                Id = 1,
                BusinessId = TestBusinessId,
                PlanId = 2,
                Status = "active",
                CurrentPeriodStart = new DateTime(2026, 1, 1),
                CurrentPeriodEnd = new DateTime(2026, 1, 31),
                CreatedAtUtc = DateTime.UtcNow
            });

        _planRepoMock
            .Setup(r => r.GetByIdAsync(2))
            .ReturnsAsync(new Plan { Id = 2, Name = "Professional" });

        _businessServiceMock
            .Setup(s => s.GetBusinessByIdAsync(TestBusinessId))
            .ReturnsAsync(new Business { Id = TestBusinessId, Name = "Test Corp", IsActive = true, CreatedAtUtc = DateTime.UtcNow });

        _businessServiceMock
            .Setup(s => s.GetBusinessProfileAsync(TestBusinessId))
            .ReturnsAsync(new BusinessProfile
            {
                Id = 1,
                BusinessId = TestBusinessId,
                CompanyRegistrationNumber = "HE123456",
                VatRegistrationNumber = vatNumber ?? string.Empty,
                VatRegistrationDate = new DateOnly(2020, 1, 1),
                VatPeriodLengthInMonths = 3,
                AddressLine1 = "123 Main Street",
                AddressLine2 = "Suite 5",
                City = "Berlin",
                PostalCode = "10115",
                Country = customerCountry ?? string.Empty,
                Email = "contact@testcorp.com"
            });
    }

    #region Uses InvoiceNumber when present (Req 4.1)

    [Fact]
    public async Task GenerateInvoicePdfAsync_WithInvoiceNumber_UsesPersistedInvoiceNumber()
    {
        // Arrange
        SetupDefaultMocks(invoiceNumber: TestInvoiceNumber);

        _vatCalculationServiceMock
            .Setup(v => v.Calculate(It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(new VatCalculationResult(0.19m, 9.4981m, 59.4881m, false, null));

        var service = CreateService();

        // Act — will throw due to PuppeteerSharp, but model is captured before that
        try
        {
            await service.GenerateInvoicePdfAsync(TestInvoiceId, TestBusinessId);
        }
        catch
        {
            // Expected: PuppeteerSharp fails in unit tests, but model is already captured
        }

        // Assert
        Assert.NotNull(_capturedModel);
        Assert.Equal(TestInvoiceNumber, _capturedModel!.InvoiceNumber);
    }

    #endregion

    #region Falls back to legacy format when InvoiceNumber is null (Req 4.2)

    [Fact]
    public async Task GenerateInvoicePdfAsync_WithNullInvoiceNumber_FallsBackToLegacyFormat()
    {
        // Arrange
        SetupDefaultMocks(invoiceNumber: null);

        _vatCalculationServiceMock
            .Setup(v => v.Calculate(It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(new VatCalculationResult(0.19m, 9.4981m, 59.4881m, false, null));

        var service = CreateService();

        // Act
        try
        {
            await service.GenerateInvoicePdfAsync(TestInvoiceId, TestBusinessId);
        }
        catch
        {
            // Expected: PuppeteerSharp fails in unit tests
        }

        // Assert — falls back to INV-{Id:D6} format
        Assert.NotNull(_capturedModel);
        Assert.Equal($"INV-{TestInvoiceId:D6}", _capturedModel!.InvoiceNumber);
    }

    #endregion

    #region PDF model contains all issuer fields from InvoiceSettings (Req 5.1)

    [Fact]
    public async Task GenerateInvoicePdfAsync_PdfModelContainsAllIssuerFields()
    {
        // Arrange
        SetupDefaultMocks();

        _vatCalculationServiceMock
            .Setup(v => v.Calculate(It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(new VatCalculationResult(0.19m, 9.4981m, 59.4881m, false, null));

        var service = CreateService();

        // Act
        try
        {
            await service.GenerateInvoicePdfAsync(TestInvoiceId, TestBusinessId);
        }
        catch
        {
            // Expected: PuppeteerSharp fails in unit tests
        }

        // Assert — all issuer fields from InvoiceSettings are populated
        Assert.NotNull(_capturedModel);
        Assert.Equal(_invoiceSettings.CompanyName, _capturedModel!.CompanyName);
        Assert.Equal(_invoiceSettings.CompanyAddress, _capturedModel.CompanyAddress);
        Assert.Equal(_invoiceSettings.CompanyCountryCode, _capturedModel.CompanyCountryCode);
        Assert.Equal(_invoiceSettings.CompanyVatNumber, _capturedModel.CompanyVatNumber);
        Assert.Equal(_invoiceSettings.CompanyEmail, _capturedModel.CompanyEmail);
    }

    #endregion

    #region PDF model contains reverse charge notation for EU customer with VAT (Req 9.7)

    [Fact]
    public async Task GenerateInvoicePdfAsync_EuCustomerWithVat_ContainsReverseChargeNotation()
    {
        // Arrange — EU customer (Germany) with a VAT number triggers reverse charge
        SetupDefaultMocks(customerCountry: "DE", vatNumber: "DE123456789");

        var reverseChargeNotation = "Reverse Charge - Article 196 Council Directive 2006/112/EC";
        _vatCalculationServiceMock
            .Setup(v => v.Calculate(49.99m, "DE", "DE123456789"))
            .Returns(new VatCalculationResult(0m, 0m, 49.99m, true, reverseChargeNotation));

        var service = CreateService();

        // Act
        try
        {
            await service.GenerateInvoicePdfAsync(TestInvoiceId, TestBusinessId);
        }
        catch
        {
            // Expected: PuppeteerSharp fails in unit tests
        }

        // Assert — reverse charge fields populated
        Assert.NotNull(_capturedModel);
        Assert.True(_capturedModel!.IsReverseCharge);
        Assert.Equal(reverseChargeNotation, _capturedModel.ReverseChargeNotation);
        Assert.Equal(0m, _capturedModel.VatRate);
        Assert.Equal(0m, _capturedModel.VatAmount);
    }

    #endregion

    #region PDF model contains correct VAT calculation (Req 4.3, 9.6)

    [Fact]
    public async Task GenerateInvoicePdfAsync_CyprusCustomer_ContainsCorrectVatCalculation()
    {
        // Arrange — Cyprus customer gets 19% VAT
        SetupDefaultMocks(customerCountry: "CY", vatNumber: null);

        var expectedVatAmount = 49.99m * 0.19m; // 9.4981
        _vatCalculationServiceMock
            .Setup(v => v.Calculate(49.99m, "CY", It.IsAny<string?>()))
            .Returns(new VatCalculationResult(0.19m, expectedVatAmount, 49.99m + expectedVatAmount, false, null));

        var service = CreateService();

        // Act
        try
        {
            await service.GenerateInvoicePdfAsync(TestInvoiceId, TestBusinessId);
        }
        catch
        {
            // Expected: PuppeteerSharp fails in unit tests
        }

        // Assert — VAT rate and amount populated from VatCalculationService result
        Assert.NotNull(_capturedModel);
        Assert.Equal(0.19m, _capturedModel!.VatRate);
        Assert.Equal(expectedVatAmount, _capturedModel.VatAmount);
        Assert.Equal(49.99m + expectedVatAmount, _capturedModel.Total);
        Assert.Equal(49.99m, _capturedModel.Subtotal);
        Assert.False(_capturedModel.IsReverseCharge);
        Assert.Null(_capturedModel.ReverseChargeNotation);
    }

    #endregion
}
