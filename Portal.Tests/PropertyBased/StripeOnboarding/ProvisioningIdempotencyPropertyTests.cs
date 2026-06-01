using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Billing;
using Portal.Infrastructure.Entities.Identity;
using Portal.Infrastructure.Entities.Stripe;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Models.Stripe;
using Portal.Web.Services.Stripe;
using Xunit;

namespace Portal.Tests.PropertyBased.StripeOnboarding;

// Feature: stripe-onboarding, Property 6: Provisioning idempotency

/// <summary>
/// Property-based tests for provisioning idempotency.
/// For any checkout.session.completed event where the PendingRegistration is already marked as completed
/// OR the Stripe Session Id has already been provisioned, the ProvisioningService SHALL create zero new
/// records and return success without error.
/// **Validates: Requirements 3.11, 3.12**
/// </summary>
public class ProvisioningIdempotencyPropertyTests
{
    /// <summary>
    /// Generates a valid ProvisioningRequest with random but constrained values.
    /// </summary>
    private static Gen<ProvisioningRequest> GenProvisioningRequest()
    {
        return from userId in Gen.Elements("user_001", "user_002", "user_003", "user_abc", "user_xyz")
               from pendingRegId in Gen.Choose(1, 10000)
               from planId in Gen.Choose(1, 5)
               from custSuffix in Gen.Choose(1, 99999)
               from sessionSuffix in Gen.Choose(1, 99999)
               from subSuffix in Gen.Choose(1, 99999)
               from piSuffix in Gen.Choose(1, 99999)
               from amount in Gen.Choose(500, 50000).Select(x => (decimal)x / 100m)
               from daysOffset in Gen.Choose(1, 365)
               select new ProvisioningRequest
               {
                   UserId = userId,
                   PendingRegistrationId = pendingRegId,
                   PlanId = planId,
                   StripeCustomerId = $"cus_test_{custSuffix:D5}",
                   StripeSessionId = $"cs_test_{sessionSuffix:D5}",
                   StripeSubscriptionId = $"sub_test_{subSuffix:D5}",
                   StripePaymentIntentId = $"pi_test_{piSuffix:D5}",
                   SubscriptionStart = DateTime.UtcNow.AddDays(-daysOffset),
                   SubscriptionEnd = DateTime.UtcNow.AddDays(30 - daysOffset),
                   AmountPaid = amount,
                   Currency = "eur"
               };
    }

    /// <summary>
    /// Creates a ProvisioningService with mocked dependencies configured for the
    /// "PendingRegistration already completed" idempotency scenario.
    /// </summary>
    private static (
        ProvisioningService Service,
        Mock<SubscriptionRepository> SubscriptionRepoMock,
        Mock<BillingInvoiceRepository> InvoiceRepoMock,
        Mock<BillingPaymentRepository> PaymentRepoMock,
        Mock<StripeCustomerRepository> CustomerRepoMock
    ) CreateServiceWithCompletedPendingRegistration(ProvisioningRequest request)
    {
        // Setup MembershipDbContext with a completed PendingRegistration
        var membershipOptions = new DbContextOptionsBuilder<MembershipDbContext>()
            .UseInMemoryDatabase(databaseName: $"IdempotencyMembership_{Guid.NewGuid()}")
            .Options;
        var membershipDbContext = new MembershipDbContext(membershipOptions);

        // Seed a completed PendingRegistration
        membershipDbContext.PendingRegistrations.Add(new PendingRegistration
        {
            Id = request.PendingRegistrationId,
            UserId = request.UserId,
            PlanId = request.PlanId,
            IsCompleted = true,
            CompletedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1),
            User = new ApplicationUser
            {
                Id = request.UserId,
                UserName = $"{request.UserId}@test.com",
                Email = $"{request.UserId}@test.com",
                FirstName = "Test",
                LastName = "User"
            }
        });
        membershipDbContext.SaveChanges();

