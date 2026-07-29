using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Configuration;
using Serilog;
using Stripe;
using Stripe.Checkout;

namespace Portal.Web.Services.Stripe;

/// <summary>
/// Stripe Connect service implementation — handles OAuth onboarding,
/// Checkout Session creation with destination charges, and webhook auto-reconciliation.
/// No platform fee is taken — this is a pure value add-on.
/// </summary>
public class StripeConnectService : IStripeConnectService
{
    private readonly StripeConnectRepository _repository;
    private readonly PortalDbContext _portalDbContext;
    private readonly PaymentRepository _paymentRepository;
    private readonly IFinancialStatusEngine _financialStatusEngine;
    private readonly IPaymentReceiptService _receiptService;
    private readonly StripeSettings _stripeSettings;
    private readonly IStripeKeyResolutionService _keyResolutionService;

    public StripeConnectService(
        StripeConnectRepository repository,
        PortalDbContext portalDbContext,
        PaymentRepository paymentRepository,
        IFinancialStatusEngine financialStatusEngine,
        IPaymentReceiptService receiptService,
        IOptions<StripeSettings> stripeSettings,
        IStripeKeyResolutionService keyResolutionService)
    {
        _repository = repository;
        _portalDbContext = portalDbContext;
        _paymentRepository = paymentRepository;
        _financialStatusEngine = financialStatusEngine;
        _receiptService = receiptService;
        _stripeSettings = stripeSettings.Value;
        _keyResolutionService = keyResolutionService;
    }

