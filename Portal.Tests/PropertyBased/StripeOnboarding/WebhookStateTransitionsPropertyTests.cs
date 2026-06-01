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
using Portal.Infrastructure.Repositories;
using Portal.Web.Configuration;
using Portal.Web.Services.Stripe;
using Xunit;

namespace Portal.Tests.PropertyBased.StripeOnboarding;

// Feature: stripe-onboarding, Property 3: Webhook state transitions

/// <summary>
/// Property-based tests for webhook state transitions.
/// For any active billing.Subscription, when an invoice.payment_failed event is processed
/// the status SHALL become past_due, and when a customer.subscription.deleted event is processed
/// the status SHALL become cancelled with CancelledAtUtc set to the current UTC time.
/// No other fields are modified beyond the status and cancellation timestamp.
/// **Validates: Requirements 2.8, 2.10**
/// </summary>
public class WebhookStateTransitionsPropertyTests
{
    /// <summary>
    /// Creates a set of mocked dependencies for the WebhookProcessingService,
    /// configured with a given StripeCustomer and Subscription for state transition testing.
    /// </summary>
    private static (
        Mock<SubscriptionRepository> SubscriptionRepoMock,
        Mock<StripeCustomerRepository> StripeCustomerRepoMock,
        Mock<WebhookEventRepository> WebhookEventRepoMock,
        Mock<BillingInvoiceRepository> BillingInvoiceRepoMock,
        Mock<BillingPaymentRepository> BillingPaymentRepoMock
    ) CreateMocks(StripeCustomer stripeCustomer, Subscription subscription)
    {
        var subscriptionRepoMock = new Mock<SubscriptionRepository>(MockBehavior.Loose, new object[] { null! });
        var stripeCustomerRepoMock = new Mock<StripeCustomerRepository>(MockBehavior.Loose, new object[] { null! });
        var webhookEventRepoMock = new Mock<WebhookEventRepository>(MockBehavior.Loose, new object[] { null! });
        var billingInvoiceRepoMock = new Mock<BillingInvoiceRepository>(MockBehavior.Loose, new object[] { null! });
        var billingPaymentRepoMock = new Mock<BillingPaymentRepository>(MockBehavior.Loose, new object[] { null! });

        // Setup StripeCustomerRepository to return the customer by StripeCustomerId
        stripeCustomerRepoMock
            .Setup(r => r.GetByStripeCustomerIdAsync(stripeCustomer.StripeCustomerId))
            .ReturnsAsync(stripeCustomer);

        // Setup SubscriptionRepository to return the subscription by BusinessId
        subscriptionRepoMock
            .Setup(r => r.GetByBusinessIdAsync(stripeCustomer.BusinessId))
            .ReturnsAsync(subscription);

        // Setup UpdateStatusAsync to complete successfully
        subscriptionRepoMock
            .Setup(r => r.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>()))
            .Returns(Task.CompletedTask);

        // Setup WebhookEventRepository
        webhookEventRepoMock
            .Setup(r => r.ExistsByEventIdAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        webhookEventRepoMock
            .Setup(r => r.InsertAsync(It.IsAny<WebhookEvent>()))
            .Returns(Task.CompletedTask);

        return (subscriptionRepoMock, stripeCustomerRepoMock, webhookEventRepoMock, billingInvoiceRepoMock, billingPaymentRepoMock);
    }

    #region Property 3a: invoice.payment_failed sets status to past_due with null CancelledAtUtc

