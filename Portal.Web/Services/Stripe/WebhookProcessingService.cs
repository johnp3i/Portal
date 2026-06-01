using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Billing;
using Portal.Infrastructure.Entities.Stripe;
using Portal.Infrastructure.Repositories;
using Portal.Web.Configuration;
using Portal.Web.Models.Stripe;
using StripeLib = Stripe;

namespace Portal.Web.Services.Stripe;

/// <summary>
/// Processes incoming Stripe webhook events by verifying signatures, checking idempotency,
/// routing to the appropriate handler, and wrapping state changes in a database transaction.
/// </summary>
public class WebhookProcessingService : IWebhookProcessingService
{
    private readonly IOptions<StripeSettings> _stripeSettings;
    private readonly WebhookEventRepository _webhookEventRepository;
    private readonly SubscriptionRepository _subscriptionRepository;
    private readonly BillingInvoiceRepository _billingInvoiceRepository;
    private readonly BillingPaymentRepository _billingPaymentRepository;
    private readonly StripeCustomerRepository _stripeCustomerRepository;
    private readonly IProvisioningService _provisioningService;
    private readonly PortalDbContext _portalDbContext;
    private readonly ILogger<WebhookProcessingService> _logger;

    public WebhookProcessingService(
        IOptions<StripeSettings> stripeSettings,
        WebhookEventRepository webhookEventRepository,
        SubscriptionRepository subscriptionRepository,
        BillingInvoiceRepository billingInvoiceRepository,
        BillingPaymentRepository billingPaymentRepository,
        StripeCustomerRepository stripeCustomerRepository,
        IProvisioningService provisioningService,
        PortalDbContext portalDbContext,
        ILogger<WebhookProcessingService> logger)
    {
        _stripeSettings = stripeSettings;
        _webhookEventRepository = webhookEventRepository;
        _subscriptionRepository = subscriptionRepository;
        _billingInvoiceRepository = billingInvoiceRepository;
        _billingPaymentRepository = billingPaymentRepository;
        _stripeCustomerRepository = stripeCustomerRepository;
        _provisioningService = provisioningService;
        _portalDbContext = portalDbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> ProcessEventAsync(string json, string signatureHeader)
    {
        StripeLib.Event stripeEvent;

        // 1. Verify Stripe signature
        try
        {
            stripeEvent = StripeLib.EventUtility.ConstructEvent(
                json,
                signatureHeader,
                _stripeSettings.Value.WebhookSigningSecret);
        }
        catch (StripeLib.StripeException ex)
        {
            _logger.LogWarning("Stripe webhook signature verification failed. Reason: {Reason}", ex.Message);
            return 400;
        }

        var eventId = stripeEvent.Id;
        var eventType = stripeEvent.Type;

        // 2. Check idempotency
        var alreadyProcessed = await _webhookEventRepository.ExistsByEventIdAsync(eventId);
        if (alreadyProcessed)
        {
            _logger.LogInformation("Duplicate webhook event skipped. EventId: {EventId}, Type: {Type}", eventId, eventType);
            return 200;
        }

        // 3. Route to handler based on event type
        try
        {
            switch (eventType)
            {
                case "checkout.session.completed":
                    await HandleCheckoutSessionCompleted(stripeEvent, eventId, eventType);
                    break;

                case "invoice.paid":
                    await HandleInvoicePaid(stripeEvent, eventId, eventType);
                    break;

                case "invoice.payment_failed":
                    await HandleInvoicePaymentFailed(stripeEvent, eventId, eventType);
                    break;

                case "customer.subscription.updated":
                    await HandleSubscriptionUpdated(stripeEvent, eventId, eventType);
                    break;

                case "customer.subscription.deleted":
                    await HandleSubscriptionDeleted(stripeEvent, eventId, eventType);
                    break;

                default:
                    _logger.LogInformation("Unrecognized webhook event type received. EventId: {EventId}, Type: {Type}", eventId, eventType);
                    return 200;
            }

            _logger.LogInformation("Webhook event processed successfully. EventId: {EventId}, Type: {Type}, Result: {Result}", eventId, eventType, "success");
            return 200;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook event processing failed. EventId: {EventId}, Type: {Type}, Result: {Result}", eventId, eventType, "error");
            return 500;
        }
    }

    private async Task HandleCheckoutSessionCompleted(StripeLib.Event stripeEvent, string eventId, string eventType)
    {
        var session = stripeEvent.Data.Object as StripeLib.Checkout.Session;
        if (session == null)
        {
            _logger.LogWarning("checkout.session.completed event has null session data. EventId: {EventId}", eventId);
            return;
        }

        // Extract metadata for provisioning
        var metadata = session.Metadata;
        var userId = metadata?.ContainsKey("UserId") == true ? metadata["UserId"] : null;
        var pendingRegistrationIdStr = metadata?.ContainsKey("PendingRegistrationId") == true ? metadata["PendingRegistrationId"] : null;

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(pendingRegistrationIdStr))
        {
            _logger.LogWarning("checkout.session.completed event missing required metadata. EventId: {EventId}", eventId);
            return;
        }

        if (!int.TryParse(pendingRegistrationIdStr, out var pendingRegistrationId))
        {
            _logger.LogWarning("checkout.session.completed event has invalid PendingRegistrationId metadata. EventId: {EventId}, Value: {Value}", eventId, pendingRegistrationIdStr);
            return;
        }

        // Build provisioning request from session data
        var subscriptionId = session.SubscriptionId ?? session.Subscription?.Id ?? string.Empty;
        var customerId = session.CustomerId ?? session.Customer?.Id ?? string.Empty;
        var paymentIntentId = session.PaymentIntentId ?? session.PaymentIntent?.Id ?? string.Empty;

        // Extract plan from metadata or subscription
        var planIdStr = metadata?.ContainsKey("PlanId") == true ? metadata["PlanId"] : null;
        int.TryParse(planIdStr, out var planId);

        // Extract amounts
        var amountTotal = session.AmountTotal ?? 0L;
        var amountPaid = amountTotal / 100m; // Stripe amounts are in cents
        var currency = session.Currency ?? "eur";

        // Extract subscription period dates
        var subscriptionStart = DateTime.UtcNow;
        var subscriptionEnd = DateTime.UtcNow.AddMonths(1);

        if (session.Subscription?.Items?.Data?.Count > 0)
        {
            var firstItem = session.Subscription.Items.Data[0];
            subscriptionStart = firstItem.CurrentPeriodStart;
            subscriptionEnd = firstItem.CurrentPeriodEnd;
        }

        var provisioningRequest = new ProvisioningRequest
        {
            UserId = userId,
            PendingRegistrationId = pendingRegistrationId,
            PlanId = planId,
            StripeCustomerId = customerId,
            StripeSessionId = session.Id,
            StripeSubscriptionId = subscriptionId,
            StripePaymentIntentId = paymentIntentId,
            SubscriptionStart = subscriptionStart,
            SubscriptionEnd = subscriptionEnd,
            AmountPaid = amountPaid,
            Currency = currency
        };

        // Provisioning handles its own transaction; record webhook event after
        var result = await _provisioningService.ProvisionTenantAsync(provisioningRequest);

        // Record the webhook event after successful provisioning
        await using var transaction = await _portalDbContext.Database.BeginTransactionAsync();
        try
        {
            await _webhookEventRepository.InsertAsync(new WebhookEvent
            {
                EventId = eventId,
                Type = eventType,
                ProcessedAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow
            });

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        if (!result.Success)
        {
            _logger.LogWarning("Provisioning returned failure for checkout.session.completed. EventId: {EventId}, Error: {Error}", eventId, result.ErrorMessage);
        }
    }

    private async Task HandleInvoicePaid(StripeLib.Event stripeEvent, string eventId, string eventType)
    {
        var invoice = stripeEvent.Data.Object as StripeLib.Invoice;
        if (invoice == null)
        {
            _logger.LogWarning("invoice.paid event has null invoice data. EventId: {EventId}", eventId);
            return;
        }

        var stripeCustomerId = invoice.CustomerId ?? invoice.Customer?.Id;
        if (string.IsNullOrEmpty(stripeCustomerId))
        {
            _logger.LogWarning("invoice.paid event missing customer reference. EventId: {EventId}", eventId);
            return;
        }

        // Find the business via StripeCustomer mapping
        var stripeCustomer = await _stripeCustomerRepository.GetByStripeCustomerIdAsync(stripeCustomerId);
        if (stripeCustomer == null)
        {
            _logger.LogWarning("invoice.paid event references unknown Stripe customer. EventId: {EventId}, StripeCustomerId: {StripeCustomerId}", eventId, stripeCustomerId);
            return;
        }

        var subscription = await _subscriptionRepository.GetByBusinessIdAsync(stripeCustomer.BusinessId);
        if (subscription == null)
        {
            _logger.LogWarning("invoice.paid event references business with no subscription. EventId: {EventId}, BusinessId: {BusinessId}", eventId, stripeCustomer.BusinessId);

            // Still record the webhook event
            await RecordWebhookEvent(eventId, eventType);
            return;
        }

        await using var transaction = await _portalDbContext.Database.BeginTransactionAsync();
        try
        {
            // Update subscription period
            var periodStart = invoice.PeriodStart;
            var periodEnd = invoice.PeriodEnd;

            await _subscriptionRepository.UpdatePeriodAsync(
                subscription.Id,
                periodStart,
                periodEnd,
                "active",
                subscription.PlanId);

            // Record the billing invoice
            var amountEur = invoice.AmountPaid / 100m;
            var invoiceId = await _billingInvoiceRepository.InsertAsync(new BillingInvoice
            {
                BusinessId = stripeCustomer.BusinessId,
                StripeInvoiceId = invoice.Id,
                AmountEur = amountEur,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                Status = "paid",
                PaidAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow
            });

            // Record the payment
            await _billingPaymentRepository.InsertAsync(new BillingPayment
            {
                InvoiceId = invoiceId,
                AmountEur = amountEur,
                Method = "stripe",
                PaidAtUtc = DateTime.UtcNow,
                StripePaymentIntentId = invoice.Id,
                CreatedAtUtc = DateTime.UtcNow
            });

            // Record webhook event
            await _webhookEventRepository.InsertAsync(new WebhookEvent
            {
                EventId = eventId,
                Type = eventType,
                ProcessedAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow
            });

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task HandleInvoicePaymentFailed(StripeLib.Event stripeEvent, string eventId, string eventType)
    {
        var invoice = stripeEvent.Data.Object as StripeLib.Invoice;
        if (invoice == null)
        {
            _logger.LogWarning("invoice.payment_failed event has null invoice data. EventId: {EventId}", eventId);
            return;
        }

        var stripeCustomerId = invoice.CustomerId ?? invoice.Customer?.Id;
        if (string.IsNullOrEmpty(stripeCustomerId))
        {
            _logger.LogWarning("invoice.payment_failed event missing customer reference. EventId: {EventId}", eventId);
            return;
        }

        var stripeCustomer = await _stripeCustomerRepository.GetByStripeCustomerIdAsync(stripeCustomerId);
        if (stripeCustomer == null)
        {
            _logger.LogWarning("invoice.payment_failed event references unknown Stripe customer. EventId: {EventId}, StripeCustomerId: {StripeCustomerId}", eventId, stripeCustomerId);
            return;
        }

        var subscription = await _subscriptionRepository.GetByBusinessIdAsync(stripeCustomer.BusinessId);
        if (subscription == null)
        {
            _logger.LogWarning("invoice.payment_failed event references business with no subscription. EventId: {EventId}, BusinessId: {BusinessId}", eventId, stripeCustomer.BusinessId);

            await RecordWebhookEvent(eventId, eventType);
            return;
        }

        await using var transaction = await _portalDbContext.Database.BeginTransactionAsync();
        try
        {
            await _subscriptionRepository.UpdateStatusAsync(subscription.Id, "past_due", null);

            await _webhookEventRepository.InsertAsync(new WebhookEvent
            {
                EventId = eventId,
                Type = eventType,
                ProcessedAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow
            });

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task HandleSubscriptionUpdated(StripeLib.Event stripeEvent, string eventId, string eventType)
    {
        var stripeSubscription = stripeEvent.Data.Object as StripeLib.Subscription;
        if (stripeSubscription == null)
        {
            _logger.LogWarning("customer.subscription.updated event has null subscription data. EventId: {EventId}", eventId);
            return;
        }

        var stripeCustomerId = stripeSubscription.CustomerId ?? stripeSubscription.Customer?.Id;
        if (string.IsNullOrEmpty(stripeCustomerId))
        {
            _logger.LogWarning("customer.subscription.updated event missing customer reference. EventId: {EventId}", eventId);
            return;
        }

        var stripeCustomer = await _stripeCustomerRepository.GetByStripeCustomerIdAsync(stripeCustomerId);
        if (stripeCustomer == null)
        {
            _logger.LogWarning("customer.subscription.updated event references unknown Stripe customer. EventId: {EventId}, StripeCustomerId: {StripeCustomerId}", eventId, stripeCustomerId);
            return;
        }

        var subscription = await _subscriptionRepository.GetByBusinessIdAsync(stripeCustomer.BusinessId);
        if (subscription == null)
        {
            _logger.LogWarning("customer.subscription.updated event references business with no subscription. EventId: {EventId}, StripeSubscriptionId: {StripeSubscriptionId}", eventId, stripeSubscription.Id);

            await RecordWebhookEvent(eventId, eventType);
            return;
        }

        await using var transaction = await _portalDbContext.Database.BeginTransactionAsync();
        try
        {
            var newStatus = MapStripeStatus(stripeSubscription.Status);

            // Get period from subscription items
            var periodStart = DateTime.UtcNow;
            var periodEnd = DateTime.UtcNow.AddMonths(1);
            if (stripeSubscription.Items?.Data?.Count > 0)
            {
                periodStart = stripeSubscription.Items.Data[0].CurrentPeriodStart;
                periodEnd = stripeSubscription.Items.Data[0].CurrentPeriodEnd;
            }

            // Determine plan from subscription items
            var planId = subscription.PlanId; // Default to current plan
            if (stripeSubscription.Items?.Data?.Count > 0)
            {
                var priceId = stripeSubscription.Items.Data[0].Price?.Id;
                if (!string.IsNullOrEmpty(priceId))
                {
                    var plan = await GetPlanByStripePriceIdAsync(priceId);
                    if (plan != null)
                    {
                        planId = plan.Id;
                    }
                }
            }

            // If the webhook sets Status to "active" with a CurrentPeriodEnd later than the
            // currently stored value, this indicates a genuine renewal — reset IsGraceAccessUsed.
            if (newStatus == "active" && periodEnd > subscription.CurrentPeriodEnd)
            {
                await _subscriptionRepository.UpdatePeriodWithGraceResetAsync(
                    subscription.Id,
                    periodStart,
                    periodEnd,
                    newStatus,
                    planId);
            }
            else
            {
                await _subscriptionRepository.UpdatePeriodAsync(
                    subscription.Id,
                    periodStart,
                    periodEnd,
                    newStatus,
                    planId);
            }

            await _webhookEventRepository.InsertAsync(new WebhookEvent
            {
                EventId = eventId,
                Type = eventType,
                ProcessedAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow
            });

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task HandleSubscriptionDeleted(StripeLib.Event stripeEvent, string eventId, string eventType)
    {
        var stripeSubscription = stripeEvent.Data.Object as StripeLib.Subscription;
        if (stripeSubscription == null)
        {
            _logger.LogWarning("customer.subscription.deleted event has null subscription data. EventId: {EventId}", eventId);
            return;
        }

        var stripeCustomerId = stripeSubscription.CustomerId ?? stripeSubscription.Customer?.Id;
        if (string.IsNullOrEmpty(stripeCustomerId))
        {
            _logger.LogWarning("customer.subscription.deleted event missing customer reference. EventId: {EventId}", eventId);
            return;
        }

        var stripeCustomer = await _stripeCustomerRepository.GetByStripeCustomerIdAsync(stripeCustomerId);
        if (stripeCustomer == null)
        {
            _logger.LogWarning("customer.subscription.deleted event references unknown Stripe customer. EventId: {EventId}, StripeCustomerId: {StripeCustomerId}", eventId, stripeCustomerId);
            return;
        }

        var subscription = await _subscriptionRepository.GetByBusinessIdAsync(stripeCustomer.BusinessId);
        if (subscription == null)
        {
            _logger.LogWarning("customer.subscription.deleted event references business with no subscription. EventId: {EventId}, StripeSubscriptionId: {StripeSubscriptionId}", eventId, stripeSubscription.Id);

            await RecordWebhookEvent(eventId, eventType);
            return;
        }

        // Skip status update if subscription is already cancelled (e.g., by the Expiry Guard)
        if (subscription.Status == "cancelled")
        {
            _logger.LogInformation("customer.subscription.deleted event skipped status update — subscription already cancelled. EventId: {EventId}, SubscriptionId: {SubscriptionId}, BusinessId: {BusinessId}", eventId, subscription.Id, stripeCustomer.BusinessId);

            await RecordWebhookEvent(eventId, eventType);
            return;
        }

        await using var transaction = await _portalDbContext.Database.BeginTransactionAsync();
        try
        {
            await _subscriptionRepository.UpdateStatusAsync(subscription.Id, "cancelled", DateTime.UtcNow);

            await _webhookEventRepository.InsertAsync(new WebhookEvent
            {
                EventId = eventId,
                Type = eventType,
                ProcessedAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow
            });

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Records a webhook event outside of a handler-specific transaction.
    /// Used when the event is acknowledged but no state change is needed.
    /// </summary>
    private async Task RecordWebhookEvent(string eventId, string eventType)
    {
        await using var transaction = await _portalDbContext.Database.BeginTransactionAsync();
        try
        {
            await _webhookEventRepository.InsertAsync(new WebhookEvent
            {
                EventId = eventId,
                Type = eventType,
                ProcessedAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow
            });

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Looks up a Plan by its StripePriceId using a direct query.
    /// </summary>
    private async Task<Portal.Infrastructure.Entities.Plan?> GetPlanByStripePriceIdAsync(string stripePriceId)
    {
        try
        {
            var connection = _portalDbContext.Database.GetDbConnection();

            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT [Plan].[Id], [Plan].[Name]
                FROM [dbo].[Plan]
                WHERE [Plan].[StripePriceId] = @StripePriceId";

            var transaction = _portalDbContext.Database.CurrentTransaction;
            if (transaction != null)
                command.Transaction = transaction.GetDbTransaction();

            command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@StripePriceId", stripePriceId));

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Portal.Infrastructure.Entities.Plan
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1)
                };
            }

            return null;
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Maps Stripe subscription status string to the local status values.
    /// </summary>
    private static string MapStripeStatus(string stripeStatus)
    {
        return stripeStatus switch
        {
            "active" => "active",
            "past_due" => "past_due",
            "canceled" => "cancelled",
            "trialing" => "trialing",
            "incomplete" => "incomplete",
            "unpaid" => "unpaid",
            "incomplete_expired" => "cancelled",
            _ => "active"
        };
    }
}
