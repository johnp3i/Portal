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

// Feature: demo-access-invitations, Property 9: Pagination

/// <summary>
/// Property-based tests for DemoInvitationService.GetAllPagedAsync() pagination correctness.
/// For total N invitations and page P with size 10, returns at most 10 items at correct offset,
/// total count equals N.
/// **Validates: Requirements 10.4**
/// </summary>
public class DemoInvitationPaginationCorrectnessPropertyTests
{
    #region Test Infrastructure

    private const int PageSize = 10;

    /// <summary>
    /// Creates a DemoInvitationService with mocked dependencies for testing GetAllPagedAsync pagination.
    /// Returns the service and the repository mock for setup.
    /// </summary>
    private static (DemoInvitationService Service, Mock<DemoInvitationRepository> RepoMock) CreateService()
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        var dbContextOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"Pagination_{Guid.NewGuid()}")
            .Options;
        var portalDbContext = new PortalDbContext(dbContextOptions, tenantMock.Object);

        var mockRepository = new Mock<DemoInvitationRepository>(portalDbContext) { CallBase = false };
        var mockEmailService = new Mock<IEmailService>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockLogger = new Mock<ILogger<DemoInvitationService>>();

        var membershipDbContextOptions = new DbContextOptionsBuilder<MembershipDbContext>()
            .UseInMemoryDatabase(databaseName: $"Pagination_Membership_{Guid.NewGuid()}")
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
    /// Generates a total count N between 0 and 50.
    /// </summary>
    private static Gen<int> TotalCountGen()
    {
        return Gen.Choose(0, 50);
    }

    /// <summary>
    /// Generates a (totalCount, page) pair where page is valid (1 to max page based on N and pageSize=10).
    /// </summary>
    private static Gen<(int TotalCount, int Page)> TotalCountAndPageGen()
    {
        return TotalCountGen().SelectMany(n =>
        {
            var maxPage = n == 0 ? 1 : (int)Math.Ceiling((double)n / PageSize);
            return Gen.Choose(1, maxPage).Select(p => (TotalCount: n, Page: p));
        });
    }

    /// <summary>
    /// Creates a list of DemoInvitation entities of the specified count, ordered by CreatedAtUtc descending.
    /// </summary>
    private static List<DemoInvitation> CreateInvitations(int count)
    {
        var baseDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return Enumerable.Range(0, count)
            .Select(i => new DemoInvitation
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
                CreatedAtUtc = baseDate.AddMinutes(count - i) // descending order
            })
            .ToList();
    }

    /// <summary>
    /// Extracts the page slice from a full sorted list given page number and page size.
    /// </summary>
    private static List<DemoInvitation> GetPageSlice(List<DemoInvitation> allItems, int page, int pageSize)
    {
        return allItems
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }

    #endregion

    #region Property 9: Pagination Correctness

    /// <summary>
    /// Property 9: For total N invitations and page P with size 10, the result
    /// contains at most 10 items and TotalCount equals N.
    /// **Validates: Requirements 10.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetAllPagedAsync_ReturnsAtMost10Items_AndTotalCountEqualsN()
    {
        var arb = Arb.From(TotalCountAndPageGen());

        return Prop.ForAll(arb, async input =>
        {
            var (totalCount, page) = input;
            var (service, repoMock) = CreateService();

            // Create all invitations sorted descending
            var allInvitations = CreateInvitations(totalCount);

            // Get the correct page slice (what the repository would return)
            var pageSlice = GetPageSlice(allInvitations, page, PageSize);

            repoMock
                .Setup(r => r.GetPagedAsync(page, PageSize))
                .ReturnsAsync(pageSlice);

            repoMock
                .Setup(r => r.GetTotalCountAsync())
                .ReturnsAsync(totalCount);

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
            var result = await service.GetAllPagedAsync(page, PageSize);

            // Assert: items count is at most 10
            Assert.True(result.Items.Count <= PageSize,
                $"Expected at most {PageSize} items but got {result.Items.Count} for N={totalCount}, P={page}");

            // Assert: total count equals N
            Assert.Equal(totalCount, result.TotalCount);
        });
    }

    /// <summary>
    /// Property 9: For the last page, the number of items equals the remaining items: N - (P-1)*10.
    /// **Validates: Requirements 10.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetAllPagedAsync_LastPage_ReturnsRemainingItems()
    {
        // Generate only cases where N > 0 to ensure a meaningful last page
        var arb = Arb.From(Gen.Choose(1, 50).Select(n =>
        {
            var maxPage = (int)Math.Ceiling((double)n / PageSize);
            return (TotalCount: n, Page: maxPage);
        }));

        return Prop.ForAll(arb, async input =>
        {
            var (totalCount, page) = input;
            var (service, repoMock) = CreateService();

            // Create all invitations sorted descending
            var allInvitations = CreateInvitations(totalCount);

            // Get the correct last page slice
            var pageSlice = GetPageSlice(allInvitations, page, PageSize);
            var expectedCount = totalCount - (page - 1) * PageSize;

            repoMock
                .Setup(r => r.GetPagedAsync(page, PageSize))
                .ReturnsAsync(pageSlice);

            repoMock
                .Setup(r => r.GetTotalCountAsync())
                .ReturnsAsync(totalCount);

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
            var result = await service.GetAllPagedAsync(page, PageSize);

            // Assert: last page items count = N - (P-1)*10
            Assert.Equal(expectedCount, result.Items.Count);
        });
    }

    /// <summary>
    /// Property 9: Items correspond to the correct offset (P-1)*10 in the sorted list.
    /// Verifies that the service passes the correct page/pageSize to the repository.
    /// **Validates: Requirements 10.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetAllPagedAsync_ItemsAtCorrectOffset()
    {
        var arb = Arb.From(TotalCountAndPageGen());

        return Prop.ForAll(arb, async input =>
        {
            var (totalCount, page) = input;
            var (service, repoMock) = CreateService();

            // Create all invitations sorted descending
            var allInvitations = CreateInvitations(totalCount);

            // Get the expected page slice at offset (page-1)*pageSize
            var expectedSlice = GetPageSlice(allInvitations, page, PageSize);

            repoMock
                .Setup(r => r.GetPagedAsync(page, PageSize))
                .ReturnsAsync(expectedSlice);

            repoMock
                .Setup(r => r.GetTotalCountAsync())
                .ReturnsAsync(totalCount);

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
            var result = await service.GetAllPagedAsync(page, PageSize);

            // Assert: verify the repository was called with correct page and pageSize
            repoMock.Verify(r => r.GetPagedAsync(page, PageSize), Times.Once);

            // Assert: items match the expected emails from the slice
            for (int i = 0; i < result.Items.Count; i++)
            {
                var expectedEmail = expectedSlice[i].RecipientEmail;
                Assert.Equal(expectedEmail, result.Items[i].RecipientEmail);
            }
        });
    }

    #endregion
}
