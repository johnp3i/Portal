using System.ComponentModel.DataAnnotations;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: demo-access-invitations, Property 6: Invitation Creation Validation

/// <summary>
/// Property-based tests for DemoInvitationService.CreateAsync validation.
/// Invalid email, non-demo business, past expiry, or no granted permissions SHALL reject
/// and not persist any record.
/// **Validates: Requirements 5.2**
/// </summary>
public class DemoInvitationCreationValidationPropertyTests
{
    private const string CreatedByUserId = "admin-user-id";

    #region Test Infrastructure

    /// <summary>
    /// Known demo businesses returned by the mock repository.
    /// Only BusinessId 1000 and 2000 are demo businesses.
    /// </summary>
    private static readonly List<Business> KnownDemoBusinesses = new()
    {
        new Business { Id = 1000, Name = "Demo Coffee Roasters", IsActive = true, IsDemoAccount = true, CreatedAtUtc = DateTime.UtcNow },
        new Business { Id = 2000, Name = "Demo Tech Solutions", IsActive = true, IsDemoAccount = true, CreatedAtUtc = DateTime.UtcNow }
    };

    /// <summary>
    /// Creates a DemoInvitationService with a mock repository that tracks InsertAsync calls.
    /// The repository mock returns the known demo businesses and never has token collisions.
    /// </summary>
    private static (DemoInvitationService Service, Mock<DemoInvitationRepository> RepoMock) CreateServiceWithMocks()
    {
        var dbContextOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase($"DemoInvValidation_{Guid.NewGuid()}")
            .Options;
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(1000);
        var dbContext = new PortalDbContext(dbContextOptions, tenantMock.Object);

        var repoMock = new Mock<DemoInvitationRepository>(MockBehavior.Loose, dbContext);

        // GetDemoBusinessesAsync returns known demo businesses
        repoMock.Setup(r => r.GetDemoBusinessesAsync())
            .ReturnsAsync(KnownDemoBusinesses);

        // GetByTokenAsync returns null (no collision)
        repoMock.Setup(r => r.GetByTokenAsync(It.IsAny<string>()))
            .ReturnsAsync((DemoInvitation?)null);

        // InsertAsync does nothing (we verify it's never called for invalid requests)
        repoMock.Setup(r => r.InsertAsync(It.IsAny<DemoInvitation>(), It.IsAny<List<DemoInvitationPermission>>()))
            .Returns(Task.CompletedTask);

        var emailMock = new Mock<IEmailService>();
        emailMock.Setup(e => e.SendDemoInvitationEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var loggerMock = new Mock<ILogger<DemoInvitationService>>();

        var membershipDbOptions = new DbContextOptionsBuilder<MembershipDbContext>()
            .UseInMemoryDatabase($"Membership_{Guid.NewGuid()}")
            .Options;
        var membershipDbContext = new MembershipDbContext(membershipDbOptions);

        var service = new DemoInvitationService(
            repoMock.Object,
            emailMock.Object,
            httpContextAccessorMock.Object,
            loggerMock.Object,
            membershipDbContext);

        return (service, repoMock);
    }

    /// <summary>
    /// Generates a valid set of permissions (at least one with 'full' or 'readonly')
    /// for use when testing non-permission validation failures.
    /// </summary>
    private static List<ModulePermissionEntry> ValidPermissions() => new()
    {
        new ModulePermissionEntry { Module = PortalModules.Invoice, AccessLevel = AccessLevels.Full },
        new ModulePermissionEntry { Module = PortalModules.Customer, AccessLevel = AccessLevels.ReadOnly }
    };

    #endregion

    #region Property 6: Invalid email rejects and does not persist

    /// <summary>
    /// Property 6a: For any invalid email format (missing @, no domain, empty, whitespace-only),
    /// CreateAsync SHALL throw ValidationException and InsertAsync SHALL NOT be called.
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvalidEmail_Rejects_AndDoesNotPersist(PositiveInt caseSeed)
    {
        // Generate various invalid email patterns
        var invalidEmails = new[]
        {
            "",                          // empty
            "   ",                       // whitespace only
            "nodomainemail",             // no @ sign
            "missing@",                  // no domain after @
            "@nodomain.com",             // no local part
            "spaces in@email.com",       // spaces in local part
            "no.dot@domain",             // missing TLD dot
            $"user{caseSeed.Get % 100}", // random string without @
            $"bad{caseSeed.Get}@",       // ends with @
            $"@bad{caseSeed.Get}.com",   // starts with @, no local part
        };

        var email = invalidEmails[caseSeed.Get % invalidEmails.Length];
        var (service, repoMock) = CreateServiceWithMocks();

        var request = new CreateDemoInvitationRequest
        {
            BusinessId = 1000, // valid demo business
            RecipientEmail = email,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7), // valid future expiry
            Permissions = ValidPermissions()
        };

        var threwValidation = false;
        try
        {
            service.CreateAsync(request, CreatedByUserId).GetAwaiter().GetResult();
        }
        catch (ValidationException)
        {
            threwValidation = true;
        }

        // Verify InsertAsync was never called
        repoMock.Verify(
            r => r.InsertAsync(It.IsAny<DemoInvitation>(), It.IsAny<List<DemoInvitationPermission>>()),
            Times.Never);

        return threwValidation.ToProperty()
            .Label($"Email='{email}' should throw ValidationException but threwValidation={threwValidation}");
    }

    #endregion

    #region Property 6: Non-demo business rejects and does not persist

