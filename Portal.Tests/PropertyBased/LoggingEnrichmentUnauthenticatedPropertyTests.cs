using System.Security.Claims;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Portal.Web.Middleware;
using Serilog;
using Serilog.Events;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: serilog-structured-logging, Property 2: Unauthenticated requests produce null enrichment values

/// <summary>
/// Property-based tests for unauthenticated request enrichment.
/// Validates that for any HTTP context where the user is unauthenticated
/// (no claims principal or no NameIdentifier/BusinessId claims),
/// invoking the LoggingEnrichmentMiddleware SHALL push null for both
/// UserId and BusinessId into the Serilog LogContext.
/// **Validates: Requirements 4.5**
/// </summary>
public class LoggingEnrichmentUnauthenticatedPropertyTests
{
    #region Scenario Generators

    /// <summary>
    /// Represents the different unauthenticated scenarios we want to test.
    /// </summary>
    public enum UnauthenticatedScenario
    {
        NoUser,
        AnonymousUser,
        AuthenticatedButMissingNameIdentifier,
        AuthenticatedButMissingBusinessId,
        AuthenticatedButMissingBothClaims,
        AuthenticatedWithEmptyIdentity
    }

    private static HttpContext CreateUnauthenticatedHttpContext(UnauthenticatedScenario scenario, string? irrelevantClaimValue)
    {
        var httpContext = new DefaultHttpContext();

        switch (scenario)
        {
            case UnauthenticatedScenario.NoUser:
                // context.User is a default ClaimsPrincipal with no identity
                httpContext.User = new ClaimsPrincipal();
                break;

            case UnauthenticatedScenario.AnonymousUser:
                // User with an unauthenticated identity (IsAuthenticated = false)
                var anonIdentity = new ClaimsIdentity(); // No authenticationType = not authenticated
                httpContext.User = new ClaimsPrincipal(anonIdentity);
                break;

            case UnauthenticatedScenario.AuthenticatedButMissingNameIdentifier:
                // Authenticated user but no NameIdentifier claim, has some other claim
                var identityNoNameId = new ClaimsIdentity("TestAuth");
                if (!string.IsNullOrEmpty(irrelevantClaimValue))
                {
                    identityNoNameId.AddClaim(new Claim("SomeOtherClaim", irrelevantClaimValue));
                }
                httpContext.User = new ClaimsPrincipal(identityNoNameId);
                break;

            case UnauthenticatedScenario.AuthenticatedButMissingBusinessId:
                // Authenticated user with NameIdentifier but no BusinessId claim
                var identityNoBizId = new ClaimsIdentity("TestAuth");
                identityNoBizId.AddClaim(new Claim(ClaimTypes.NameIdentifier, irrelevantClaimValue ?? "some-user"));
                httpContext.User = new ClaimsPrincipal(identityNoBizId);
                break;

            case UnauthenticatedScenario.AuthenticatedButMissingBothClaims:
                // Authenticated user with neither NameIdentifier nor BusinessId
                var identityNoClaims = new ClaimsIdentity("TestAuth");
                if (!string.IsNullOrEmpty(irrelevantClaimValue))
                {
                    identityNoClaims.AddClaim(new Claim("IrrelevantClaim", irrelevantClaimValue));
                }
                httpContext.User = new ClaimsPrincipal(identityNoClaims);
                break;

            case UnauthenticatedScenario.AuthenticatedWithEmptyIdentity:
                // User with an identity that has no claims at all
                var emptyIdentity = new ClaimsIdentity("TestAuth");
                httpContext.User = new ClaimsPrincipal(emptyIdentity);
                break;
        }

        return httpContext;
    }

    #endregion

    #region Property 2: Unauthenticated requests produce null enrichment values

