using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Entities.Billing;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Models.Stripe;
using Portal.Web.Services.Stripe;
using Xunit;

namespace Portal.Tests.PropertyBased.StripeOnboarding;

// Feature: stripe-onboarding, Property 11: Module access decision correctness

/// <summary>
/// Property-based tests for module access decision correctness.
/// For any authenticated user requesting a module-gated endpoint: access SHALL be granted if and only if
/// (1) the user's Business has a Subscription with Status in {active, trialing, past_due} AND
/// (2) the requested module exists in PlanFeature with IsIncluded=true for the Business's Plan AND
/// (3) the module identifier is a valid value in PortalModules.All.
/// When Status is past_due, access is granted with a warning flag set.
/// For any invalid module identifier, access is denied.
/// **Validates: Requirements 5.1, 5.2, 5.3, 5.4, 5.8, 5.11**
/// </summary>
public class ModuleAccessDecisionPropertyTests
{
    /// <summary>
    /// Valid subscription statuses that grant access (HasActiveSubscription = true).
    /// </summary>
    private static readonly string[] ActiveStatuses = { "active", "trialing", "past_due" };

    /// <summary>
    /// Subscription statuses that deny access (HasActiveSubscription = false).
    /// </summary>
    private static readonly string[] InactiveStatuses = { "cancelled", "incomplete", "unpaid" };

    /// <summary>
    /// All valid subscription statuses.
    /// </summary>
    private static readonly string[] AllStatuses = ActiveStatuses.Concat(InactiveStatuses).ToArray();

    /// <summary>
    /// Creates a PortalDbContext backed by an in-memory database with the given plan and features seeded.
    /// </summary>
    private static PortalDbContext CreateInMemoryDbContext(
        string dbName,
        Plan plan,
        List<PlanFeature> planFeatures,
        Subscription? subscription,
        int businessId)
    {
        var currentTenantServiceMock = new Mock<ICurrentTenantService>();
        currentTenantServiceMock.Setup(s => s.CurrentBusinessId).Returns(businessId);

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new PortalDbContext(options, currentTenantServiceMock.Object);

        // Seed the plan
        context.Plans.Add(plan);

        // Seed plan features
        foreach (var feature in planFeatures)
        {
            context.PlanFeatures.Add(feature);
        }

        // Seed subscription if provided
        if (subscription != null)
        {
            context.Subscriptions.Add(subscription);
        }

        context.SaveChanges();

        return context;
    }

    /// <summary>
    /// Creates a SubscriptionPlanService with the given dependencies.
    /// </summary>
    private static SubscriptionPlanService CreateService(
        SubscriptionRepository subscriptionRepo,
        PortalDbContext dbContext,
        HttpContext? httpContext = null)
    {
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock.Setup(a => a.HttpContext).Returns(httpContext ?? new DefaultHttpContext());

        var loggerMock = new Mock<ILogger<SubscriptionPlanService>>();

        return new SubscriptionPlanService(
            subscriptionRepo,
            dbContext,
            httpContextAccessorMock.Object,
            loggerMock.Object);
    }

    #region Property 11a: Active/trialing subscriptions grant access (HasActiveSubscription = true)

