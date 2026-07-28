using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Portal.Web.Configuration;
using Portal.Web.Services.Stripe;
using Serilog;
using Stripe;

namespace Portal.Web.Controllers;

/// <summary>
/// Public webhook endpoint for Stripe Connect events.
/// Handles checkout.session.completed and checkout.session.expired.
/// No authentication — verified via Stripe webhook signature.
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("stripe")]
public class StripeConnectWebhookController : ControllerBase
{
    private readonly IStripeConnectService _stripeConnectService;
    private readonly StripeSettings _stripeSettings;

    public StripeConnectWebhookController(
        IStripeConnectService stripeConnectService,
        IOptions<StripeSettings> stripeSettings)
    {
        _stripeConnectService = stripeConnectService;
        _stripeSettings = stripeSettings.Value;
    }

    /// <summary>
    /// POST /stripe/connect-webhook
    /// Receives and processes Stripe Connect webhook events.
    /// </summary>
    [HttpPost("connect-webhook")]
    public async Task<IActionResult> HandleWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"].ToString();

        if (string.IsNullOrEmpty(signature))
        {
            Log.Warning("Stripe Connect webhook received without signature header");
            return BadRequest("Missing Stripe-Signature header.");
        }

        Event stripeEvent;

        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                json,
                signature,
                _stripeSettings.ConnectWebhookSecret);
        }
        catch (StripeException ex)
        {
            Log.Warning(ex, "Stripe Connect webhook signature verification failed");
            return BadRequest("Invalid webhook signature.");
        }

        try
        {
            switch (stripeEvent.Type)
            {
                case "checkout.session.completed":
                    await HandleCheckoutSessionCompleted(stripeEvent);
                    break;

                case "checkout.session.expired":
                    await HandleCheckoutSessionExpired(stripeEvent);
                    break;

                default:
                    Log.Information("Stripe Connect webhook received unhandled event type: {EventType}", stripeEvent.Type);
                    break;
            }

            return Ok();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Stripe Connect webhook processing failed for event {EventId} ({EventType})",
                stripeEvent.Id, stripeEvent.Type);
            // Return 200 to prevent Stripe from retrying (we've logged the error)
            // A 500 would cause Stripe to retry, potentially creating duplicate processing attempts
            return Ok();
        }
    }

    private async Task HandleCheckoutSessionCompleted(Event stripeEvent)
    {
        var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
        if (session == null)
        {
            Log.Warning("checkout.session.completed event had null session object");
            return;
        }

        var stripeSessionId = session.Id;
        var paymentIntentId = session.PaymentIntentId;

        if (string.IsNullOrEmpty(stripeSessionId) || string.IsNullOrEmpty(paymentIntentId))
        {
            Log.Warning("checkout.session.completed missing sessionId or paymentIntentId");
            return;
        }

        Log.Information("Processing checkout.session.completed: SessionId={SessionId}, PaymentIntentId={PaymentIntentId}",
            stripeSessionId, paymentIntentId);

        var result = await _stripeConnectService.HandleCheckoutCompletedAsync(stripeSessionId, paymentIntentId);

        if (result.Success)
        {
            Log.Information("Stripe Connect payment recorded successfully for session {SessionId}", stripeSessionId);
        }
        else
        {
            Log.Warning("Stripe Connect HandleCheckoutCompleted returned failure for session {SessionId}: {Message}",
                stripeSessionId, result.Message);
        }
    }

    private async Task HandleCheckoutSessionExpired(Event stripeEvent)
    {
        var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
        if (session == null) return;

        Log.Information("Checkout session expired: SessionId={SessionId}", session.Id);
        await _stripeConnectService.HandleCheckoutExpiredAsync(session.Id);
    }
}
