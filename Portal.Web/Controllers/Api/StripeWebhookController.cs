using Microsoft.AspNetCore.Mvc;
using Portal.Web.Services.Stripe;

namespace Portal.Web.Controllers.Api;

/// <summary>
/// Receives and processes Stripe webhook events.
/// No [Authorize] attribute — Stripe calls this endpoint directly.
/// Signature verification is handled by the WebhookProcessingService.
/// </summary>
[ApiController]
[Route("api/webhooks/stripe")]
public class StripeWebhookController : ControllerBase
{
    private readonly IWebhookProcessingService _webhookProcessingService;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(
        IWebhookProcessingService webhookProcessingService,
        ILogger<StripeWebhookController> logger)
    {
        _webhookProcessingService = webhookProcessingService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> HandleWebhook()
    {
        string json;
        try
        {
            using var reader = new StreamReader(Request.Body);
            json = await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read webhook request body");
            return BadRequest();
        }

        var signatureHeader = Request.Headers["Stripe-Signature"].FirstOrDefault() ?? string.Empty;

        try
        {
            var statusCode = await _webhookProcessingService.ProcessEventAsync(json, signatureHeader);

            return StatusCode(statusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in webhook processing");
            return StatusCode(500);
        }
    }
}
