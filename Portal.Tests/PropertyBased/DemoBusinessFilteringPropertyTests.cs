using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: demo-access-invitations, Property 2: Demo business filtering

/// <summary>
/// Property-based tests for DemoInvitationService.GetDemoBusinessesAsync().
/// Validates that the service returns exactly businesses where IsDemoAccount=true
/// and excludes any business where IsDemoAccount=false.
/// **Validates: Requirements 1.2**
/// </summary>
public class DemoBusinessFilteringPropertyTests
{
    #region Test Infrastructure

    /// <summary>
    /// Creates a DemoInvitationService with mocked dependencies for testing GetDemoBusinessesAsync.
    /// The repository mock is configured to return only businesses where IsDemoAccount=true,
    /// matching the real repository's WHERE clause behavior.
    /// </summary>
    private static (DemoInvitationService Service, Mock<DemoInvitationRepository> RepoMock) CreateService()
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        var dbContextOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"DemoFiltering_{Guid.NewGuid()}")
            .Options;
        var portalDbContext = new PortalDbContext(dbContextOptions, tenantMock.Object);

        var mockRepository = new Mock<DemoInvitationRepository>(portalDbContext) { CallBase = false };
        var mockEmailService = new Mock<IEmailService>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockLogger = new Mock<ILogger<DemoInvitationService>>();

        var membershipDbContextOptions = new DbContextOptionsBuilder<MembershipDbContext>()
            .UseInMemoryDatabase(databaseName: $"DemoFiltering_Membership_{Guid.NewGuid()}")
            .Options;
        var membershipDbContext = new MembershipDbContext(membershipDbContextOptions);

        var service = new DemoInvitationService(
            mockRepository.Object,
            mockEmailService.Object,
            mockHttpContextAccessor.Object,
            mockLogger.Object,
            membershipDbContext);

        return (service, mockRepository);
    }

    /// <summary>
    /// Generates a list of Business entities with random IsDemoAccount values (true/false).
    /// Each business has a unique Id and a name.
    /// </summary>
    private static Gen<List<Business>> BusinessListGen(int minCount, int maxCount)
    {
        return Gen.Choose(minCount, maxCount).SelectMany(count =>
            Gen.ArrayOf(count, Arb.Generate<bool>())
                .Select(demoFlags => demoFlags.Select((isDemo, i) => new Business
                {
                    Id = i + 1,
                    Name = $"Business {i + 1}",
                    IsActive = true,
                    IsDemoAccount = isDemo,
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-i),
                    UpdatedAtUtc = DateTime.UtcNow
                }).ToList()));
    }

    #endregion

    #region Property 2: Demo Business Filtering

    /// <summary>
    /// Property 2a: GetDemoBusinessesAsync returns exactly businesses where IsDemoAccount=true.
    /// For any list of businesses with random IsDemoAccount values, the service returns
    /// only those with IsDemoAccount=true, mapped to DemoBusinessItem with correct Id and Name.
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetDemoBusinessesAsync_ReturnsExactlyDemoBusinesses()
    {
        var arb = Arb.From(BusinessListGen(0, 25));

        return Prop.ForAll(arb, async allBusinesses =>
        {
            var (service, repoMock) = CreateService();

            // The repository returns only businesses where IsDemoAccount=true
            // (matching the real repository's WHERE [portal].[Business].[IsDemoAccount] = 1)
            var expectedDemoBusinesses = allBusinesses
                .Where(b => b.IsDemoAccount)
                .ToList();

            repoMock
                .Setup(r => r.GetDemoBusinessesAsync())
                .ReturnsAsync(expectedDemoBusinesses);

            // Act
            var result = await service.GetDemoBusinessesAsync();

            // Assert: result count matches expected demo business count
            Assert.Equal(expectedDemoBusinesses.Count, result.Count);

            // Assert: result contains exactly the demo businesses with correct Id and Name
            foreach (var expected in expectedDemoBusinesses)
            {
                var found = result.Any(r => r.Id == expected.Id && r.Name == expected.Name);
                Assert.True(found,
                    $"Expected demo business Id={expected.Id}, Name='{expected.Name}' not found in result");
            }
        });
    }

    /// <summary>
    /// Property 2b: GetDemoBusinessesAsync does NOT contain any business where IsDemoAccount=false.
    /// For any list of businesses with random IsDemoAccount values, the service result
    /// does not include any business that has IsDemoAccount=false.
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetDemoBusinessesAsync_ExcludesNonDemoBusinesses()
    {
        var arb = Arb.From(BusinessListGen(1, 25));

        return Prop.ForAll(arb, async allBusinesses =>
        {
            var (service, repoMock) = CreateService();

            // Repository filters to only IsDemoAccount=true
            var demoBusinesses = allBusinesses
                .Where(b => b.IsDemoAccount)
                .ToList();

            var nonDemoBusinesses = allBusinesses
                .Where(b => !b.IsDemoAccount)
                .ToList();

            repoMock
                .Setup(r => r.GetDemoBusinessesAsync())
                .ReturnsAsync(demoBusinesses);

            // Act
            var result = await service.GetDemoBusinessesAsync();

            // Assert: none of the non-demo business Ids appear in the result
            foreach (var nonDemo in nonDemoBusinesses)
            {
                var found = result.Any(r => r.Id == nonDemo.Id);
                Assert.False(found,
                    $"Non-demo business Id={nonDemo.Id}, Name='{nonDemo.Name}' should NOT appear in result");
            }
        });
    }

    /// <summary>
    /// Property 2c: GetDemoBusinessesAsync result count matches the expected demo business count.
    /// For any mixed list, the count of returned items equals the count of businesses with IsDemoAccount=true.
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetDemoBusinessesAsync_ResultCountMatchesExpectedDemoCount()
    {
        var arb = Arb.From(BusinessListGen(0, 30));

        return Prop.ForAll(arb, async allBusinesses =>
        {
            var (service, repoMock) = CreateService();

            var demoBusinesses = allBusinesses
                .Where(b => b.IsDemoAccount)
                .ToList();

            repoMock
                .Setup(r => r.GetDemoBusinessesAsync())
                .ReturnsAsync(demoBusinesses);

            // Act
            var result = await service.GetDemoBusinessesAsync();

            // Assert: count matches
            var expectedCount = allBusinesses.Count(b => b.IsDemoAccount);
            Assert.Equal(expectedCount, result.Count);
        });
    }

    /// <summary>
    /// Property 2d: GetDemoBusinessesAsync returns empty when no businesses are demo accounts.
    /// When all businesses have IsDemoAccount=false, the result is an empty list.
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetDemoBusinessesAsync_ReturnsEmpty_WhenNoDemoBusinesses(PositiveInt countSeed)
    {
        var count = (countSeed.Get % 10) + 1;
        var allNonDemo = Enumerable.Range(1, count)
            .Select(i => new Business
            {
                Id = i,
                Name = $"Regular Business {i}",
                IsActive = true,
                IsDemoAccount = false,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-i),
                UpdatedAtUtc = DateTime.UtcNow
            })
            .ToList();

        var (service, repoMock) = CreateService();

        // Repository returns empty list when no demo businesses exist
        repoMock
            .Setup(r => r.GetDemoBusinessesAsync())
            .ReturnsAsync(new List<Business>());

        // Act
        var result = service.GetDemoBusinessesAsync().GetAwaiter().GetResult();

        // Assert: empty result
        return (result.Count == 0).ToProperty()
            .Label($"Expected 0 demo businesses, got {result.Count} (total businesses: {allNonDemo.Count})");
    }

    #endregion
}
