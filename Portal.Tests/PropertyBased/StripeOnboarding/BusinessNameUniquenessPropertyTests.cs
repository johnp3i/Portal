using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Services;
using Portal.Web.Models.Stripe;
using Portal.Web.Services.Stripe;
using Xunit;

namespace Portal.Tests.PropertyBased.StripeOnboarding;

// Feature: stripe-onboarding, Property 10: Business name uniqueness enforcement

/// <summary>
/// Property-based tests for business name uniqueness enforcement.
/// For any setup wizard submission where the provided business name matches an existing
/// Business.Name belonging to a different tenant, the system SHALL reject the submission
/// with a validation error.
/// **Validates: Requirements 4.14**
/// </summary>
public class BusinessNameUniquenessPropertyTests
{
    /// <summary>
    /// Represents a scenario for testing business name uniqueness.
    /// </summary>
    public class UniquenessScenario
    {
        public string BusinessName { get; set; } = null!;
        public int CurrentBusinessId { get; set; }
        public int OtherBusinessId { get; set; }
        public string CurrencySymbol { get; set; } = null!;

        public override string ToString() =>
            $"(Name='{BusinessName}', CurrentId={CurrentBusinessId}, OtherId={OtherBusinessId})";
    }

    /// <summary>
    /// Creates a PortalDbContext backed by an in-memory database with the given name.
    /// </summary>
    private static PortalDbContext CreatePortalDbContext(string dbName)
    {
        var currentTenantService = new Mock<ICurrentTenantService>();
        currentTenantService.Setup(s => s.CurrentBusinessId).Returns(0);

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new PortalDbContext(options, currentTenantService.Object);
    }

    /// <summary>
    /// Creates a SetupWizardService with the given PortalDbContext and mocked dependencies.
    /// </summary>
    private static SetupWizardService CreateService(PortalDbContext portalDbContext)
    {
        var logoService = new Mock<ILogoService>();
        var logger = new Mock<ILogger<SetupWizardService>>();

        return new SetupWizardService(portalDbContext, logoService.Object, logger.Object);
    }

    #region Property 10a: Name taken by different business returns validation error