        // Setup PortalDbContext
        var portalOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"IdempotencyPortal_{Guid.NewGuid()}")
            .Options;
        var portalDbContext = new PortalDbContext(portalOptions, new Mock<ICurrentTenantService>().Object);

        // Mock repositories
        var mockSubscriptionRepo = new Mock<SubscriptionRepository>(MockBehavior.Loose, new object[] { null! });
        var mockInvoiceRepo = new Mock<BillingInvoiceRepository>(MockBehavior.Loose, new object[] { null! });
        var mockPaymentRepo = new Mock<BillingPaymentRepository>(MockBehavior.Loose, new object[] { null! });
        var mockCustomerRepo = new Mock<StripeCustomerRepository>(MockBehavior.Loose, new object[] { null! });
        var mockLogger = new Mock<ILogger<ProvisioningService>>();

        // StripeCustomerRepository should return null (no existing customer) — 
        // the idempotency check on PendingRegistration.IsCompleted should trigger first
        mockCustomerRepo
            .Setup(r => r.GetByStripeCustomerIdAsync(It.IsAny<string>()))
            .ReturnsAsync((StripeCustomer?)null);

        var service = new ProvisioningService(
            membershipDbContext,
            portalDbContext,
            mockSubscriptionRepo.Object,
            mockInvoiceRepo.Object,
            mockPaymentRepo.Object,
            mockCustomerRepo.Object,
            mockLogger.Object);

        return (service, mockSubscriptionRepo, mockInvoiceRepo, mockPaymentRepo, mockCustomerRepo);
    }

    /// <summary>
    /// Creates a ProvisioningService with mocked dependencies configured for the
    /// "StripeCustomerId already provisioned" idempotency scenario.
    /// </summary>
    private static (
        ProvisioningService Service,
        Mock<SubscriptionRepository> SubscriptionRepoMock,
        Mock<BillingInvoiceRepository> InvoiceRepoMock,
        Mock<BillingPaymentRepository> PaymentRepoMock,
        Mock<StripeCustomerRepository> CustomerRepoMock
    ) CreateServiceWithAlreadyProvisionedCustomer(ProvisioningRequest request)
    {
        // Setup MembershipDbContext with a non-completed PendingRegistration
        var membershipOptions = new DbContextOptionsBuilder<MembershipDbContext>()
            .UseInMemoryDatabase(databaseName: $"IdempotencyMembership_{Guid.NewGuid()}")
            .Options;
        var membershipDbContext = new MembershipDbContext(membershipOptions);

        // Seed a non-completed PendingRegistration (so the first check passes)
        membershipDbContext.PendingRegistrations.Add(new PendingRegistration
        {
            Id = request.PendingRegistrationId,
            UserId = request.UserId,
            PlanId = request.PlanId,
            IsCompleted = false,
            CompletedAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1),
            User = new ApplicationUser
            {
                Id = request.UserId,
                UserName = $"{request.UserId}@test.com",
                Email = $"{request.UserId}@test.com",
                FirstName = "Test",
                LastName = "User"
            }
        });
        membershipDbContext.SaveChanges();

        // Setup PortalDbContext
        var portalOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"IdempotencyPortal_{Guid.NewGuid()}")
            .Options;
        var portalDbContext = new PortalDbContext(portalOptions, new Mock<ICurrentTenantService>().Object);

        // Mock repositories
        var mockSubscriptionRepo = new Mock<SubscriptionRepository>(MockBehavior.Loose, new object[] { null! });
        var mockInvoiceRepo = new Mock<BillingInvoiceRepository>(MockBehavior.Loose, new object[] { null! });
        var mockPaymentRepo = new Mock<BillingPaymentRepository>(MockBehavior.Loose, new object[] { null! });
        var mockCustomerRepo = new Mock<StripeCustomerRepository>(MockBehavior.Loose, new object[] { null! });
        var mockLogger = new Mock<ILogger<ProvisioningService>>();

        // StripeCustomerRepository returns an existing customer (already provisioned)
        mockCustomerRepo
            .Setup(r => r.GetByStripeCustomerIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new StripeCustomer
            {
                Id = 1,
                BusinessId = 42,
                StripeCustomerId = request.StripeCustomerId,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10)
            });

        var service = new ProvisioningService(
            membershipDbContext,
            portalDbContext,
            mockSubscriptionRepo.Object,
            mockInvoiceRepo.Object,
            mockPaymentRepo.Object,
            mockCustomerRepo.Object,
            mockLogger.Object);

        return (service, mockSubscriptionRepo, mockInvoiceRepo, mockPaymentRepo, mockCustomerRepo);
    }

    #region Property 6a: Completed PendingRegistration returns success without creating records

    /// <summary>
    /// Property 6a: For any provisioning request where the PendingRegistration is already
    /// marked as completed, the ProvisioningService SHALL return success without creating
    /// any new records (Business, Subscription, Invoice, Payment, StripeCustomer).
    /// **Validates: Requirements 3.11**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ProvisioningIdempotencyArbitraries) })]
    public Property CompletedPendingRegistration_ReturnsSuccess_WithoutCreatingRecords(ProvisioningRequest request)
    {
        var (service, subscriptionRepoMock, invoiceRepoMock, paymentRepoMock, customerRepoMock) =
            CreateServiceWithCompletedPendingRegistration(request);

        var result = service.ProvisionTenantAsync(request).GetAwaiter().GetResult();

        // Verify success returned
        var isSuccess = result.Success;

        // Verify no records were created
        var noSubscriptionInsert = true;
        var noInvoiceInsert = true;
        var noPaymentInsert = true;
        var noCustomerInsert = true;

        try { subscriptionRepoMock.Verify(r => r.InsertAsync(It.IsAny<Subscription>()), Times.Never()); }
        catch { noSubscriptionInsert = false; }

        try { invoiceRepoMock.Verify(r => r.InsertAsync(It.IsAny<BillingInvoice>()), Times.Never()); }
        catch { noInvoiceInsert = false; }

        try { paymentRepoMock.Verify(r => r.InsertAsync(It.IsAny<BillingPayment>()), Times.Never()); }
        catch { noPaymentInsert = false; }

        try { customerRepoMock.Verify(r => r.InsertAsync(It.IsAny<StripeCustomer>()), Times.Never()); }
        catch { noCustomerInsert = false; }

        return (isSuccess && noSubscriptionInsert && noInvoiceInsert && noPaymentInsert && noCustomerInsert)
            .ToProperty()
            .Label($"PendingRegistrationId={request.PendingRegistrationId}, UserId='{request.UserId}': " +
                   $"Success={isSuccess}, NoSubscription={noSubscriptionInsert}, NoInvoice={noInvoiceInsert}, " +
                   $"NoPayment={noPaymentInsert}, NoCustomer={noCustomerInsert}");
    }

    #endregion

    #region Property 6b: Already provisioned StripeCustomerId returns success without creating records

    /// <summary>
    /// Property 6b: For any provisioning request where the StripeCustomerId has already been
    /// provisioned (exists in stripe.Customer), the ProvisioningService SHALL return success
    /// without creating any new records.
    /// **Validates: Requirements 3.12**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ProvisioningIdempotencyArbitraries) })]
    public Property AlreadyProvisionedCustomer_ReturnsSuccess_WithoutCreatingRecords(ProvisioningRequest request)
    {
        var (service, subscriptionRepoMock, invoiceRepoMock, paymentRepoMock, customerRepoMock) =
            CreateServiceWithAlreadyProvisionedCustomer(request);

        var result = service.ProvisionTenantAsync(request).GetAwaiter().GetResult();

        // Verify success returned
        var isSuccess = result.Success;

        // Verify no records were created
        var noSubscriptionInsert = true;
        var noInvoiceInsert = true;
        var noPaymentInsert = true;
        var noCustomerInsert = true;

        try { subscriptionRepoMock.Verify(r => r.InsertAsync(It.IsAny<Subscription>()), Times.Never()); }
        catch { noSubscriptionInsert = false; }

        try { invoiceRepoMock.Verify(r => r.InsertAsync(It.IsAny<BillingInvoice>()), Times.Never()); }
        catch { noInvoiceInsert = false; }

        try { paymentRepoMock.Verify(r => r.InsertAsync(It.IsAny<BillingPayment>()), Times.Never()); }
        catch { noPaymentInsert = false; }

        try { customerRepoMock.Verify(r => r.InsertAsync(It.IsAny<StripeCustomer>()), Times.Never()); }
        catch { noCustomerInsert = false; }

        return (isSuccess && noSubscriptionInsert && noInvoiceInsert && noPaymentInsert && noCustomerInsert)
            .ToProperty()
            .Label($"StripeCustomerId='{request.StripeCustomerId}', UserId='{request.UserId}': " +
                   $"Success={isSuccess}, NoSubscription={noSubscriptionInsert}, NoInvoice={noInvoiceInsert}, " +
                   $"NoPayment={noPaymentInsert}, NoCustomer={noCustomerInsert}");
    }

    #endregion

    #region Property 6c: Already provisioned StripeCustomerId returns existing BusinessId

    /// <summary>
    /// Property 6c: For any provisioning request where the StripeCustomerId has already been
    /// provisioned, the ProvisioningService SHALL return the existing BusinessId in the result.
    /// **Validates: Requirements 3.12**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ProvisioningIdempotencyArbitraries) })]
    public Property AlreadyProvisionedCustomer_ReturnsExistingBusinessId(ProvisioningRequest request)
    {
        var (service, _, _, _, _) = CreateServiceWithAlreadyProvisionedCustomer(request);

        var result = service.ProvisionTenantAsync(request).GetAwaiter().GetResult();

        // The existing customer has BusinessId = 42 (set in the mock)
        return (result.Success && result.BusinessId == 42)
            .ToProperty()
            .Label($"StripeCustomerId='{request.StripeCustomerId}': " +
                   $"Success={result.Success}, BusinessId={result.BusinessId} (expected 42)");
    }

    #endregion

    #region Property 6d: Repeated calls with same request produce same result

    /// <summary>
    /// Property 6d: Calling ProvisionTenantAsync multiple times with the same request
    /// (where PendingRegistration is already completed) produces the same success result
    /// each time without side effects.
    /// **Validates: Requirements 3.11, 3.12**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ProvisioningIdempotencyArbitraries) })]
    public Property RepeatedCalls_ProduceSameResult(ProvisioningRequest request)
    {
        var (service, subscriptionRepoMock, invoiceRepoMock, paymentRepoMock, customerRepoMock) =
            CreateServiceWithCompletedPendingRegistration(request);

        // Call multiple times
        var result1 = service.ProvisionTenantAsync(request).GetAwaiter().GetResult();
        var result2 = service.ProvisionTenantAsync(request).GetAwaiter().GetResult();
        var result3 = service.ProvisionTenantAsync(request).GetAwaiter().GetResult();

        // All calls should return success
        var allSuccess = result1.Success && result2.Success && result3.Success;

        // No records should have been created across all calls
        var noSubscriptionInsert = true;
        var noInvoiceInsert = true;
        var noPaymentInsert = true;
        var noCustomerInsert = true;

        try { subscriptionRepoMock.Verify(r => r.InsertAsync(It.IsAny<Subscription>()), Times.Never()); }
        catch { noSubscriptionInsert = false; }

        try { invoiceRepoMock.Verify(r => r.InsertAsync(It.IsAny<BillingInvoice>()), Times.Never()); }
        catch { noInvoiceInsert = false; }

        try { paymentRepoMock.Verify(r => r.InsertAsync(It.IsAny<BillingPayment>()), Times.Never()); }
        catch { noPaymentInsert = false; }

        try { customerRepoMock.Verify(r => r.InsertAsync(It.IsAny<StripeCustomer>()), Times.Never()); }
        catch { noCustomerInsert = false; }

        return (allSuccess && noSubscriptionInsert && noInvoiceInsert && noPaymentInsert && noCustomerInsert)
            .ToProperty()
            .Label($"PendingRegistrationId={request.PendingRegistrationId}: " +
                   $"AllSuccess={allSuccess}, NoSubscription={noSubscriptionInsert}, NoInvoice={noInvoiceInsert}, " +
                   $"NoPayment={noPaymentInsert}, NoCustomer={noCustomerInsert}");
    }

    #endregion

    #region Property 6e: No error returned for idempotent scenarios

    /// <summary>
    /// Property 6e: For any idempotent provisioning scenario (completed PendingRegistration
    /// or already provisioned StripeCustomerId), the result SHALL have no error message.
    /// **Validates: Requirements 3.11, 3.12**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ProvisioningIdempotencyArbitraries) })]
    public Property IdempotentScenario_HasNoErrorMessage(ProvisioningRequest request, bool useCompletedRegistration)
    {
        ProvisioningResult result;

        if (useCompletedRegistration)
        {
            var (service, _, _, _, _) = CreateServiceWithCompletedPendingRegistration(request);
            result = service.ProvisionTenantAsync(request).GetAwaiter().GetResult();
        }
        else
        {
            var (service, _, _, _, _) = CreateServiceWithAlreadyProvisionedCustomer(request);
            result = service.ProvisionTenantAsync(request).GetAwaiter().GetResult();
        }

        return (result.Success && result.ErrorMessage == null)
            .ToProperty()
            .Label($"Scenario={(useCompletedRegistration ? "CompletedRegistration" : "AlreadyProvisioned")}, " +
                   $"UserId='{request.UserId}': Success={result.Success}, ErrorMessage='{result.ErrorMessage}'");
    }

    #endregion
}

