using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Portal.Infrastructure.Data;
using Portal.Web.Configuration;
using Portal.Web.Models.Stripe;
using Stripe;
using Stripe.Checkout;

namespace Portal.Web.Services.Stripe;

/// <summary>
/// Handles Stripe Checkout Session creation for users completing their subscription payment.
/// Loads the user's PendingRegistration, validates preconditions, and creates a Stripe
/// Checkout Session in subscription mode with the Plan's StripePriceId.
/// </summary>
public class CheckoutService : ICheckoutService
{
    private readonly MembershipDbContext _membershipDbContext;
    private readonly PortalDbContext _portalDbContext;
    private readonly ILogger<CheckoutService> _logger;
    private readonly StripeSettings _stripeSettings;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CheckoutService(
        MembershipDbContext membershipDbContext,
        PortalDbContext portalDbContext,
        ILogger<CheckoutService> logger,
        IOptions<StripeSettings> stripeSettings,
        IHttpContextAccessor httpContextAccessor)
    {
        _membershipDbContext = membershipDbContext;
        _portalDbContext = portalDbContext;
        _logger = logger;
        _stripeSettings = stripeSettings.Value;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public async Task<CheckoutResult> CreateCheckoutSessionAsync(string userId)
    {
        try
        {
            // Load PendingRegistration by UserId
            var pendingRegistration = await _membershipDbContext.PendingRegistrations
                .FirstOrDefaultAsync(pr => pr.UserId == userId);

            if (pendingRegistration == null)
            {
                _logger.LogWarning("No PendingRegistration found for user {UserId}", userId);
                return new CheckoutResult
                {
                    Success = false,
                    FailureReason = CheckoutFailureReason.NoPendingRegistration,
                    ErrorMessage = "No pending registration found."
                };
            }

            // Validate: not already completed
            if (pendingRegistration.IsCompleted)
            {
                _logger.LogInformation("PendingRegistration already completed for user {UserId}", userId);
                return new CheckoutResult
                {
                    Success = false,
                    FailureReason = CheckoutFailureReason.AlreadyCompleted,
                    ErrorMessage = "Registration has already been completed."
                };
            }

            // Load Plan from PortalDbContext
            var plan = await _portalDbContext.Plans
                .FirstOrDefaultAsync(p => p.Id == pendingRegistration.PlanId);

            // Validate: Plan has a non-null, non-empty StripePriceId
            if (plan == null || string.IsNullOrWhiteSpace(plan.StripePriceId))
            {
                _logger.LogError(
                    "Plan not available for checkout. PlanId: {PlanId}, UserId: {UserId}",
                    pendingRegistration.PlanId, userId);
                return new CheckoutResult
                {
                    Success = false,
                    FailureReason = CheckoutFailureReason.PlanNotAvailable,
                    ErrorMessage = "The selected plan is not available for purchase."
                };
            }

            // Build absolute URLs for success and cancel
            var baseUrl = GetBaseUrl();
            var successUrl = $"{baseUrl}/Checkout/Success";
            var cancelUrl = $"{baseUrl}/Checkout/Cancelled";

            // Create Stripe Checkout Session
            var sessionOptions = new SessionCreateOptions
            {
                Mode = "subscription",
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Price = plan.StripePriceId,
                        Quantity = 1,
                        TaxRates = string.IsNullOrWhiteSpace(_stripeSettings.DefaultTaxRateId)
                            ? null
                            : new List<string> { _stripeSettings.DefaultTaxRateId }
                    }
                },
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                AllowPromotionCodes = true,
                Metadata = new Dictionary<string, string>
                {
                    { "PendingRegistrationId", pendingRegistration.Id.ToString() },
                    { "UserId", userId },
                    { "PlanId", pendingRegistration.PlanId.ToString() }
                }
            };

            var sessionService = new SessionService(new StripeClient(_stripeSettings.SecretKey));
            var session = await sessionService.CreateAsync(sessionOptions);

            _logger.LogInformation(
                "Stripe Checkout Session created. SessionId: {SessionId}, UserId: {UserId}, PlanId: {PlanId}",
                session.Id, userId, plan.Id);

            return new CheckoutResult
            {
                Success = true,
                CheckoutUrl = session.Url
            };
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex,
                "Stripe API error creating Checkout Session. UserId: {UserId}, ErrorCode: {ErrorCode}, ErrorMessage: {ErrorMessage}",
                userId, ex.StripeError?.Code, ex.StripeError?.Message);

            return new CheckoutResult
            {
                Success = false,
                FailureReason = CheckoutFailureReason.StripeApiError,
                ErrorMessage = "Payment setup failed. Please try again."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error creating Checkout Session for user {UserId}", userId);
            throw;
        }
    }

    private string GetBaseUrl()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            _logger.LogWarning("HttpContext is null when building checkout URLs");
            return string.Empty;
        }

        var request = httpContext.Request;
        return $"{request.Scheme}://{request.Host}";
    }
}
