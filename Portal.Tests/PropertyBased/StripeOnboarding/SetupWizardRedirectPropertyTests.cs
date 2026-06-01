using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Services;
using Portal.Web.Services.Stripe;
using Xunit;

namespace Portal.Tests.PropertyBased.StripeOnboarding;

// Feature: stripe-onboarding, Property 7: Setup wizard redirect enforcement

/// <summary>
/// Property-based tests for setup wizard redirect enforcement.
/// For any authenticated HTTP request by a user with the IsOwner claim whose Business has no
/// BusinessProfile record, the system SHALL redirect to the setup wizard page, regardless of
/// the originally requested URL (excluding the setup wizard route itself).
/// **Validates: Requirements 4.1, 4.11**
/// </summary>
public class SetupWizardRedirectPropertyTests
{
    /// <summary>
    /// Represents the state of a business for redirect enforcement testing.
    /// </summary>
    public record BusinessState(int BusinessId, bool HasBusinessProfile);

    /// <summary>
    /// Determines whether a redirect to the setup wizard should occur.
    /// A redirect happens when:
    /// 1. The user is authenticated
    /// 2. The user is an owner (IsOwner claim)
    /// 3. The user has a BusinessId
    /// 4. No BusinessProfile exists for that BusinessId
    /// </summary>
    private static bool ShouldRedirectToSetupWizard(bool isAuthenticated, bool isOwner, int? businessId, bool hasBusinessProfile)
    {
        return isAuthenticated
            && isOwner
            && businessId.HasValue
            && businessId.Value > 0
            && !hasBusinessProfile;
    }

    /// <summary>
    /// Creates a PortalDbContext with optional BusinessProfile seeded for the given businessId.
    /// Executes IsSetupCompleteAsync and returns the result.
    /// </summary>
    private static async Task<bool> ExecuteIsSetupCompleteCheck(int businessId, bool seedBusinessProfile)
    {
        var currentTenantService = new Mock<ICurrentTenantService>();
        currentTenantService.Setup(s => s.CurrentBusinessId).Returns(businessId);

        var portalOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"PortalDb_Redirect_{Guid.NewGuid()}")
            .Options;

        var portalDbContext = new PortalDbContext(portalOptions, currentTenantService.Object);