/// <summary>
/// FsCheck Arbitrary provider for ProvisioningRequest generation.
/// </summary>
public class ProvisioningIdempotencyArbitraries
{
    public static Arbitrary<ProvisioningRequest> ProvisioningRequest()
    {
        var gen = from userId in Gen.Elements("user_001", "user_002", "user_003", "user_abc", "user_xyz")
                  from pendingRegId in Gen.Choose(1, 10000)
                  from planId in Gen.Choose(1, 5)
                  from custSuffix in Gen.Choose(1, 99999)
                  from sessionSuffix in Gen.Choose(1, 99999)
                  from subSuffix in Gen.Choose(1, 99999)
                  from piSuffix in Gen.Choose(1, 99999)
                  from amountCents in Gen.Choose(500, 50000)
                  from daysOffset in Gen.Choose(1, 365)
                  select new ProvisioningRequest
                  {
                      UserId = userId,
                      PendingRegistrationId = pendingRegId,
                      PlanId = planId,
                      StripeCustomerId = $"cus_test_{custSuffix:D5}",
                      StripeSessionId = $"cs_test_{sessionSuffix:D5}",
                      StripeSubscriptionId = $"sub_test_{subSuffix:D5}",
                      StripePaymentIntentId = $"pi_test_{piSuffix:D5}",
                      SubscriptionStart = DateTime.UtcNow.AddDays(-daysOffset),
                      SubscriptionEnd = DateTime.UtcNow.AddDays(30 - daysOffset),
                      AmountPaid = (decimal)amountCents / 100m,
                      Currency = "eur"
                  };

        return Arb.From(gen);
    }
}