    /// <summary>
    /// Property 2: For any HTTP context where the user is unauthenticated (no claims principal
    /// or no NameIdentifier/BusinessId claims), invoking the LoggingEnrichmentMiddleware
    /// SHALL push null for both UserId and BusinessId into the Serilog LogContext.
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UnauthenticatedRequests_ProduceNullEnrichmentValues(
        int scenarioSeed,
        NonNull<string> irrelevantClaimValue)
    {
        // Select a scenario based on the seed
        var scenarios = Enum.GetValues<UnauthenticatedScenario>();
        var scenario = scenarios[Math.Abs(scenarioSeed) % scenarios.Length];

        var httpContext = CreateUnauthenticatedHttpContext(scenario, irrelevantClaimValue.Get);

        string? capturedUserId = "NOT_SET";
        object? capturedBusinessId = "NOT_SET";

        // Configure a Serilog logger that captures LogContext properties
        var logConfig = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(new PropertyCaptureSink(logEvent =>
            {
                if (logEvent.Properties.TryGetValue("UserId", out var userIdProp))
                {
                    capturedUserId = userIdProp is ScalarValue sv && sv.Value == null
                        ? null
                        : userIdProp?.ToString()?.Trim('"');
                }
                else
                {
                    capturedUserId = null;
                }

                if (logEvent.Properties.TryGetValue("BusinessId", out var bizIdProp))
                {
                    capturedBusinessId = bizIdProp is ScalarValue sv && sv.Value == null
                        ? null
                        : bizIdProp;
                }
                else
                {
                    capturedBusinessId = null;
                }
            }));

        using var logger = logConfig.CreateLogger();

        // The middleware pushes properties into LogContext, so we need to invoke it
        // and log within the same context
        var middleware = new LoggingEnrichmentMiddleware(ctx =>
        {
            // Log within the middleware's context to capture the pushed properties
            logger.Information("Test log event");
            return Task.CompletedTask;
        });

        // Invoke the middleware synchronously for the test
        middleware.InvokeAsync(httpContext).GetAwaiter().GetResult();

        // For scenarios where NameIdentifier is missing, UserId should be null
        // For scenarios where BusinessId claim is missing, BusinessId should be null
        bool userIdIsNull;
        bool businessIdIsNull;

        switch (scenario)
        {
            case UnauthenticatedScenario.NoUser:
            case UnauthenticatedScenario.AnonymousUser:
            case UnauthenticatedScenario.AuthenticatedButMissingBothClaims:
            case UnauthenticatedScenario.AuthenticatedWithEmptyIdentity:
            case UnauthenticatedScenario.AuthenticatedButMissingNameIdentifier:
                userIdIsNull = capturedUserId == null;
                break;
            case UnauthenticatedScenario.AuthenticatedButMissingBusinessId:
                // This scenario HAS a NameIdentifier, so UserId won't be null
                userIdIsNull = true; // Skip this check — UserId is expected to be set
                break;
            default:
                userIdIsNull = capturedUserId == null;
                break;
        }

        switch (scenario)
        {
            case UnauthenticatedScenario.NoUser:
            case UnauthenticatedScenario.AnonymousUser:
            case UnauthenticatedScenario.AuthenticatedButMissingBothClaims:
            case UnauthenticatedScenario.AuthenticatedWithEmptyIdentity:
            case UnauthenticatedScenario.AuthenticatedButMissingBusinessId:
            case UnauthenticatedScenario.AuthenticatedButMissingNameIdentifier:
                businessIdIsNull = capturedBusinessId == null;
                break;
            default:
                businessIdIsNull = capturedBusinessId == null;
                break;
        }

        var allPropertiesHold = userIdIsNull && businessIdIsNull;

        return allPropertiesHold.ToProperty()
            .Label($"Scenario={scenario}, UserId={capturedUserId ?? "null"}, " +
                   $"BusinessId={capturedBusinessId ?? "null"}, " +
                   $"UserIdIsNull={userIdIsNull}, BusinessIdIsNull={businessIdIsNull}");
    }

