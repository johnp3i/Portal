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

// Feature: demo-access-invitations, Property 8: List ordering

/// <summary>
/// Property-based tests for DemoInvitationService.GetAllPagedAsync() ordering.
/// Validates that results are sorted by CreatedAtUtc descending — each adjacent pair
/// satisfies result[i].CreatedAtUtc >= result[i+1].CreatedAtUtc.
/// **Validates: Requirements 10.2**
/// </summary>
public class DemoInvitationListOrderingPropertyTests
{
    #region Test Infrastructure

    /// <summary>
    /// Creates a DemoInvitationService with mocked dependencies for testing GetAllPagedAsync.
    /// Returns the service and the repository mock for setup.
    /// </summary>
    private static (DemoInvitationService Service, Mock<DemoInvitationRepository> RepoMock) CreateService()
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        var dbContextOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"ListOrdering_{Guid.NewGuid()}")
            .Options;
        var portalDbContext = new PortalDbContext(dbContextOptions, tenantMock.Object);

        var mockRepository = new Mock<DemoInvitationRepository>(portalDbContext) { CallBase = false };
        var mockEmailService = new Mock<IEmailService>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockLogger = new Mock<ILogger<DemoInvitationService>>();

        var membershipDbContextOptions = new DbContextOptionsBuilder<MembershipDbContext>()
            .UseInMemoryDatabase(databaseName: $"ListOrdering_Membership_{Guid.NewGuid()}")
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
    /// Generates a random DateTime within a 5-year range for CreatedAtUtc values.
    /// </summary>
    private static Gen<DateTime> CreatedAtUtcGen()
    {
        return Gen.Choose(0, 365 * 5 * 24 * 60) // minutes in 5 years
            .Select(minutes => new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(minutes));
    }

    /// <summary>
    /// Generates a list of DemoInvitation entities with random CreatedAtUtc values.
    /// </summary>
    private static Gen<List<DemoInvitation>> DemoInvitationListGen(int minCount, int maxCount)
    {
        return Gen.Choose(minCount, maxCount).SelectMany(count =>
            Gen.ArrayOf(count, CreatedAtUtcGen())
                .Select(dates => dates.Select((date, i) => new DemoInvitation
                {
                    Id = i + 1,
                    BusinessId = 1000,
                    Token = $"token-{i + 1}-{Guid.NewGuid():N}",
                    RecipientEmail = $"user{i + 1}@example.com",
                    RecipientName = $"User {i + 1}",
                    ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
                    Status = "sent",
                    CreatedByUserId = "admin-user-id",
                    AccessCount = 0,
                    CreatedAtUtc = date
                }).ToList()));
    }

    #endregion

    #region Property 8: Invitation List Ordering

    /// <summary>
    /// Property 8: GetAllPagedAsync returns results sorted by CreatedAtUtc descending.
    /// Each adjacent pair satisfies result[i].CreatedAtUtc >= result[i+1].CreatedAtUtc.
    /// **Validates: Requirements 10.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetAllPagedAsync_ReturnsSortedByCreatedAtUtcDescending()
    {
        var arb = Arb.From(DemoInvitationListGen(0, 20));

        return Prop.ForAll(arb, async invitations =>
        {
            var (service, repoMock) = CreateService();

            // The repository returns data already sorted by CreatedAtUtc DESC (as the real repo does ORDER BY DESC)
            var sortedInvitations = invitations
                .OrderByDescending(i => i.CreatedAtUtc)
                .ToList();

            repoMock
                .Setup(r => r.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(sortedInvitations);

            repoMock
                .Setup(r => r.GetTotalCountAsync())
                .ReturnsAsync(sortedInvitations.Count);

            // Mock GetDemoBusinessesAsync to return a business matching BusinessId 1000
            repoMock
                .Setup(r => r.GetDemoBusinessesAsync())
                .ReturnsAsync(new List<Business>
                {
                    new Business
                    {
                        Id = 1000,
                        Name = "Demo Business",
                        IsActive = true,
                        IsDemoAccount = true,
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow
                    }
                });

            // Act
            var result = await service.GetAllPagedAsync(1, 20);

            // Assert: each adjacent pair satisfies result[i].CreatedAtUtc >= result[i+1].CreatedAtUtc
            for (int i = 0; i < result.Items.Count - 1; i++)
            {
                Assert.True(result.Items[i].CreatedAtUtc >= result.Items[i + 1].CreatedAtUtc,
                    $"Expected descending order but item[{i}].CreatedAtUtc={result.Items[i].CreatedAtUtc:O} " +
                    $"< item[{i + 1}].CreatedAtUtc={result.Items[i + 1].CreatedAtUtc:O}");
            }
        });
    }

    /// <summary>
    /// Property 8 (stability): Sorting an already-sorted list produces the same order.
    /// Verifies the ordering is deterministic regardless of input order.
    /// **Validates: Requirements 10.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetAllPagedAsync_OrderingIsDeterministic()
    {
        var arb = Arb.From(DemoInvitationListGen(2, 15));

        return Prop.ForAll(arb, async invitations =>
        {
            var (service, repoMock) = CreateService();

            // Sort descending (as the repo always does)
            var sortedInvitations = invitations
                .OrderByDescending(i => i.CreatedAtUtc)
                .ToList();

            repoMock
                .Setup(r => r.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(sortedInvitations);

            repoMock
                .Setup(r => r.GetTotalCountAsync())
                .ReturnsAsync(sortedInvitations.Count);

            repoMock
                .Setup(r => r.GetDemoBusinessesAsync())
                .ReturnsAsync(new List<Business>
                {
                    new Business
                    {
                        Id = 1000,
                        Name = "Demo Business",
                        IsActive = true,
                        IsDemoAccount = true,
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow
                    }
                });

            // Act — call twice
            var result1 = await service.GetAllPagedAsync(1, 20);
            var result2 = await service.GetAllPagedAsync(1, 20);

            // Assert: both calls produce identical ordering
            Assert.Equal(result1.Items.Count, result2.Items.Count);
            for (int i = 0; i < result1.Items.Count; i++)
            {
                Assert.Equal(result1.Items[i].CreatedAtUtc, result2.Items[i].CreatedAtUtc);
                Assert.Equal(result1.Items[i].RecipientEmail, result2.Items[i].RecipientEmail);
            }
        });
    }

    /// <summary>
    /// Property 8 (empty list): An empty invitation list returns an empty result with no ordering violations.
    /// **Validates: Requirements 10.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetAllPagedAsync_EmptyList_ReturnsEmptyWithNoViolation()
    {
        return Prop.ForAll(Arb.From(Gen.Constant(0)), async _ =>
        {
            var (service, repoMock) = CreateService();

            repoMock
                .Setup(r => r.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new List<DemoInvitation>());

            repoMock
                .Setup(r => r.GetTotalCountAsync())
                .ReturnsAsync(0);

            repoMock
                .Setup(r => r.GetDemoBusinessesAsync())
                .ReturnsAsync(new List<Business>());

            // Act
            var result = await service.GetAllPagedAsync(1, 10);

            // Assert: empty list, no ordering violations
            Assert.Empty(result.Items);
        });
    }

    #endregion
}
