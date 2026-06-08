using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Billing;
using Portal.Infrastructure.Entities.Stripe;
using Portal.Infrastructure.Services;
using Portal.Infrastructure.Repositories;
using Portal.Web.Configuration;
using Portal.Web.Models.Stripe;
using Portal.Web.Services.Billing;
using Portal.Web.Services.Stripe;
using Xunit;
using StripeLib = Stripe;

namespace Portal.Tests.PropertyBased.StripeOnboarding;

// Feature: stripe-onboarding, Property 2: Webhook idempotency

/// <summary>
/// Property-based tests for webhook idempotency.
/// For any Stripe webhook event whose EventId already exists in the stripe.WebhookEvent table,
/// the handler SHALL return HTTP 200 without modifying any database state (no subscription updates,
/// no new records, no duplicate WebhookEvent entries).
/// **Validates: Requirements 2.4, 2.5**
/// </summary>
public class WebhookIdempotencyPropertyTests
{
    private const string TestWebhookSecret = "whsec_test_secret_for_property_tests";

    /// <summary>
    /// Known Stripe event types that the webhook handler processes.
    /// </summary>
    private static readonly string[] KnownEventTypes = new[]
    {
        "checkout.session.completed",
        "invoice.paid",
        "invoice.payment_failed",
        "customer.subscription.updated",
        "customer.subscription.deleted"
    };

    /// <summary>
    /// Builds a minimal valid Stripe event JSON payload for testing.
    /// </summary>
    private static string BuildStripeEventJson(string eventId, string eventType)
    {
        return $$"""
        {
            "id": "{{eventId}}",
            "object": "event",
            "type": "{{eventType}}",
            "api_version": "2023-10-16",
            "created": 1700000000,
            "data": {
                "object": {}
            },
            "livemode": false,
            "pending_webhooks": 1,
            "request": {
                "id": "req_test",
                "idempotency_key": null
            }
        }
        """;
    }