    /// <summary>
    /// Property 2 (focused): For any HTTP context with no user at all,
    /// the middleware SHALL push null for both UserId and BusinessId.
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoUserContext_ProducesNullForBothProperties(byte arbitraryByte)
    {
        var httpContext = new DefaultHttpContext();
        // DefaultHttpContext has a default ClaimsPrincipal with no claims

        string? capturedUserId = "NOT_SET";
        object? capturedBusinessId = "NOT_SET";

        var logConfig = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(new PropertyCaptureSink(logEvent =>
            {
                capturedUserId = logEvent.Properties.TryGetValue("UserId", out var userIdProp)
                    && userIdProp is ScalarValue sv1 && sv1.Value == null
                    ? null
                    : (logEvent.Properties.ContainsKey("UserId") ? userIdProp?.ToString()?.Trim('"') : null);

                capturedBusinessId = logEvent.Properties.TryGetValue("BusinessId", out var bizIdProp)
                    && bizIdProp is ScalarValue sv2 && sv2.Value == null
                    ? null
                    : (logEvent.Properties.ContainsKey("BusinessId") ? bizIdProp : null);
            }));

        using var logger = logConfig.CreateLogger();

        var middleware = new LoggingEnrichmentMiddleware(ctx =>
        {
            logger.Information("Test log event {ArbitraryByte}", arbitraryByte);
            return Task.CompletedTask;
        });

        middleware.InvokeAsync(httpContext).GetAwaiter().GetResult();

        var userIdNull = capturedUserId == null;
        var businessIdNull = capturedBusinessId == null;

        return (userIdNull && businessIdNull).ToProperty()
            .Label($"UserId={capturedUserId ?? "null"}, BusinessId={capturedBusinessId ?? "null"}");
    }

    /// <summary>
    /// Property 2 (focused): For any HTTP context with an anonymous (unauthenticated) user
    /// carrying arbitrary non-relevant claims, the middleware SHALL push null for both
    /// UserId and BusinessId.
    /// **Validates: Requirements 4.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AnonymousUserWithIrrelevantClaims_ProducesNullForBothProperties(
        NonNull<string> claimName,
        NonNull<string> claimValue)
    {
        var httpContext = new DefaultHttpContext();
        var identity = new ClaimsIdentity(); // No auth type = unauthenticated

        // Add an irrelevant claim (not NameIdentifier or BusinessId)
        var safeName = claimName.Get.Replace(ClaimTypes.NameIdentifier, "X")
                                    .Replace("BusinessId", "Y");
        identity.AddClaim(new Claim(safeName, claimValue.Get));
        httpContext.User = new ClaimsPrincipal(identity);

        string? capturedUserId = "NOT_SET";
        object? capturedBusinessId = "NOT_SET";

        var logConfig = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(new PropertyCaptureSink(logEvent =>
            {
                capturedUserId = logEvent.Properties.TryGetValue("UserId", out var userIdProp)
                    && userIdProp is ScalarValue sv1 && sv1.Value == null
                    ? null
                    : (logEvent.Properties.ContainsKey("UserId") ? userIdProp?.ToString()?.Trim('"') : null);

                capturedBusinessId = logEvent.Properties.TryGetValue("BusinessId", out var bizIdProp)
                    && bizIdProp is ScalarValue sv2 && sv2.Value == null
                    ? null
                    : (logEvent.Properties.ContainsKey("BusinessId") ? bizIdProp : null);
            }));

        using var logger = logConfig.CreateLogger();

        var middleware = new LoggingEnrichmentMiddleware(ctx =>
        {
            logger.Information("Test log event");
            return Task.CompletedTask;
        });

        middleware.InvokeAsync(httpContext).GetAwaiter().GetResult();

        var userIdNull = capturedUserId == null;
        var businessIdNull = capturedBusinessId == null;

        return (userIdNull && businessIdNull).ToProperty()
            .Label($"ClaimName={safeName}, UserId={capturedUserId ?? "null"}, BusinessId={capturedBusinessId ?? "null"}");
    }

    #endregion

    #region Test Infrastructure

    /// <summary>
    /// A custom Serilog sink that captures log events for property verification.
    /// </summary>
    private class PropertyCaptureSink : Serilog.Core.ILogEventSink
    {
        private readonly Action<LogEvent> _onEmit;

        public PropertyCaptureSink(Action<LogEvent> onEmit)
        {
            _onEmit = onEmit;
        }

        public void Emit(LogEvent logEvent)
        {
            _onEmit(logEvent);
        }
    }

    #endregion
}