    /// <summary>
    /// Property 10a: For any valid business name that already exists for a different tenant,
    /// CompleteSetupAsync SHALL reject the submission with a validation error on BusinessName
    /// indicating the name is already in use.
    /// **Validates: Requirements 4.14**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(UniquenessScenarioArbitrary) })]
    public Property NameTakenByDifferentBusiness_ReturnsValidationError(UniquenessScenario scenario)
    {
        var dbName = $"Uniqueness_Taken_{Guid.NewGuid()}";
        using var portalDbContext = CreatePortalDbContext(dbName);

        // Seed: another business already has this name
        portalDbContext.Businesses.Add(new Business
        {
            Id = scenario.OtherBusinessId,
            Name = scenario.BusinessName,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        // Seed: the current business with a different name
        portalDbContext.Businesses.Add(new Business
        {
            Id = scenario.CurrentBusinessId,
            Name = $"Placeholder_{scenario.CurrentBusinessId}",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        portalDbContext.SaveChanges();

        var service = CreateService(portalDbContext);

        var model = new SetupWizardModel
        {
            BusinessName = scenario.BusinessName,
            CurrencySymbol = scenario.CurrencySymbol
        };

        var result = service.CompleteSetupAsync(scenario.CurrentBusinessId, model).Result;

        // Clean up
        portalDbContext.Database.EnsureDeleted();

        var hasBusinessNameError = result.ValidationErrors.ContainsKey("BusinessName")
            && result.ValidationErrors["BusinessName"].Contains("already in use", StringComparison.OrdinalIgnoreCase);

        return (!result.Success && hasBusinessNameError).ToProperty()
            .Label($"Scenario={scenario}: Expected validation error for duplicate name. " +
                   $"Success={result.Success}, Errors={string.Join("; ", result.ValidationErrors.Select(kv => $"{kv.Key}={kv.Value}"))}");
    }

    #endregion

    #region Property 10b: Same name belonging to current business is allowed

    /// <summary>
    /// Property 10b: For any business name that belongs to the current business (same BusinessId),
    /// CompleteSetupAsync SHALL NOT reject the submission due to name uniqueness — the name
    /// is allowed because it's the business's own name.
    /// **Validates: Requirements 4.14**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(UniquenessScenarioArbitrary) })]
    public Property SameNameBelongsToCurrentBusiness_IsAllowed(UniquenessScenario scenario)
    {
        var dbName = $"Uniqueness_Own_{Guid.NewGuid()}";
        using var portalDbContext = CreatePortalDbContext(dbName);

        // Seed: the current business already has this name (e.g., user is re-submitting)
        portalDbContext.Businesses.Add(new Business
        {
            Id = scenario.CurrentBusinessId,
            Name = scenario.BusinessName,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        portalDbContext.SaveChanges();

        var service = CreateService(portalDbContext);

        var model = new SetupWizardModel
        {
            BusinessName = scenario.BusinessName,
            CurrencySymbol = scenario.CurrencySymbol
        };

        var result = service.CompleteSetupAsync(scenario.CurrentBusinessId, model).Result;

        // Clean up
        portalDbContext.Database.EnsureDeleted();

        // Should NOT have a "BusinessName already in use" validation error
        var hasUniquenessError = result.ValidationErrors.ContainsKey("BusinessName")
            && result.ValidationErrors["BusinessName"].Contains("already in use", StringComparison.OrdinalIgnoreCase);

        return (!hasUniquenessError).ToProperty()
            .Label($"Scenario={scenario}: Expected no uniqueness error when name belongs to current business. " +
                   $"Success={result.Success}, HasUniquenessError={hasUniquenessError}");
    }

    #endregion

    #region Property 10c: Unique name does not trigger uniqueness error

    /// <summary>
    /// Property 10c: For any business name that does not exist in the database for any other
    /// tenant, CompleteSetupAsync SHALL NOT return a uniqueness validation error.
    /// **Validates: Requirements 4.14**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(UniquenessScenarioArbitrary) })]
    public Property UniqueName_DoesNotTriggerUniquenessError(UniquenessScenario scenario)
    {
        var dbName = $"Uniqueness_Unique_{Guid.NewGuid()}";
        using var portalDbContext = CreatePortalDbContext(dbName);

        // Seed: the current business with a different placeholder name
        portalDbContext.Businesses.Add(new Business
        {
            Id = scenario.CurrentBusinessId,
            Name = $"Placeholder_{scenario.CurrentBusinessId}",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        // Seed: another business with a DIFFERENT name (not the one being submitted)
        portalDbContext.Businesses.Add(new Business
        {
            Id = scenario.OtherBusinessId,
            Name = $"OtherBusiness_{scenario.OtherBusinessId}",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        portalDbContext.SaveChanges();

        var service = CreateService(portalDbContext);

        var model = new SetupWizardModel
        {
            BusinessName = scenario.BusinessName,
            CurrencySymbol = scenario.CurrencySymbol
        };

        var result = service.CompleteSetupAsync(scenario.CurrentBusinessId, model).Result;

        // Clean up
        portalDbContext.Database.EnsureDeleted();

        // Should NOT have a "BusinessName already in use" validation error
        var hasUniquenessError = result.ValidationErrors.ContainsKey("BusinessName")
            && result.ValidationErrors["BusinessName"].Contains("already in use", StringComparison.OrdinalIgnoreCase);

        return (!hasUniquenessError).ToProperty()
            .Label($"Scenario={scenario}: Expected no uniqueness error for unique name. " +
                   $"Success={result.Success}, HasUniquenessError={hasUniquenessError}");
    }

    #endregion

    #region Property 10d: IsBusinessNameTakenAsync correctly identifies taken names

    /// <summary>
    /// Property 10d: For any business name, IsBusinessNameTakenAsync SHALL return true if and
    /// only if a Business with that exact name exists with a different Id than excludeBusinessId.
    /// **Validates: Requirements 4.14**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(UniquenessScenarioArbitrary) })]
    public Property IsBusinessNameTaken_ReturnsTrueOnlyWhenDifferentBusinessHasName(
        UniquenessScenario scenario)
    {
        var dbName = $"Uniqueness_Check_{Guid.NewGuid()}";
        using var portalDbContext = CreatePortalDbContext(dbName);

        // Seed: another business with the same name
        portalDbContext.Businesses.Add(new Business
        {
            Id = scenario.OtherBusinessId,
            Name = scenario.BusinessName,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        portalDbContext.SaveChanges();

        var service = CreateService(portalDbContext);

        // Check with excludeBusinessId = current business (different from the one that has the name)
        var takenForDifferent = service.IsBusinessNameTakenAsync(
            scenario.BusinessName, scenario.CurrentBusinessId).Result;

        // Check with excludeBusinessId = the business that owns the name (should NOT be taken)
        var takenForOwner = service.IsBusinessNameTakenAsync(
            scenario.BusinessName, scenario.OtherBusinessId).Result;

        // Clean up
        portalDbContext.Database.EnsureDeleted();

        return (takenForDifferent && !takenForOwner).ToProperty()
            .Label($"Scenario={scenario}: TakenForDifferent={takenForDifferent} (expected true), " +
                   $"TakenForOwner={takenForOwner} (expected false)");
    }

    #endregion
}

#region Custom Arbitraries

/// <summary>
/// Arbitrary that generates valid UniquenessScenario instances with randomized
/// business names and distinct business IDs.
/// </summary>
public class UniquenessScenarioArbitrary
{
    public static Arbitrary<BusinessNameUniquenessPropertyTests.UniquenessScenario> UniquenessScenarios()
    {
        var businessNameGen = Gen.Elements(
            "Acme Corp", "TechVentures Ltd", "Global Solutions",
            "Smith & Partners", "Digital Dynamics", "CloudFirst Inc",
            "Nordic Innovations", "Sunrise Bakery", "Metro Logistics",
            "Alpine Engineering", "Coastal Designs", "Urban Eats",
            "Quantum Labs", "Evergreen Services", "Bright Ideas Co");

        var currencyGen = Gen.Elements("€", "$", "£", "CHF", "SEK");

        // Generate two distinct business IDs
        var idPairGen = Gen.Choose(1, 500)
            .Two()
            .Where(pair => pair.Item1 != pair.Item2);

        var gen = from name in businessNameGen
                  from currency in currencyGen
                  from ids in idPairGen
                  select new BusinessNameUniquenessPropertyTests.UniquenessScenario
                  {
                      BusinessName = name,
                      CurrentBusinessId = ids.Item1,
                      OtherBusinessId = ids.Item2,
                      CurrencySymbol = currency
                  };

        return Arb.From(gen);
    }
}

#endregion