    // ─── Onboarding ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<string> GetOAuthConnectUrlAsync(int businessId)
    {
        var state = $"{businessId}_{Guid.NewGuid():N}";

        var resolvedKeys = await _keyResolutionService.ResolveKeysAsync(businessId);
        var clientId = resolvedKeys.ConnectClientId;
        var redirectUri = resolvedKeys.ConnectOAuthRedirectUri;

        var url = $"https://connect.stripe.com/oauth/authorize" +
                  $"?response_type=code" +
                  $"&client_id={clientId}" +
                  $"&scope=read_write" +
                  $"&state={state}" +
                  $"&redirect_uri={Uri.EscapeDataString(redirectUri ?? "")}";

        return url;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CompleteOAuthAsync(int businessId, string authorizationCode, string state)
    {
        try
        {
            // Verify the state starts with the expected businessId
            if (!state.StartsWith($"{businessId}_"))
                return ServiceResult.Fail("Invalid OAuth state parameter.");

            // Exchange authorization code for connected account ID
            var options = new OAuthTokenCreateOptions
            {
                GrantType = "authorization_code",
                Code = authorizationCode
            };

            var service = new OAuthTokenService();
            var response = await service.CreateAsync(options);

            if (string.IsNullOrEmpty(response.StripeUserId))
                return ServiceResult.Fail("Failed to retrieve Stripe account ID.");

            // Check if already connected (reconnecting after disconnect)
            var existing = await _repository.GetConnectedAccountAsync(businessId);
            if (existing != null)
                return ServiceResult.Fail("This business already has a connected Stripe account.");

            // Store the connected account
            var connectedAccount = new StripeConnectedAccount
            {
                BusinessId = businessId,
                StripeAccountId = response.StripeUserId,
                IsActive = true,
                ConnectedAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _repository.InsertConnectedAccountAsync(connectedAccount);

            return new ServiceResult { Success = true, Message = "Stripe account connected successfully." };
        }
        catch (StripeException ex)
        {
            Log.Error(ex, "Stripe OAuth exchange failed for businessId={BusinessId}", businessId);
            return ServiceResult.Fail($"Stripe connection failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CompleteOAuthAsync failed for businessId={BusinessId}", businessId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DisconnectAsync(int businessId)
    {
        try
        {
            var account = await _repository.GetConnectedAccountAsync(businessId);
            if (account == null)
                return ServiceResult.Fail("No active Stripe connection found.");

            await _repository.DisconnectAccountAsync(businessId);

            return new ServiceResult { Success = true, Message = "Stripe account disconnected." };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "DisconnectAsync failed for businessId={BusinessId}", businessId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsConnectedAsync(int businessId)
    {
        var account = await _repository.GetConnectedAccountAsync(businessId);
        return account != null;
    }

    /// <inheritdoc />
    public async Task<string?> GetConnectedAccountIdAsync(int businessId)
    {
        var account = await _repository.GetConnectedAccountAsync(businessId);
        return account?.StripeAccountId;
    }

    // ─── Checkout ────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<ServiceResult<string>> CreateCheckoutSessionAsync(int invoiceId, int businessId, string successUrl, string cancelUrl, string? customerName)
    {
        try
        {
            // 1. Get connected account
            var connectedAccountId = await GetConnectedAccountIdAsync(businessId);
            if (string.IsNullOrEmpty(connectedAccountId))
                return ServiceResult<string>.Fail("Business does not have Stripe Connect enabled.");

            // 2. Get invoice and calculate outstanding balance
            var invoice = await _portalDbContext.Invoices
                .FirstOrDefaultAsync(i => i.Id == invoiceId && i.BusinessId == businessId);

            if (invoice == null)
                return ServiceResult<string>.Fail("Invoice not found.");

            // Calculate outstanding balance
            var totalPaid = await _portalDbContext.Payments
                .IgnoreQueryFilters()
                .Where(p => p.InvoiceId == invoiceId && !p.IsVoided && p.BusinessId == businessId)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            var outstandingBalance = invoice.TotalAmount - totalPaid;

            if (outstandingBalance <= 0)
                return ServiceResult<string>.Fail("This invoice has no outstanding balance.");

            // 3. Resolve Stripe keys for this business
            var resolvedKeys = await _keyResolutionService.ResolveKeysAsync(businessId);

            // 4. Create Stripe Checkout Session (destination charge, no application fee)
            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(outstandingBalance * 100), // convert to cents
                            Currency = "eur",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"Invoice {invoice.InvoiceNumber}",
                                Description = customerName != null
                                    ? $"Payment for invoice {invoice.InvoiceNumber} — {customerName}"
                                    : $"Payment for invoice {invoice.InvoiceNumber}"
                            }
                        },
                        Quantity = 1
                    }
                },
                Mode = "payment",
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                PaymentIntentData = new SessionPaymentIntentDataOptions
                {
                    TransferData = new SessionPaymentIntentDataTransferDataOptions
                    {
                        Destination = connectedAccountId
                    }
                },
                Metadata = new Dictionary<string, string>
                {
                    { "invoiceId", invoiceId.ToString() },
                    { "businessId", businessId.ToString() },
                    { "platform", "portal" }
                },
                ExpiresAt = DateTime.UtcNow.AddMinutes(30)
            };

            // Use resolved secret key for this API call
            var requestOptions = new global::Stripe.RequestOptions { ApiKey = resolvedKeys.SecretKey };

            var sessionService = new SessionService();
            var session = await sessionService.CreateAsync(options, requestOptions);

            // 5. Store checkout session record for idempotency and fee tracking
            var checkoutSession = new StripeCheckoutSession
            {
                BusinessId = businessId,
                InvoiceId = invoiceId,
                StripeSessionId = session.Id,
                Amount = outstandingBalance,
                Currency = "EUR",
                Status = "pending",
                StripePaymentIntentId = session.PaymentIntentId,
                CustomerName = customerName,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _repository.InsertCheckoutSessionAsync(checkoutSession);

            // 6. Return the checkout URL
            return ServiceResult<string>.Ok(session.Url);
        }
        catch (StripeException ex)
        {
            Log.Error(ex, "CreateCheckoutSessionAsync Stripe error for invoiceId={InvoiceId}", invoiceId);
            return ServiceResult<string>.Fail("Card payments are temporarily unavailable for this business. Please try again later or use bank transfer.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CreateCheckoutSessionAsync failed for invoiceId={InvoiceId}", invoiceId);
            throw;
        }
    }

    // ─── Webhook ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<ServiceResult> HandleCheckoutCompletedAsync(string stripeSessionId, string paymentIntentId)
    {
        try
        {
            // 1. Idempotency check
            var checkoutSession = await _repository.GetByStripeSessionIdAsync(stripeSessionId);
            if (checkoutSession == null)
                return ServiceResult.Fail("Checkout session not found.");

            if (checkoutSession.Status == "completed")
                return new ServiceResult { Success = true, Message = "Already processed." };

            // 2. Retrieve charge and balance transaction for fee info
            decimal stripeFeeAmount = 0m;
            decimal netAmount = checkoutSession.Amount;
            string? chargeId = null;

            try
            {
                var paymentIntentService = new PaymentIntentService();
                var paymentIntent = await paymentIntentService.GetAsync(paymentIntentId, new PaymentIntentGetOptions
                {
                    Expand = new List<string> { "latest_charge.balance_transaction" }
                });

                var charge = paymentIntent.LatestCharge;
                if (charge != null)
                {
                    chargeId = charge.Id;
                    var balanceTransaction = charge.BalanceTransaction;
                    if (balanceTransaction != null)
                    {
                        stripeFeeAmount = balanceTransaction.Fee / 100m; // cents to EUR
                        netAmount = balanceTransaction.Net / 100m;
                    }
                }
            }
            catch (StripeException ex)
            {
                Log.Warning(ex, "Failed to retrieve fee info for session {SessionId}", stripeSessionId);
                // Continue without fee info — payment recording is more important
            }

            // 3. Get the "Card" payment method type ID
            var cardMethodId = await _portalDbContext.PaymentMethodTypes
                .Where(pmt => pmt.Name == "Card")
                .Select(pmt => pmt.Id)
                .FirstOrDefaultAsync();

            if (cardMethodId == 0)
            {
                Log.Error("Card payment method type not found in database. Seed required.");
                return ServiceResult.Fail("Card payment method type not configured.");
            }

            // 4. Create Payment record (same as manual payment recording)
            var payment = new Payment
            {
                BusinessId = checkoutSession.BusinessId,
                InvoiceId = checkoutSession.InvoiceId,
                ParentPaymentId = null,
                IsAutoAllocated = false,
                CustomerId = null,
                CreditAmount = 0,
                PaymentMethodTypeId = cardMethodId,
                PaymentDateUtc = DateTime.UtcNow,
                Amount = checkoutSession.Amount,
                Reference = chargeId ?? stripeSessionId,
                Notes = "Stripe card payment",
                IsVoided = false,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = null
            };

            var paymentId = await _paymentRepository.InsertAsync(payment);

            // 5. Recalculate invoice financial status
            await _financialStatusEngine.RecalculateStatusAsync(checkoutSession.InvoiceId, checkoutSession.BusinessId, stripeSessionId);

            // 5b. Auto-generate receipt if enabled for this business
            try
            {
                await TryAutoGenerateReceiptAsync(paymentId, checkoutSession.BusinessId);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Auto-receipt generation failed for PaymentId={PaymentId}, BusinessId={BusinessId}. Continuing webhook processing.",
                    paymentId, checkoutSession.BusinessId);
            }

            // 6. Update checkout session with completion data
            await _repository.MarkSessionCompletedAsync(
                stripeSessionId,
                stripeFeeAmount,
                netAmount,
                chargeId ?? "",
                paymentId);

            Log.Information("Stripe payment processed: SessionId={SessionId}, PaymentId={PaymentId}, Amount={Amount}",
                stripeSessionId, paymentId, checkoutSession.Amount);

            return new ServiceResult { Success = true, Message = "Payment recorded successfully." };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "HandleCheckoutCompletedAsync failed for sessionId={SessionId}", stripeSessionId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task HandleCheckoutExpiredAsync(string stripeSessionId)
    {
        try
        {
            await _repository.MarkSessionExpiredAsync(stripeSessionId);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "HandleCheckoutExpiredAsync failed for sessionId={SessionId}", stripeSessionId);
        }
    }

    // ─── Card Payments View ──────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<List<StripeCheckoutSession>> GetCompletedSessionsAsync(int businessId, DateTime? fromUtc, DateTime? toUtc)
    {
        return await _repository.GetCompletedSessionsAsync(businessId, fromUtc, toUtc);
    }

    // ─── Private Helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Checks if auto-receipt is enabled for the business and generates a receipt if so.
    /// Used after webhook-triggered payment creation (no userId — system generated).
    /// </summary>
    private async Task TryAutoGenerateReceiptAsync(int paymentId, int businessId)
    {
        var business = await _portalDbContext.Businesses
            .Where(b => b.Id == businessId)
            .Select(b => new { b.IsAutoReceiptEnabled })
            .FirstOrDefaultAsync();

        if (business?.IsAutoReceiptEnabled != true)
            return;

        // Use default signature if available
        int? defaultSignatureId = null;
        var defaultSig = await _portalDbContext.Signatures.IgnoreQueryFilters()
            .Where(s => s.BusinessId == businessId && s.IsDefault && s.IsActive)
            .Select(s => s.Id)
            .FirstOrDefaultAsync();

        if (defaultSig > 0)
            defaultSignatureId = defaultSig;

        await _receiptService.GenerateReceiptAsync(paymentId, businessId, "", defaultSignatureId);
    }
}
