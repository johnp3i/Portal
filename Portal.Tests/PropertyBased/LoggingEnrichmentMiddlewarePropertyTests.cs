using System.Security.Claims;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Portal.Web.Middleware;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: serilog-structured-logging, Property 1: Enrichment middleware preserves claim values in LogContext

/// <summary>
/// Property-based tests for LoggingEnrichmentMiddleware claim value preservation.
/// Validates that for any HTTP context with an authenticated user carrying a UserId claim
/// (any non-empty string) and a BusinessId claim (any valid integer string), invoking the
/// LoggingEnrichmentMiddleware SHALL push those exact values into the Serilog LogContext
/// such that downstream log events contain UserId equal to the claim value and BusinessId
/// equal to the parsed integer.
/// **Validates: Requirements 4.3, 4.4**
/// </summary>
public class LoggingEnrichmentMiddlewarePropertyTests
{
    /// <summary>
    /// Property 1: Enrichment middleware preserves claim values in LogContext.
    /// For any non-empty UserId string and any positive BusinessId integer,
    /// the middleware pushes exact values into LogContext.
    /// **Validates: Requirements 4.3, 4.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Middleware_PreservesClaimValues_InLogContext(NonEmptyString userId, PositiveInt businessId)
    {
        var userIdValue = userId.Get;
        var businessIdValue = businessId.Get;

        // Capture log event properties written during middleware execution
        LogEvent? capturedEvent = null;

        var logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(new DelegateSink(evt => capturedEvent = evt))
            .CreateLogger();

        // Create HttpContext with claims
        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userIdValue),
            new Claim("BusinessId", businessIdValue.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);

        // The middleware's RequestDelegate will log an event so we can capture LogContext properties
        var middleware = new LoggingEnrichmentMiddleware(async ctx =>
        {
            logger.Information("Test log event");
            await Task.CompletedTask;
        });

        // Invoke middleware synchronously for test
        middleware.InvokeAsync(httpContext).GetAwaiter().GetResult();

        // Verify captured properties
        var hasUserId = capturedEvent?.Properties.ContainsKey("UserId") == true;
        var hasBusinessId = capturedEvent?.Properties.ContainsKey("BusinessId") == true;

        var userIdMatches = hasUserId &&
            capturedEvent!.Properties["UserId"] is ScalarValue userIdScalar &&
            userIdScalar.Value?.ToString() == userIdValue;

        var businessIdMatches = hasBusinessId &&
            capturedEvent!.Properties["BusinessId"] is ScalarValue businessIdScalar &&
            businessIdScalar.Value is int capturedBusinessId &&
            capturedBusinessId == businessIdValue;

        var allPropertiesHold = userIdMatches && businessIdMatches;

        return allPropertiesHold.ToProperty()
            .Label($"UserId='{userIdValue}' matched={userIdMatches}, " +
                   $"BusinessId={businessIdValue} matched={businessIdMatches}");
    }

    /// <summary>
    /// A simple Serilog sink that delegates log events to a callback for test capture.
    /// </summary>
    private class DelegateSink : Serilog.Core.ILogEventSink
    {
        private readonly Action<LogEvent> _callback;

        public DelegateSink(Action<LogEvent> callback)
        {
            _callback = callback;
        }

        public void Emit(LogEvent logEvent)
        {
            _callback(logEvent);
        }
    }
}