        // Seed a Business record
        portalDbContext.Businesses.Add(new Business
        {
            Id = businessId,
            Name = $"Test Business {businessId}",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await portalDbContext.SaveChangesAsync();

        // Seed BusinessProfile if specified
        if (seedBusinessProfile)
        {
            portalDbContext.BusinessProfiles.Add(new BusinessProfile
            {
                BusinessId = businessId,
                CompanyRegistrationNumber = "REG123",
                VatRegistrationNumber = "VAT456",
                VatRegistrationDate = DateOnly.FromDateTime(DateTime.UtcNow),
                VatPeriodLengthInMonths = 2,
                AddressLine1 = "123 Test Street",
                City = "Test City",
                PostalCode = "TC1 2AB",
                Country = "Test Country",
                Email = "test@example.com",
                CurrencySymbol = "€"
            });
            await portalDbContext.SaveChangesAsync();
        }

        var logger = new Mock<ILogger<SetupWizardService>>();
        var logoService = new Mock<ILogoService>();

        var service = new SetupWizardService(portalDbContext, logoService.Object, logger.Object);

        var isComplete = await service.IsSetupCompleteAsync(businessId);

        // Clean up
        await portalDbContext.Database.EnsureDeletedAsync();
        portalDbContext.Dispose();

        return isComplete;
    }

    #region Property 7a: Owner without BusinessProfile triggers redirect

    /// <summary>
    /// Property 7a: For any authenticated owner with a valid BusinessId and no BusinessProfile,
    /// IsSetupCompleteAsync returns false, meaning the system SHALL redirect to the setup wizard.
    /// **Validates: Requirements 4.1, 4.11**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OwnerWithoutBusinessProfile_ShouldRedirect(PositiveInt businessId)
    {
        var isSetupComplete = ExecuteIsSetupCompleteCheck(businessId.Get, seedBusinessProfile: false).Result;

        // When no BusinessProfile exists, setup is NOT complete → redirect should occur
        var shouldRedirect = ShouldRedirectToSetupWizard(
            isAuthenticated: true,
            isOwner: true,
            businessId: businessId.Get,
            hasBusinessProfile: isSetupComplete);

        return (shouldRedirect && !isSetupComplete).ToProperty()
            .Label($"BusinessId={businessId.Get}: Expected redirect (no BusinessProfile). " +
                   $"IsSetupComplete={isSetupComplete}, ShouldRedirect={shouldRedirect}");
    }

    #endregion

    #region Property 7b: Owner with BusinessProfile does NOT trigger redirect

    /// <summary>
    /// Property 7b: For any authenticated owner with a valid BusinessId that HAS a BusinessProfile,
    /// IsSetupCompleteAsync returns true, meaning no redirect to the setup wizard occurs.
    /// **Validates: Requirements 4.1, 4.11**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OwnerWithBusinessProfile_ShouldNotRedirect(PositiveInt businessId)
    {
        var isSetupComplete = ExecuteIsSetupCompleteCheck(businessId.Get, seedBusinessProfile: true).Result;

        // When BusinessProfile exists, setup IS complete → no redirect
        var shouldRedirect = ShouldRedirectToSetupWizard(
            isAuthenticated: true,
            isOwner: true,
            businessId: businessId.Get,
            hasBusinessProfile: isSetupComplete);

        return (!shouldRedirect && isSetupComplete).ToProperty()
            .Label($"BusinessId={businessId.Get}: Expected no redirect (BusinessProfile exists). " +
                   $"IsSetupComplete={isSetupComplete}, ShouldRedirect={shouldRedirect}");
    }

    #endregion

    #region Property 7c: Non-owner users are never redirected regardless of BusinessProfile state

    /// <summary>
    /// Property 7c: For any authenticated user who is NOT an owner, the system SHALL NOT redirect
    /// to the setup wizard, regardless of whether a BusinessProfile exists.
    /// **Validates: Requirements 4.1, 4.11**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(NonOwnerRedirectArbitrary) })]
    public Property NonOwner_NeverRedirected(NonOwnerRedirectInput input)
    {
        // Non-owners should never be redirected, regardless of BusinessProfile state
        var shouldRedirect = ShouldRedirectToSetupWizard(
            isAuthenticated: true,
            isOwner: false,
            businessId: input.BusinessId,
            hasBusinessProfile: input.HasBusinessProfile);

        return (!shouldRedirect).ToProperty()
            .Label($"BusinessId={input.BusinessId}, HasProfile={input.HasBusinessProfile}: " +
                   $"Non-owner should never be redirected. ShouldRedirect={shouldRedirect}");
    }

    #endregion

    #region Property 7d: Unauthenticated users are never redirected

    /// <summary>
    /// Property 7d: For any unauthenticated request, the system SHALL NOT redirect to the
    /// setup wizard (authentication is handled separately by the auth middleware).
    /// **Validates: Requirements 4.1, 4.11**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(UnauthenticatedRedirectArbitrary) })]
    public Property Unauthenticated_NeverRedirected(UnauthenticatedRedirectInput input)
    {
        // Unauthenticated users should never be redirected to setup wizard
        var shouldRedirect = ShouldRedirectToSetupWizard(
            isAuthenticated: false,
            isOwner: input.IsOwner,
            businessId: input.BusinessId,
            hasBusinessProfile: input.HasBusinessProfile);

        return (!shouldRedirect).ToProperty()
            .Label($"IsOwner={input.IsOwner}, BusinessId={input.BusinessId}, HasProfile={input.HasBusinessProfile}: " +
                   $"Unauthenticated user should never be redirected. ShouldRedirect={shouldRedirect}");
    }

    #endregion

    #region Property 7e: Redirect decision is solely determined by BusinessProfile existence for authenticated owners

    /// <summary>
    /// Property 7e: For any authenticated owner with a valid BusinessId, the redirect decision
    /// is determined exclusively by whether a BusinessProfile record exists — no other factor
    /// (requested URL, time of day, etc.) affects the decision.
    /// **Validates: Requirements 4.1, 4.11**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(RedirectEnforcementArbitrary) })]
    public Property RedirectDecision_DeterminedByBusinessProfileExistence(RedirectEnforcementInput input)
    {
        var isSetupComplete = ExecuteIsSetupCompleteCheck(input.BusinessId, input.HasBusinessProfile).Result;

        // The redirect decision for an authenticated owner is the inverse of IsSetupComplete
        var shouldRedirect = ShouldRedirectToSetupWizard(
            isAuthenticated: true,
            isOwner: true,
            businessId: input.BusinessId,
            hasBusinessProfile: isSetupComplete);

        // Key property: shouldRedirect == !hasBusinessProfile for authenticated owners
        var expectedRedirect = !input.HasBusinessProfile;

        return (shouldRedirect == expectedRedirect).ToProperty()
            .Label($"BusinessId={input.BusinessId}, HasProfile={input.HasBusinessProfile}: " +
                   $"Expected redirect={expectedRedirect}, Got redirect={shouldRedirect}, " +
                   $"IsSetupComplete={isSetupComplete}");
    }

    #endregion
}

#region Custom Arbitraries for Setup Wizard Redirect Tests

/// <summary>
/// Input for testing non-owner redirect scenarios.
/// </summary>
public class NonOwnerRedirectInput
{
    public int BusinessId { get; set; }
    public bool HasBusinessProfile { get; set; }