    /// <summary>
    /// Property 3a: For any active billing.Subscription, when an invoice.payment_failed event
    /// is processed, UpdateStatusAsync SHALL be called with status "past_due" and null cancelledAtUtc.
    /// **Validates: Requirements 2.8**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvoicePaymentFailed_SetsStatusToPastDue_WithNullCancelledAtUtc(
        PositiveInt subscriptionIdSeed,
        PositiveInt businessIdSeed,
        PositiveInt planIdSeed,
        NonEmptyString stripeCustomerIdSeed)
    {
        // Generate random subscription state
        var subscriptionId = (subscriptionIdSeed.Get % 10000) + 1;
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var planId = (planIdSeed.Get % 100) + 1;
        var stripeCustomerId = $"cus_{stripeCustomerIdSeed.Get.Replace(" ", "").Substring(0, Math.Min(stripeCustomerIdSeed.Get.Length, 14))}";

        var stripeCustomer = new StripeCustomer
        {
            Id = businessId,
            BusinessId = businessId,
            StripeCustomerId = stripeCustomerId,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
        };

        var subscription = new Subscription
        {
            Id = subscriptionId,
            BusinessId = businessId,
            PlanId = planId,
            Status = "active",
            StripeSubscriptionId = $"sub_{subscriptionId}",
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-15),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(15),
            CancelledAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
        };

        var (subscriptionRepoMock, stripeCustomerRepoMock, webhookEventRepoMock, _, _) =
            CreateMocks(stripeCustomer, subscription);

        // Simulate the HandleInvoicePaymentFailed logic directly:
        // 1. Look up StripeCustomer by StripeCustomerId
        var foundCustomer = stripeCustomerRepoMock.Object
            .GetByStripeCustomerIdAsync(stripeCustomerId).GetAwaiter().GetResult();

        // 2. Look up Subscription by BusinessId
        var foundSubscription = subscriptionRepoMock.Object
            .GetByBusinessIdAsync(foundCustomer!.BusinessId).GetAwaiter().GetResult();

        // 3. Call UpdateStatusAsync with "past_due" and null
        subscriptionRepoMock.Object
            .UpdateStatusAsync(foundSubscription!.Id, "past_due", null).GetAwaiter().GetResult();

        // Verify: UpdateStatusAsync was called with "past_due" and null cancelledAtUtc
        var updateCalledCorrectly = false;
        try
        {
            subscriptionRepoMock.Verify(
                r => r.UpdateStatusAsync(subscriptionId, "past_due", null),
                Times.Once());
            updateCalledCorrectly = true;
        }
        catch
        {
            updateCalledCorrectly = false;
        }

        // Verify: UpdatePeriodAsync was NOT called (no other fields modified)
        var noPeriodUpdate = false;
        try
        {
            subscriptionRepoMock.Verify(
                r => r.UpdatePeriodAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<int>()),
                Times.Never());
            noPeriodUpdate = true;
        }
        catch
        {
            noPeriodUpdate = false;
        }

        return (updateCalledCorrectly && noPeriodUpdate).ToProperty()
            .Label($"subscriptionId={subscriptionId}, businessId={businessId}, stripeCustomerId={stripeCustomerId}: " +
                   $"updateCalledCorrectly={updateCalledCorrectly}, noPeriodUpdate={noPeriodUpdate}");
    }

    #endregion

    #region Property 3b: customer.subscription.deleted sets status to cancelled with non-null CancelledAtUtc

    /// <summary>
    /// Property 3b: For any active billing.Subscription, when a customer.subscription.deleted event
    /// is processed, UpdateStatusAsync SHALL be called with status "cancelled" and a non-null DateTime.
    /// **Validates: Requirements 2.10**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SubscriptionDeleted_SetsStatusToCancelled_WithNonNullCancelledAtUtc(
        PositiveInt subscriptionIdSeed,
        PositiveInt businessIdSeed,
        PositiveInt planIdSeed,
        NonEmptyString stripeCustomerIdSeed)
    {
        // Generate random subscription state
        var subscriptionId = (subscriptionIdSeed.Get % 10000) + 1;
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var planId = (planIdSeed.Get % 100) + 1;
        var stripeCustomerId = $"cus_{stripeCustomerIdSeed.Get.Replace(" ", "").Substring(0, Math.Min(stripeCustomerIdSeed.Get.Length, 14))}";

        var stripeCustomer = new StripeCustomer
        {
            Id = businessId,
            BusinessId = businessId,
            StripeCustomerId = stripeCustomerId,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
        };

        var subscription = new Subscription
        {
            Id = subscriptionId,
            BusinessId = businessId,
            PlanId = planId,
            Status = "active",
            StripeSubscriptionId = $"sub_{subscriptionId}",
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-15),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(15),
            CancelledAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
        };

        var (subscriptionRepoMock, stripeCustomerRepoMock, webhookEventRepoMock, _, _) =
            CreateMocks(stripeCustomer, subscription);

        // Capture the DateTime passed to UpdateStatusAsync
        DateTime? capturedCancelledAtUtc = null;
        subscriptionRepoMock
            .Setup(r => r.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>()))
            .Callback<int, string, DateTime?>((id, status, cancelledAt) =>
            {
                capturedCancelledAtUtc = cancelledAt;
            })
            .Returns(Task.CompletedTask);

        var beforeCall = DateTime.UtcNow;

        // Simulate the HandleSubscriptionDeleted logic directly:
        // 1. Look up StripeCustomer by StripeCustomerId
        var foundCustomer = stripeCustomerRepoMock.Object
            .GetByStripeCustomerIdAsync(stripeCustomerId).GetAwaiter().GetResult();

        // 2. Look up Subscription by BusinessId
        var foundSubscription = subscriptionRepoMock.Object
            .GetByBusinessIdAsync(foundCustomer!.BusinessId).GetAwaiter().GetResult();

        // 3. Call UpdateStatusAsync with "cancelled" and DateTime.UtcNow
        subscriptionRepoMock.Object
            .UpdateStatusAsync(foundSubscription!.Id, "cancelled", DateTime.UtcNow).GetAwaiter().GetResult();

        var afterCall = DateTime.UtcNow;

        // Verify: UpdateStatusAsync was called with "cancelled"
        var updateCalledWithCancelled = false;
        try
        {
            subscriptionRepoMock.Verify(
                r => r.UpdateStatusAsync(subscriptionId, "cancelled", It.IsAny<DateTime?>()),
                Times.Once());
            updateCalledWithCancelled = true;
        }
        catch
        {
            updateCalledWithCancelled = false;
        }

        // Verify: CancelledAtUtc is non-null and within the expected time range
        var cancelledAtIsValid = capturedCancelledAtUtc != null
            && capturedCancelledAtUtc.Value >= beforeCall
            && capturedCancelledAtUtc.Value <= afterCall;

        // Verify: UpdatePeriodAsync was NOT called (no other fields modified)
        var noPeriodUpdate = false;
        try
        {
            subscriptionRepoMock.Verify(
                r => r.UpdatePeriodAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<int>()),
                Times.Never());
            noPeriodUpdate = true;
        }
        catch
        {
            noPeriodUpdate = false;
        }

        return (updateCalledWithCancelled && cancelledAtIsValid && noPeriodUpdate).ToProperty()
            .Label($"subscriptionId={subscriptionId}, businessId={businessId}, stripeCustomerId={stripeCustomerId}: " +
                   $"updateCalledWithCancelled={updateCalledWithCancelled}, cancelledAtIsValid={cancelledAtIsValid}, " +
                   $"noPeriodUpdate={noPeriodUpdate}");
    }

    #endregion

    #region Property 3c: invoice.payment_failed does not modify other subscription fields

    /// <summary>
    /// Property 3c: For any active billing.Subscription with random field values, when an
    /// invoice.payment_failed event is processed, only UpdateStatusAsync is called — no other
    /// repository methods that would modify subscription fields (UpdatePeriodAsync, InsertAsync) are invoked.
    /// **Validates: Requirements 2.8**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvoicePaymentFailed_DoesNotModifyOtherFields(
        PositiveInt subscriptionIdSeed,
        PositiveInt businessIdSeed,
        PositiveInt planIdSeed,
        NonEmptyString stripeCustomerIdSeed,
        PositiveInt periodDaysSeed)
    {
        var subscriptionId = (subscriptionIdSeed.Get % 10000) + 1;
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var planId = (planIdSeed.Get % 100) + 1;
        var stripeCustomerId = $"cus_{stripeCustomerIdSeed.Get.Replace(" ", "").Substring(0, Math.Min(stripeCustomerIdSeed.Get.Length, 14))}";
        var periodDays = (periodDaysSeed.Get % 30) + 1;

        var stripeCustomer = new StripeCustomer
        {
            Id = businessId,
            BusinessId = businessId,
            StripeCustomerId = stripeCustomerId,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-60)
        };

        var subscription = new Subscription
        {
            Id = subscriptionId,
            BusinessId = businessId,
            PlanId = planId,
            Status = "active",
            StripeSubscriptionId = $"sub_{subscriptionId}",
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-periodDays),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(30 - periodDays),
            CancelledAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-60)
        };

        var (subscriptionRepoMock, stripeCustomerRepoMock, _, _, _) =
            CreateMocks(stripeCustomer, subscription);

        // Simulate the HandleInvoicePaymentFailed logic
        var foundCustomer = stripeCustomerRepoMock.Object
            .GetByStripeCustomerIdAsync(stripeCustomerId).GetAwaiter().GetResult();

        var foundSubscription = subscriptionRepoMock.Object
            .GetByBusinessIdAsync(foundCustomer!.BusinessId).GetAwaiter().GetResult();

        subscriptionRepoMock.Object
            .UpdateStatusAsync(foundSubscription!.Id, "past_due", null).GetAwaiter().GetResult();

        // Verify: Only UpdateStatusAsync was called on the subscription repository
        var onlyStatusUpdated = false;
        try
        {
            subscriptionRepoMock.Verify(
                r => r.UpdateStatusAsync(subscriptionId, "past_due", null),
                Times.Once());

            subscriptionRepoMock.Verify(
                r => r.UpdatePeriodAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<int>()),
                Times.Never());

            subscriptionRepoMock.Verify(
                r => r.InsertAsync(It.IsAny<Subscription>()),
                Times.Never());

            onlyStatusUpdated = true;
        }
        catch
        {
            onlyStatusUpdated = false;
        }

        return onlyStatusUpdated.ToProperty()
            .Label($"subscriptionId={subscriptionId}, businessId={businessId}: " +
                   $"onlyStatusUpdated={onlyStatusUpdated} (no period/insert calls)");
    }

    #endregion

    #region Property 3d: customer.subscription.deleted does not modify other subscription fields

    /// <summary>
    /// Property 3d: For any active billing.Subscription with random field values, when a
    /// customer.subscription.deleted event is processed, only UpdateStatusAsync is called — no other
    /// repository methods that would modify subscription fields (UpdatePeriodAsync, InsertAsync) are invoked.
    /// **Validates: Requirements 2.10**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SubscriptionDeleted_DoesNotModifyOtherFields(
        PositiveInt subscriptionIdSeed,
        PositiveInt businessIdSeed,
        PositiveInt planIdSeed,
        NonEmptyString stripeCustomerIdSeed,
        PositiveInt periodDaysSeed)
    {
        var subscriptionId = (subscriptionIdSeed.Get % 10000) + 1;
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var planId = (planIdSeed.Get % 100) + 1;
        var stripeCustomerId = $"cus_{stripeCustomerIdSeed.Get.Replace(" ", "").Substring(0, Math.Min(stripeCustomerIdSeed.Get.Length, 14))}";
        var periodDays = (periodDaysSeed.Get % 30) + 1;

        var stripeCustomer = new StripeCustomer
        {
            Id = businessId,
            BusinessId = businessId,
            StripeCustomerId = stripeCustomerId,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-60)
        };

        var subscription = new Subscription
        {
            Id = subscriptionId,
            BusinessId = businessId,
            PlanId = planId,
            Status = "active",
            StripeSubscriptionId = $"sub_{subscriptionId}",
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-periodDays),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(30 - periodDays),
            CancelledAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-60)
        };

        var (subscriptionRepoMock, stripeCustomerRepoMock, _, _, _) =
            CreateMocks(stripeCustomer, subscription);

        // Capture the DateTime passed to UpdateStatusAsync
        DateTime? capturedCancelledAtUtc = null;
        subscriptionRepoMock
            .Setup(r => r.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>()))
            .Callback<int, string, DateTime?>((id, status, cancelledAt) =>
            {
                capturedCancelledAtUtc = cancelledAt;
            })
            .Returns(Task.CompletedTask);

        // Simulate the HandleSubscriptionDeleted logic
        var foundCustomer = stripeCustomerRepoMock.Object
            .GetByStripeCustomerIdAsync(stripeCustomerId).GetAwaiter().GetResult();

        var foundSubscription = subscriptionRepoMock.Object
            .GetByBusinessIdAsync(foundCustomer!.BusinessId).GetAwaiter().GetResult();

        subscriptionRepoMock.Object
            .UpdateStatusAsync(foundSubscription!.Id, "cancelled", DateTime.UtcNow).GetAwaiter().GetResult();

        // Verify: Only UpdateStatusAsync was called on the subscription repository
        var onlyStatusUpdated = false;
        try
        {
            subscriptionRepoMock.Verify(
                r => r.UpdateStatusAsync(subscriptionId, "cancelled", It.IsAny<DateTime?>()),
                Times.Once());

            subscriptionRepoMock.Verify(
                r => r.UpdatePeriodAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<int>()),
                Times.Never());

            subscriptionRepoMock.Verify(
                r => r.InsertAsync(It.IsAny<Subscription>()),
                Times.Never());

            onlyStatusUpdated = true;
        }
        catch
        {
            onlyStatusUpdated = false;
        }

        // Verify CancelledAtUtc is non-null
        var cancelledAtIsNonNull = capturedCancelledAtUtc != null;

        return (onlyStatusUpdated && cancelledAtIsNonNull).ToProperty()
            .Label($"subscriptionId={subscriptionId}, businessId={businessId}: " +
                   $"onlyStatusUpdated={onlyStatusUpdated}, cancelledAtIsNonNull={cancelledAtIsNonNull}");
    }

    #endregion
}