    /// <summary>
    /// Property 6b: For any BusinessId that doesn't match a known demo business,
    /// CreateAsync SHALL throw ValidationException and InsertAsync SHALL NOT be called.
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonDemoBusiness_Rejects_AndDoesNotPersist(PositiveInt businessIdSeed)
    {
        // Generate a BusinessId that is NOT in our known demo businesses (1000, 2000)
        var invalidBusinessId = (businessIdSeed.Get % 900) + 1; // 1-900, never 1000 or 2000
        if (invalidBusinessId == 1000 || invalidBusinessId == 2000)
            invalidBusinessId = 999;

        var (service, repoMock) = CreateServiceWithMocks();

        var request = new CreateDemoInvitationRequest
        {
            BusinessId = invalidBusinessId,
            RecipientEmail = "valid@example.com",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            Permissions = ValidPermissions()
        };

        var threwValidation = false;
        try
        {
            service.CreateAsync(request, CreatedByUserId).GetAwaiter().GetResult();
        }
        catch (ValidationException)
        {
            threwValidation = true;
        }

        // Verify InsertAsync was never called
        repoMock.Verify(
            r => r.InsertAsync(It.IsAny<DemoInvitation>(), It.IsAny<List<DemoInvitationPermission>>()),
            Times.Never);

        return threwValidation.ToProperty()
            .Label($"BusinessId={invalidBusinessId} should throw ValidationException but threwValidation={threwValidation}");
    }

    #endregion

    #region Property 6: Past expiry rejects and does not persist

    /// <summary>
    /// Property 6c: For any ExpiresAtUtc in the past (or exactly UtcNow),
    /// CreateAsync SHALL throw ValidationException and InsertAsync SHALL NOT be called.
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PastExpiry_Rejects_AndDoesNotPersist(PositiveInt minutesInPastSeed)
    {
        // Generate an expiry that is anywhere from 1 minute to 10000 minutes in the past
        var minutesInPast = (minutesInPastSeed.Get % 10000) + 1;
        var pastExpiry = DateTime.UtcNow.AddMinutes(-minutesInPast);

        var (service, repoMock) = CreateServiceWithMocks();

        var request = new CreateDemoInvitationRequest
        {
            BusinessId = 1000, // valid demo business
            RecipientEmail = "valid@example.com",
            ExpiresAtUtc = pastExpiry,
            Permissions = ValidPermissions()
        };

        var threwValidation = false;
        try
        {
            service.CreateAsync(request, CreatedByUserId).GetAwaiter().GetResult();
        }
        catch (ValidationException)
        {
            threwValidation = true;
        }

        // Verify InsertAsync was never called
        repoMock.Verify(
            r => r.InsertAsync(It.IsAny<DemoInvitation>(), It.IsAny<List<DemoInvitationPermission>>()),
            Times.Never);

        return threwValidation.ToProperty()
            .Label($"ExpiresAtUtc={pastExpiry:O} (past) should throw ValidationException but threwValidation={threwValidation}");
    }

    #endregion

    #region Property 6: No granted permissions rejects and does not persist

    /// <summary>
    /// Property 6d: For any set of permissions where ALL access levels are 'none'
    /// (no granted permissions), CreateAsync SHALL throw ValidationException and
    /// InsertAsync SHALL NOT be called.
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoGrantedPermissions_Rejects_AndDoesNotPersist(PositiveInt moduleCountSeed)
    {
        // Generate 1 to 9 permissions, all with 'none' access level
        var moduleCount = (moduleCountSeed.Get % 9) + 1;
        var allNonePermissions = PortalModules.All
            .Take(moduleCount)
            .Select(m => new ModulePermissionEntry { Module = m, AccessLevel = AccessLevels.None })
            .ToList();

        var (service, repoMock) = CreateServiceWithMocks();

        var request = new CreateDemoInvitationRequest
        {
            BusinessId = 1000, // valid demo business
            RecipientEmail = "valid@example.com",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7), // valid future expiry
            Permissions = allNonePermissions
        };

        var threwValidation = false;
        try
        {
            service.CreateAsync(request, CreatedByUserId).GetAwaiter().GetResult();
        }
        catch (ValidationException)
        {
            threwValidation = true;
        }

        // Verify InsertAsync was never called
        repoMock.Verify(
            r => r.InsertAsync(It.IsAny<DemoInvitation>(), It.IsAny<List<DemoInvitationPermission>>()),
            Times.Never);

        return threwValidation.ToProperty()
            .Label($"All {moduleCount} permissions set to 'none' should throw ValidationException " +
                   $"but threwValidation={threwValidation}");
    }

    /// <summary>
    /// Property 6e: For an empty permissions list (no permissions at all),
    /// CreateAsync SHALL throw ValidationException and InsertAsync SHALL NOT be called.
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EmptyPermissions_Rejects_AndDoesNotPersist(PositiveInt seed)
    {
        var (service, repoMock) = CreateServiceWithMocks();

        var request = new CreateDemoInvitationRequest
        {
            BusinessId = 1000, // valid demo business
            RecipientEmail = "valid@example.com",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(seed.Get % 30 + 1), // valid future expiry
            Permissions = new List<ModulePermissionEntry>() // empty list
        };

        var threwValidation = false;
        try
        {
            service.CreateAsync(request, CreatedByUserId).GetAwaiter().GetResult();
        }
        catch (ValidationException)
        {
            threwValidation = true;
        }

        // Verify InsertAsync was never called
        repoMock.Verify(
            r => r.InsertAsync(It.IsAny<DemoInvitation>(), It.IsAny<List<DemoInvitationPermission>>()),
            Times.Never);

        return threwValidation.ToProperty()
            .Label($"Empty permissions list should throw ValidationException but threwValidation={threwValidation}");
    }

    #endregion
}