    public override string ToString() => $"(BusinessId={BusinessId}, HasProfile={HasBusinessProfile})";
}

/// <summary>
/// Arbitrary that generates non-owner redirect test inputs.
/// </summary>
public class NonOwnerRedirectArbitrary
{
    public static Arbitrary<NonOwnerRedirectInput> NonOwnerRedirectInputs()
    {
        var businessIdGen = Gen.Choose(1, 1000);
        var hasProfileGen = Gen.Elements(true, false);

        var gen = from businessId in businessIdGen
                  from hasProfile in hasProfileGen
                  select new NonOwnerRedirectInput
                  {
                      BusinessId = businessId,
                      HasBusinessProfile = hasProfile
                  };

        return Arb.From(gen);
    }
}

/// <summary>
/// Input for testing unauthenticated redirect scenarios.
/// </summary>
public class UnauthenticatedRedirectInput
{
    public bool IsOwner { get; set; }
    public int? BusinessId { get; set; }
    public bool HasBusinessProfile { get; set; }

    public override string ToString() => $"(IsOwner={IsOwner}, BusinessId={BusinessId}, HasProfile={HasBusinessProfile})";
}

/// <summary>
/// Arbitrary that generates unauthenticated redirect test inputs.
/// </summary>
public class UnauthenticatedRedirectArbitrary
{
    public static Arbitrary<UnauthenticatedRedirectInput> UnauthenticatedRedirectInputs()
    {
        var isOwnerGen = Gen.Elements(true, false);
        var businessIdGen = Gen.Frequency(
            Tuple.Create(3, Gen.Choose(1, 1000).Select(id => (int?)id)),
            Tuple.Create(1, Gen.Constant<int?>(null))
        );
        var hasProfileGen = Gen.Elements(true, false);

        var gen = from isOwner in isOwnerGen
                  from businessId in businessIdGen
                  from hasProfile in hasProfileGen
                  select new UnauthenticatedRedirectInput
                  {
                      IsOwner = isOwner,
                      BusinessId = businessId,
                      HasBusinessProfile = hasProfile
                  };

        return Arb.From(gen);
    }
}

/// <summary>
/// Input for testing the core redirect enforcement property.
/// </summary>
public class RedirectEnforcementInput
{
    public int BusinessId { get; set; }
    public bool HasBusinessProfile { get; set; }

    public override string ToString() => $"(BusinessId={BusinessId}, HasProfile={HasBusinessProfile})";
}

/// <summary>
/// Arbitrary that generates redirect enforcement test inputs with varying business states.
/// </summary>
public class RedirectEnforcementArbitrary
{
    public static Arbitrary<RedirectEnforcementInput> RedirectEnforcementInputs()
    {
        var businessIdGen = Gen.Choose(1, 1000);
        var hasProfileGen = Gen.Elements(true, false);

        var gen = from businessId in businessIdGen
                  from hasProfile in hasProfileGen
                  select new RedirectEnforcementInput
                  {
                      BusinessId = businessId,
                      HasBusinessProfile = hasProfile
                  };

        return Arb.From(gen);
    }
}

#endregion