    /// <summary>
    /// Generates a valid Stripe signature header for the given JSON payload using the test secret.
    /// </summary>
    private static string GenerateSignatureHeader(string json)
    {
        // Use Stripe's test header generation utility
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = $"{timestamp}.{json}";
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(TestWebhookSecret));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
        var signature = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        return $"t={timestamp},v1={signature}";
    }

    /// <summary>
    /// Creates a WebhookProcessingService with mocked dependencies.
    /// The WebhookEventRepository is configured to return true for ExistsByEventIdAsync (duplicate event).
    /// Returns the service and all mocks for verification.
    /// </summary>
    private static (
        WebhookProcessingService Service,
        Mock<WebhookEventRepository> WebhookEventRepoMock,
        Mock<SubscriptionRepository> SubscriptionRepoMock,
        Mock<BillingInvoiceRepository> InvoiceRepoMock,
        Mock<BillingPaymentRepository> PaymentRepoMock,
        Mock<StripeCustomerRepository> CustomerRepoMock,
        Mock<IProvisioningService> ProvisioningMock
    ) CreateServiceWithDuplicateEventMocks()
    {
        var mockWebhookEventRepo = new Mock<WebhookEventRepository>(MockBehavior.Loose, new object[] { null! });
        var mockSubscriptionRepo = new Mock<SubscriptionRepository>(MockBehavior.Loose, new object[] { null! });
        var mockInvoiceRepo = new Mock<BillingInvoiceRepository>(MockBehavior.Loose, new object[] { null! });
        var mockPaymentRepo = new Mock<BillingPaymentRepository>(MockBehavior.Loose, new object[] { null! });
        var mockCustomerRepo = new Mock<StripeCustomerRepository>(MockBehavior.Loose, new object[] { null! });
        var mockProvisioning = new Mock<IProvisioningService>();
        var mockInvoiceNumberGenerator = new Mock<IInvoiceNumberGenerator>();
        var mockInvoiceEmailService = new Mock<IInvoiceEmailService>();
        var mockLogger = new Mock<ILogger<WebhookProcessingService>>();

        // Configure ExistsByEventIdAsync to always return true (simulating duplicate event)
        mockWebhookEventRepo
            .Setup(r => r.ExistsByEventIdAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        // Configure StripeSettings with the test webhook secret
        var stripeSettings = Options.Create(new StripeSettings
        {
            SecretKey = "sk_test_fake",
            PublishableKey = "pk_test_fake",
            WebhookSigningSecret = TestWebhookSecret
        });

        // Mock PortalDbContext — not needed for idempotency path but required by constructor
        var dbContextOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"IdempotencyTest_{Guid.NewGuid()}")
            .Options;
        var portalDbContext = new PortalDbContext(dbContextOptions, new Mock<ICurrentTenantService>().Object);

        // Mock MembershipDbContext — not needed for idempotency path but required by constructor
        var membershipDbContextOptions = new DbContextOptionsBuilder<MembershipDbContext>()
            .UseInMemoryDatabase(databaseName: $"IdempotencyMembershipTest_{Guid.NewGuid()}")
            .Options;
        var membershipDbContext = new MembershipDbContext(membershipDbContextOptions);

        var service = new WebhookProcessingService(
            stripeSettings,
            mockWebhookEventRepo.Object,
            mockSubscriptionRepo.Object,
            mockInvoiceRepo.Object,
            mockPaymentRepo.Object,
            mockCustomerRepo.Object,
            mockProvisioning.Object,
            mockInvoiceNumberGenerator.Object,
            mockInvoiceEmailService.Object,
            membershipDbContext,
            portalDbContext,
            mockLogger.Object);

        return (service, mockWebhookEventRepo, mockSubscriptionRepo, mockInvoiceRepo, mockPaymentRepo, mockCustomerRepo, mockProvisioning);
    }

    #region Property 2a: Duplicate event returns HTTP 200

    /// <summary>
    /// Property 2a: For any Stripe webhook event whose EventId already exists in the
    /// stripe.WebhookEvent table, the handler SHALL return HTTP 200.
    /// **Validates: Requirements 2.4, 2.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DuplicateEvent_ReturnsHttp200(PositiveInt eventIdSeed, byte eventTypeSeed)
    {
        var eventId = $"evt_test_{eventIdSeed.Get:D8}";
        var eventType = KnownEventTypes[eventTypeSeed % KnownEventTypes.Length];

        var json = BuildStripeEventJson(eventId, eventType);
        var signatureHeader = GenerateSignatureHeader(json);

        var (service, _, _, _, _, _, _) = CreateServiceWithDuplicateEventMocks();

        var result = service.ProcessEventAsync(json, signatureHeader).GetAwaiter().GetResult();

        return (result == 200).ToProperty()
            .Label($"EventId='{eventId}', Type='{eventType}': Expected HTTP 200, Got {result}");
    }

    #endregion

    #region Property 2b: Duplicate event does not modify subscription state

    /// <summary>
    /// Property 2b: For any duplicate webhook event, the handler SHALL NOT call any
    /// SubscriptionRepository methods (no subscription updates).
    /// **Validates: Requirements 2.4, 2.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DuplicateEvent_DoesNotModifySubscription(PositiveInt eventIdSeed, byte eventTypeSeed)
    {
        var eventId = $"evt_test_{eventIdSeed.Get:D8}";
        var eventType = KnownEventTypes[eventTypeSeed % KnownEventTypes.Length];

        var json = BuildStripeEventJson(eventId, eventType);
        var signatureHeader = GenerateSignatureHeader(json);

        var (service, _, subscriptionRepoMock, _, _, _, _) = CreateServiceWithDuplicateEventMocks();

        service.ProcessEventAsync(json, signatureHeader).GetAwaiter().GetResult();

        var noSubscriptionCalls = true;
        try
        {
            subscriptionRepoMock.Verify(r => r.GetByBusinessIdAsync(It.IsAny<int>()), Times.Never());
            subscriptionRepoMock.Verify(r => r.InsertAsync(It.IsAny<Subscription>()), Times.Never());
            subscriptionRepoMock.Verify(r => r.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>()), Times.Never());
            subscriptionRepoMock.Verify(r => r.UpdatePeriodAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never());
        }
        catch
        {
            noSubscriptionCalls = false;
        }

        return noSubscriptionCalls.ToProperty()
            .Label($"EventId='{eventId}', Type='{eventType}': SubscriptionRepository should not be called for duplicate events");
    }

    #endregion

    #region Property 2c: Duplicate event does not create invoice or payment records

    /// <summary>
    /// Property 2c: For any duplicate webhook event, the handler SHALL NOT call
    /// BillingInvoiceRepository or BillingPaymentRepository insert methods (no new records).
    /// **Validates: Requirements 2.4, 2.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DuplicateEvent_DoesNotCreateInvoiceOrPayment(PositiveInt eventIdSeed, byte eventTypeSeed)
    {
        var eventId = $"evt_test_{eventIdSeed.Get:D8}";
        var eventType = KnownEventTypes[eventTypeSeed % KnownEventTypes.Length];

        var json = BuildStripeEventJson(eventId, eventType);
        var signatureHeader = GenerateSignatureHeader(json);

        var (service, _, _, invoiceRepoMock, paymentRepoMock, _, _) = CreateServiceWithDuplicateEventMocks();

        service.ProcessEventAsync(json, signatureHeader).GetAwaiter().GetResult();

        var noInvoiceCalls = true;
        var noPaymentCalls = true;
        try
        {
            invoiceRepoMock.Verify(r => r.InsertAsync(It.IsAny<BillingInvoice>()), Times.Never());
        }
        catch
        {
            noInvoiceCalls = false;
        }

        try
        {
            paymentRepoMock.Verify(r => r.InsertAsync(It.IsAny<BillingPayment>()), Times.Never());
        }
        catch
        {
            noPaymentCalls = false;
        }

        return (noInvoiceCalls && noPaymentCalls).ToProperty()
            .Label($"EventId='{eventId}', Type='{eventType}': No invoice/payment records should be created for duplicate events. " +
                   $"NoInvoiceCalls={noInvoiceCalls}, NoPaymentCalls={noPaymentCalls}");
    }

    #endregion

    #region Property 2d: Duplicate event does not invoke provisioning

    /// <summary>
    /// Property 2d: For any duplicate webhook event, the handler SHALL NOT invoke
    /// the ProvisioningService (no tenant provisioning for duplicates).
    /// **Validates: Requirements 2.4, 2.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DuplicateEvent_DoesNotInvokeProvisioning(PositiveInt eventIdSeed, byte eventTypeSeed)
    {
        var eventId = $"evt_test_{eventIdSeed.Get:D8}";
        var eventType = KnownEventTypes[eventTypeSeed % KnownEventTypes.Length];

        var json = BuildStripeEventJson(eventId, eventType);
        var signatureHeader = GenerateSignatureHeader(json);

        var (service, _, _, _, _, _, provisioningMock) = CreateServiceWithDuplicateEventMocks();

        service.ProcessEventAsync(json, signatureHeader).GetAwaiter().GetResult();

        var noProvisioningCalls = true;
        try
        {
            provisioningMock.Verify(p => p.ProvisionTenantAsync(It.IsAny<ProvisioningRequest>()), Times.Never());
        }
        catch
        {
            noProvisioningCalls = false;
        }

        return noProvisioningCalls.ToProperty()
            .Label($"EventId='{eventId}', Type='{eventType}': ProvisioningService should not be called for duplicate events");
    }

    #endregion

    #region Property 2e: Duplicate event does not insert duplicate WebhookEvent entries

    /// <summary>
    /// Property 2e: For any duplicate webhook event, the handler SHALL NOT insert a new
    /// WebhookEvent record (no duplicate entries in the webhook event log).
    /// **Validates: Requirements 2.4, 2.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DuplicateEvent_DoesNotInsertWebhookEventRecord(PositiveInt eventIdSeed, byte eventTypeSeed)
    {
        var eventId = $"evt_test_{eventIdSeed.Get:D8}";
        var eventType = KnownEventTypes[eventTypeSeed % KnownEventTypes.Length];

        var json = BuildStripeEventJson(eventId, eventType);
        var signatureHeader = GenerateSignatureHeader(json);

        var (service, webhookEventRepoMock, _, _, _, _, _) = CreateServiceWithDuplicateEventMocks();

        service.ProcessEventAsync(json, signatureHeader).GetAwaiter().GetResult();

        var noInsertCalls = true;
        try
        {
            webhookEventRepoMock.Verify(r => r.InsertAsync(It.IsAny<WebhookEvent>()), Times.Never());
        }
        catch
        {
            noInsertCalls = false;
        }

        return noInsertCalls.ToProperty()
            .Label($"EventId='{eventId}', Type='{eventType}': WebhookEvent.InsertAsync should not be called for duplicate events");
    }

    #endregion
}
