using System.Security.Claims;
using Serilog.Context;

namespace Portal.Web.Middleware;

public class LoggingEnrichmentMiddleware
{
    private readonly RequestDelegate _next;

    public LoggingEnrichmentMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var businessIdClaim = context.User?.FindFirst("BusinessId")?.Value;
        int.TryParse(businessIdClaim, out var businessId);

        using (LogContext.PushProperty("UserId", userId))
        using (LogContext.PushProperty("BusinessId", businessId == 0 ? null : (object)businessId))
        {
            await _next(context);
        }
    }
}
