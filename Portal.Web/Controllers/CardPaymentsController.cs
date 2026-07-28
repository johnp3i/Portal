using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Services;
using Portal.Web.Services.Stripe;

namespace Portal.Web.Controllers;

/// <summary>
/// Business-facing Card Payments view — shows all Stripe card payments with fee transparency.
/// Route: /Revenue/CardPayments
/// </summary>
[Authorize]
[Route("Revenue/CardPayments")]
public class CardPaymentsController : Controller
{
    private readonly IStripeConnectService _stripeConnectService;
    private readonly ICurrentTenantService _tenantService;

    public CardPaymentsController(
        IStripeConnectService stripeConnectService,
        ICurrentTenantService tenantService)
    {
        _stripeConnectService = stripeConnectService;
        _tenantService = tenantService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? period)
    {
        var businessId = _tenantService.CurrentBusinessId;

        // Check if Stripe is connected
        var isConnected = await _stripeConnectService.IsConnectedAsync(businessId);
        ViewBag.IsStripeConnected = isConnected;

        if (!isConnected)
        {
            ViewBag.Sessions = new List<Portal.Infrastructure.Entities.StripeCheckoutSession>();
            ViewBag.Period = period ?? "this_month";
            return View();
        }

        // Determine date range from period filter
        var now = DateTime.UtcNow;
        DateTime? fromUtc = null;
        DateTime? toUtc = null;

        switch (period)
        {
            case "last_month":
                var lastMonth = now.AddMonths(-1);
                fromUtc = new DateTime(lastMonth.Year, lastMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                toUtc = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                break;
            case "last_3":
                fromUtc = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-3);
                toUtc = null;
                break;
            case "this_year":
                fromUtc = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                toUtc = null;
                break;
            case "all":
                fromUtc = null;
                toUtc = null;
                break;
            default: // this_month
                period = "this_month";
                fromUtc = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                toUtc = null;
                break;
        }

        var sessions = await _stripeConnectService.GetCompletedSessionsAsync(businessId, fromUtc, toUtc);

        ViewBag.Sessions = sessions;
        ViewBag.Period = period;
        ViewBag.TotalGross = sessions.Sum(s => s.Amount);
        ViewBag.TotalFees = sessions.Sum(s => s.StripeFeeAmount ?? 0m);
        ViewBag.TotalNet = sessions.Sum(s => s.NetAmount ?? s.Amount);
        ViewBag.TransactionCount = sessions.Count;

        return View();
    }
}