    /// <summary>
    /// Property 11a: For any subscription with Status in {active, trialing}, HasActiveSubscription SHALL be true.
    /// **Validates: Requirements 5.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ActiveOrTrialingSubscription_GrantsAccess(
        PositiveInt businessIdSeed,
        PositiveInt planIdSeed)
    {
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var planId = (planIdSeed.Get % 100) + 1;

        // Test both "active" and "trialing" statuses
        var results = new List<bool>();

        foreach (var status in new[] { "active", "trialing" })
        {
            var dbName = $"ModuleAccess_11a_{businessId}_{planId}_{status}_{Guid.NewGuid()}";

            var plan = new Plan
            {
                Id = planId,
                Name = "Test Plan",
                Slug = $"test-plan-{planId}",
                MonthlyPriceEur = 29.99m,
                MaxUsers = 5,
                IsActive = true,
                DisplayOrder = 1,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            var subscription = new Subscription
            {
                Id = 1,
                BusinessId = businessId,
                PlanId = planId,
                Status = status,
                CurrentPeriodStart = DateTime.UtcNow.AddDays(-15),
                CurrentPeriodEnd = DateTime.UtcNow.AddDays(15),
                CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
            };

            using var dbContext = CreateInMemoryDbContext(dbName, plan, new List<PlanFeature>(), subscription, businessId);

            var subscriptionRepoMock = new Mock<SubscriptionRepository>(MockBehavior.Loose, new object[] { null! });
            subscriptionRepoMock
                .Setup(r => r.GetByBusinessIdAsync(businessId))
                .ReturnsAsync(subscription);

            var service = CreateService(subscriptionRepoMock.Object, dbContext);
            var result = service.GetAccessAsync(businessId).GetAwaiter().GetResult();

            results.Add(result.HasActiveSubscription);
        }

        return results.All(r => r).ToProperty()
            .Label($"businessId={businessId}, planId={planId}: active/trialing both grant access");
    }

    #endregion

    #region Property 11b: Past_due subscription grants access with warning (HasActiveSubscription = true)

    /// <summary>
    /// Property 11b: For any subscription with Status = past_due, HasActiveSubscription SHALL be true
    /// and SubscriptionStatus SHALL be "past_due" (enabling warning display).
    /// **Validates: Requirements 5.8**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PastDueSubscription_GrantsAccessWithWarning(
        PositiveInt businessIdSeed,
        PositiveInt planIdSeed)
    {
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var planId = (planIdSeed.Get % 100) + 1;
        var dbName = $"ModuleAccess_11b_{businessId}_{planId}_{Guid.NewGuid()}";

        var plan = new Plan
        {
            Id = planId,
            Name = "Test Plan",
            Slug = $"test-plan-{planId}",
            MonthlyPriceEur = 29.99m,
            MaxUsers = 5,
            IsActive = true,
            DisplayOrder = 1,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var subscription = new Subscription
        {
            Id = 1,
            BusinessId = businessId,
            PlanId = planId,
            Status = "past_due",
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-15),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(15),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
        };

        using var dbContext = CreateInMemoryDbContext(dbName, plan, new List<PlanFeature>(), subscription, businessId);

        var subscriptionRepoMock = new Mock<SubscriptionRepository>(MockBehavior.Loose, new object[] { null! });
        subscriptionRepoMock
            .Setup(r => r.GetByBusinessIdAsync(businessId))
            .ReturnsAsync(subscription);

        var service = CreateService(subscriptionRepoMock.Object, dbContext);
        var result = service.GetAccessAsync(businessId).GetAwaiter().GetResult();

        var hasAccess = result.HasActiveSubscription;
        var statusIsPastDue = result.SubscriptionStatus == "past_due";

        return (hasAccess && statusIsPastDue).ToProperty()
            .Label($"businessId={businessId}: HasActiveSubscription={hasAccess}, Status={result.SubscriptionStatus}");
    }

    #endregion

    #region Property 11c: Cancelled/incomplete/unpaid subscriptions deny access (HasActiveSubscription = false)

    /// <summary>
    /// Property 11c: For any subscription with Status in {cancelled, incomplete, unpaid},
    /// HasActiveSubscription SHALL be false.
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InactiveSubscription_DeniesAccess(
        PositiveInt businessIdSeed,
        PositiveInt planIdSeed,
        PositiveInt statusIndexSeed)
    {
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var planId = (planIdSeed.Get % 100) + 1;
        var status = InactiveStatuses[statusIndexSeed.Get % InactiveStatuses.Length];
        var dbName = $"ModuleAccess_11c_{businessId}_{planId}_{status}_{Guid.NewGuid()}";

        var plan = new Plan
        {
            Id = planId,
            Name = "Test Plan",
            Slug = $"test-plan-{planId}",
            MonthlyPriceEur = 29.99m,
            MaxUsers = 5,
            IsActive = true,
            DisplayOrder = 1,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var subscription = new Subscription
        {
            Id = 1,
            BusinessId = businessId,
            PlanId = planId,
            Status = status,
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-15),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(15),
            CancelledAtUtc = status == "cancelled" ? DateTime.UtcNow.AddDays(-1) : null,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
        };

        using var dbContext = CreateInMemoryDbContext(dbName, plan, new List<PlanFeature>(), subscription, businessId);

        var subscriptionRepoMock = new Mock<SubscriptionRepository>(MockBehavior.Loose, new object[] { null! });
        subscriptionRepoMock
            .Setup(r => r.GetByBusinessIdAsync(businessId))
            .ReturnsAsync(subscription);

        var service = CreateService(subscriptionRepoMock.Object, dbContext);
        var result = service.GetAccessAsync(businessId).GetAwaiter().GetResult();

        return (!result.HasActiveSubscription).ToProperty()
            .Label($"businessId={businessId}, status={status}: HasActiveSubscription={result.HasActiveSubscription} (expected false)");
    }

    #endregion

    #region Property 11d: No subscription denies access (HasActiveSubscription = false)

    /// <summary>
    /// Property 11d: For any business with no subscription record, HasActiveSubscription SHALL be false
    /// and IncludedModules SHALL be empty.
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoSubscription_DeniesAccess(PositiveInt businessIdSeed)
    {
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var dbName = $"ModuleAccess_11d_{businessId}_{Guid.NewGuid()}";

        var plan = new Plan
        {
            Id = 1,
            Name = "Placeholder Plan",
            Slug = "placeholder",
            MonthlyPriceEur = 0m,
            MaxUsers = 1,
            IsActive = true,
            DisplayOrder = 1,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        // No subscription seeded
        using var dbContext = CreateInMemoryDbContext(dbName, plan, new List<PlanFeature>(), null, businessId);

        var subscriptionRepoMock = new Mock<SubscriptionRepository>(MockBehavior.Loose, new object[] { null! });
        subscriptionRepoMock
            .Setup(r => r.GetByBusinessIdAsync(businessId))
            .ReturnsAsync((Subscription?)null);

        var service = CreateService(subscriptionRepoMock.Object, dbContext);
        var result = service.GetAccessAsync(businessId).GetAwaiter().GetResult();

        var noAccess = !result.HasActiveSubscription;
        var emptyModules = result.IncludedModules.Count == 0;

        return (noAccess && emptyModules).ToProperty()
            .Label($"businessId={businessId}: HasActiveSubscription={result.HasActiveSubscription}, " +
                   $"IncludedModules.Count={result.IncludedModules.Count}");
    }

    #endregion

    #region Property 11e: IncludedModules matches exactly the PlanFeatures where IsIncluded = true

    /// <summary>
    /// Property 11e: For any active subscription, IncludedModules SHALL contain exactly the modules
    /// from PlanFeature where IsIncluded = true for the subscription's Plan.
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IncludedModules_MatchesPlanFeaturesWhereIsIncludedTrue(
        PositiveInt businessIdSeed,
        PositiveInt planIdSeed,
        PositiveInt featureMaskSeed)
    {
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var planId = (planIdSeed.Get % 100) + 1;
        var featureMask = featureMaskSeed.Get;
        var dbName = $"ModuleAccess_11e_{businessId}_{planId}_{featureMask}_{Guid.NewGuid()}";

        var plan = new Plan
        {
            Id = planId,
            Name = "Test Plan",
            Slug = $"test-plan-{planId}",
            MonthlyPriceEur = 29.99m,
            MaxUsers = 5,
            IsActive = true,
            DisplayOrder = 1,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        // Use the feature mask to determine which modules are included
        var planFeatures = new List<PlanFeature>();
        var expectedIncludedModules = new HashSet<string>();
        var featureId = 1;

        for (int i = 0; i < PortalModules.All.Length; i++)
        {
            var isIncluded = ((featureMask >> i) & 1) == 1;
            planFeatures.Add(new PlanFeature
            {
                Id = featureId++,
                PlanId = planId,
                ModuleName = PortalModules.All[i],
                IsIncluded = isIncluded,
                CreatedAtUtc = DateTime.UtcNow
            });

            if (isIncluded)
            {
                expectedIncludedModules.Add(PortalModules.All[i]);
            }
        }

        var subscription = new Subscription
        {
            Id = 1,
            BusinessId = businessId,
            PlanId = planId,
            Status = "active",
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-15),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(15),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
        };

        using var dbContext = CreateInMemoryDbContext(dbName, plan, planFeatures, subscription, businessId);

        var subscriptionRepoMock = new Mock<SubscriptionRepository>(MockBehavior.Loose, new object[] { null! });
        subscriptionRepoMock
            .Setup(r => r.GetByBusinessIdAsync(businessId))
            .ReturnsAsync(subscription);

        var service = CreateService(subscriptionRepoMock.Object, dbContext);
        var result = service.GetAccessAsync(businessId).GetAwaiter().GetResult();

        var modulesMatch = result.IncludedModules.SetEquals(expectedIncludedModules);

        return modulesMatch.ToProperty()
            .Label($"businessId={businessId}, planId={planId}, featureMask={featureMask}: " +
                   $"expected=[{string.Join(",", expectedIncludedModules)}], " +
                   $"actual=[{string.Join(",", result.IncludedModules)}]");
    }

    #endregion

    #region Property 11f: Modules NOT in the plan are NOT in IncludedModules

    /// <summary>
    /// Property 11f: For any active subscription, modules that have IsIncluded = false in PlanFeature
    /// SHALL NOT appear in IncludedModules. Modules with no PlanFeature record SHALL NOT appear either.
    /// **Validates: Requirements 5.11**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExcludedModules_NotInIncludedModules(
        PositiveInt businessIdSeed,
        PositiveInt planIdSeed,
        PositiveInt includedCountSeed)
    {
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var planId = (planIdSeed.Get % 100) + 1;
        // Include only a subset of modules (1 to N-1 modules included)
        var includedCount = (includedCountSeed.Get % (PortalModules.All.Length - 1)) + 1;
        var dbName = $"ModuleAccess_11f_{businessId}_{planId}_{includedCount}_{Guid.NewGuid()}";

        var plan = new Plan
        {
            Id = planId,
            Name = "Test Plan",
            Slug = $"test-plan-{planId}",
            MonthlyPriceEur = 29.99m,
            MaxUsers = 5,
            IsActive = true,
            DisplayOrder = 1,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        // Include only the first N modules, exclude the rest
        var planFeatures = new List<PlanFeature>();
        var includedModules = new HashSet<string>();
        var excludedModules = new HashSet<string>();
        var featureId = 1;

        for (int i = 0; i < PortalModules.All.Length; i++)
        {
            var isIncluded = i < includedCount;
            planFeatures.Add(new PlanFeature
            {
                Id = featureId++,
                PlanId = planId,
                ModuleName = PortalModules.All[i],
                IsIncluded = isIncluded,
                CreatedAtUtc = DateTime.UtcNow
            });

            if (isIncluded)
                includedModules.Add(PortalModules.All[i]);
            else
                excludedModules.Add(PortalModules.All[i]);
        }

        var subscription = new Subscription
        {
            Id = 1,
            BusinessId = businessId,
            PlanId = planId,
            Status = "active",
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-15),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(15),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
        };

        using var dbContext = CreateInMemoryDbContext(dbName, plan, planFeatures, subscription, businessId);

        var subscriptionRepoMock = new Mock<SubscriptionRepository>(MockBehavior.Loose, new object[] { null! });
        subscriptionRepoMock
            .Setup(r => r.GetByBusinessIdAsync(businessId))
            .ReturnsAsync(subscription);

        var service = CreateService(subscriptionRepoMock.Object, dbContext);
        var result = service.GetAccessAsync(businessId).GetAwaiter().GetResult();

        // Verify no excluded module appears in IncludedModules
        var noExcludedModulesPresent = !excludedModules.Any(m => result.IncludedModules.Contains(m));
        // Verify all included modules are present
        var allIncludedPresent = includedModules.All(m => result.IncludedModules.Contains(m));

        return (noExcludedModulesPresent && allIncludedPresent).ToProperty()
            .Label($"businessId={businessId}, includedCount={includedCount}: " +
                   $"noExcludedPresent={noExcludedModulesPresent}, allIncludedPresent={allIncludedPresent}, " +
                   $"excluded=[{string.Join(",", excludedModules)}], " +
                   $"resultModules=[{string.Join(",", result.IncludedModules)}]");
    }

    #endregion
}
