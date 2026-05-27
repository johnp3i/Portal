using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.Integration;

/// <summary>
/// Smoke test: verifies the application starts and serves HTTP requests
/// even when the logging database (LoggingDb) is unreachable.
/// This confirms that a logging infrastructure failure does not crash the application.
///
/// **Validates: Requirements 6.1, 6.2**
/// </summary>
[Trait("Category", "Integration")]
public class LoggingResilienceSmokeTests : IClassFixture<LoggingResilienceSmokeTests.InvalidLoggingDbFactory>
{
    private readonly HttpClient _client;

    public LoggingResilienceSmokeTests(InvalidLoggingDbFactory factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Verifies the application starts successfully even when the LoggingDb
    /// connection string points to a non-existent server.
    /// **Validates: Requirement 6.1**
    /// </summary>
    [Fact]
    public async Task Application_StartsSuccessfully_WhenLoggingDbIsUnreachable()
    {
        // Act: make a request to the root — if the app started, this will return a response
        var response = await _client.GetAsync("/");

        // Assert: the app is running and serving responses (redirect to login is expected for unauthenticated)
        Assert.NotNull(response);
        Assert.True(
            (int)response.StatusCode < 500,
            $"Expected a non-server-error response but got {response.StatusCode}");
    }

    /// <summary>
    /// Verifies that HTTP requests are served without error when the logging database
    /// is unreachable — the Console and File sinks continue operating independently.
    /// **Validates: Requirements 6.1, 6.2**
    /// </summary>
    [Fact]
    public async Task Application_ServesHttpRequests_WhenLoggingDbIsUnreachable()
    {
        // Act: make multiple requests to verify the app remains stable
        var response1 = await _client.GetAsync("/Account/Login");
        var response2 = await _client.GetAsync("/Account/Login");

        // Assert: both requests succeed without server errors
        Assert.True(
            (int)response1.StatusCode < 500,
            $"First request returned server error: {response1.StatusCode}");
        Assert.True(
            (int)response2.StatusCode < 500,
            $"Second request returned server error: {response2.StatusCode}");
    }

    /// <summary>
    /// Custom WebApplicationFactory that configures an invalid LoggingDb connection string
    /// and replaces real database contexts with in-memory providers to isolate the test
    /// from external SQL Server dependencies.
    /// </summary>
    public class InvalidLoggingDbFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");

            builder.ConfigureAppConfiguration((context, config) =>
            {
                // Override LoggingDb with an invalid connection string pointing to a non-existent server
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:LoggingDb"] = "Server=non-existent-server-12345.invalid;Database=Portal.Logging;Connection Timeout=1;",
                    ["ConnectionStrings:PortalDb"] = "Server=non-existent-server-12345.invalid;Database=Portal;Connection Timeout=1;",
                    ["ConnectionStrings:MembershipDb"] = "Server=non-existent-server-12345.invalid;Database=Membership;Connection Timeout=1;",
                    ["SkipSeedData"] = "true"
                });
            });

            builder.ConfigureServices(services =>
            {
                // Remove real DbContext registrations
                services.RemoveAll<DbContextOptions<PortalDbContext>>();
                services.RemoveAll<DbContextOptions<MembershipDbContext>>();
                services.RemoveAll<PortalDbContext>();
                services.RemoveAll<MembershipDbContext>();

                // Add in-memory database contexts for PortalDb and MembershipDb
                services.AddDbContext<PortalDbContext>((sp, options) =>
                    options.UseInMemoryDatabase($"SmokeTest_PortalDb_{Guid.NewGuid()}"));

                services.AddDbContext<MembershipDbContext>(options =>
                    options.UseInMemoryDatabase($"SmokeTest_MembershipDb_{Guid.NewGuid()}"));

                // Ensure ICurrentTenantService is available for PortalDbContext
                services.RemoveAll<ICurrentTenantService>();
                services.AddScoped<ICurrentTenantService, StubTenantService>();
            });
        }
    }

    /// <summary>
    /// Stub tenant service for smoke tests — returns a default business ID.
    /// </summary>
    private class StubTenantService : ICurrentTenantService
    {
        public int CurrentBusinessId => 1;
    }
}
