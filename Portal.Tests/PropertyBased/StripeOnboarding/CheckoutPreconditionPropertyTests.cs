using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Entities.Identity;
using Portal.Infrastructure.Services;
using Portal.Web.Configuration;
using Portal.Web.Models.Stripe;
using Portal.Web.Services.Stripe;
using Xunit;

namespace Portal.Tests.PropertyBased.StripeOnboarding;

// Feature: stripe-onboarding, Property 1: Checkout precondition enforcement

/// <summary>
/// Property-based tests for checkout precondition enforcement.
/// For any authenticated user with a PendingRegistration, the CheckoutService SHALL create a
/// Stripe Checkout Session if and only if the PendingRegistration is not completed AND the
/// referenced Plan has a non-null, non-empty StripePriceId. In all other cases, no Stripe
/// session is created and the appropriate redirect or error is returned.
/// **Validates: Requirements 1.1, 1.6, 1.8**
/// </summary>
public class CheckoutPreconditionPropertyTests
{
    /// <summary>
    /// Represents the state of a PendingRegistration for property testing.
    /// </summary>
    public record PendingRegistrationState(bool Exists, bool IsCompleted, int PlanId);

    /// <summary>
    /// Represents the state of a Plan for property testing.
    /// </summary>
    public record PlanState(bool Exists, string? StripePriceId);

    /// <summary>
    /// Determines whether checkout preconditions are met based on the registration and plan state.
    /// This mirrors the logic in CheckoutService.CreateCheckoutSessionAsync.
    /// </summary>
    private static bool ArePreconditionsMet(PendingRegistrationState registration, PlanState plan)
    {
        // Preconditions are met if and only if:
        // 1. PendingRegistration exists
        // 2. PendingRegistration is NOT completed
        // 3. Plan exists AND has a non-null, non-empty StripePriceId
        return registration.Exists
            && !registration.IsCompleted
            && plan.Exists
            && !string.IsNullOrWhiteSpace(plan.StripePriceId);
    }

    /// <summary>
    /// Determines the expected failure reason when preconditions are NOT met.
    /// </summary>
    private static CheckoutFailureReason? GetExpectedFailureReason(PendingRegistrationState registration, PlanState plan)
    {
        if (!registration.Exists)
            return CheckoutFailureReason.NoPendingRegistration;

        if (registration.IsCompleted)
            return CheckoutFailureReason.AlreadyCompleted;

        if (!plan.Exists || string.IsNullOrWhiteSpace(plan.StripePriceId))
            return CheckoutFailureReason.PlanNotAvailable;

        return null; // Preconditions met — no failure
    }

    /// <summary>
    /// Creates a configured CheckoutService with mocked dependencies based on the given states.
    /// The Stripe API call is not actually made — we verify precondition logic only.
    /// </summary>
    private static async Task<CheckoutResult> ExecuteCheckoutPreconditionCheck(
        string userId,
        PendingRegistrationState registrationState,
        PlanState planState)
    {
        // Set up MembershipDbContext with in-memory provider
        var membershipOptions = new DbContextOptionsBuilder<MembershipDbContext>()
            .UseInMemoryDatabase(databaseName: $"MembershipDb_{Guid.NewGuid()}")
            .Options;

        var membershipDbContext = new MembershipDbContext(membershipOptions);

        // Seed PendingRegistration if it exists
        if (registrationState.Exists)
        {
            membershipDbContext.PendingRegistrations.Add(new PendingRegistration
            {
                Id = 1,
                UserId = userId,
                PlanId = registrationState.PlanId,
                IsCompleted = registrationState.IsCompleted,
                CreatedAtUtc = DateTime.UtcNow
            });
            await membershipDbContext.SaveChangesAsync();
        }

        // Set up PortalDbContext with in-memory provider
        var currentTenantService = new Mock<ICurrentTenantService>();
        currentTenantService.Setup(s => s.CurrentBusinessId).Returns(1);

        var portalOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"PortalDb_{Guid.NewGuid()}")
            .Options;

        var portalDbContext = new PortalDbContext(portalOptions, currentTenantService.Object);

        // Seed Plan if it exists
        if (planState.Exists)
        {
            portalDbContext.Plans.Add(new Plan
            {
                Id = registrationState.PlanId,
                Name = "Test Plan",
                Slug = "test-plan",
                MonthlyPriceEur = 29.99m,
                MaxUsers = 5,
                IsActive = true,
                DisplayOrder = 1,
                StripePriceId = planState.StripePriceId,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await portalDbContext.SaveChangesAsync();
        }

        // Set up mocks
        var logger = new Mock<ILogger<CheckoutService>>();
        var stripeSettings = Options.Create(new StripeSettings
        {
            SecretKey = "sk_test_fake",
            PublishableKey = "pk_test_fake",
            WebhookSigningSecret = "whsec_fake"
        });

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost");

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(a => a.HttpContext).Returns(httpContext);

        var service = new CheckoutService(
            membershipDbContext,
            portalDbContext,
            logger.Object,
            stripeSettings,
            httpContextAccessor.Object);

        // Execute — we only test precondition logic.
        // If preconditions are met, the service will attempt to call Stripe API which will fail
        // in test (no real API key). We catch that as StripeApiError which confirms preconditions passed.
        var result = await service.CreateCheckoutSessionAsync(userId);

        // Clean up
        await membershipDbContext.Database.EnsureDeletedAsync();
        await portalDbContext.Database.EnsureDeletedAsync();
        membershipDbContext.Dispose();
        portalDbContext.Dispose();

        return result;
    }

    #region Property 1a: When PendingRegistration does not exist, result fails with NoPendingRegistration

    /// <summary>
    /// Property 1a: For any userId where no PendingRegistration exists, the CheckoutService
    /// SHALL return Success=false with FailureReason=NoPendingRegistration.
    /// **Validates: Requirements 1.1, 1.6, 1.8**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoPendingRegistration_ReturnsFailure(PositiveInt seed)
    {
        var userId = $"user-{seed.Get}";
        var registrationState = new PendingRegistrationState(Exists: false, IsCompleted: false, PlanId: 1);
        var planState = new PlanState(Exists: true, StripePriceId: "price_abc123");

        var result = ExecuteCheckoutPreconditionCheck(userId, registrationState, planState).Result;

        var preconditionsMet = ArePreconditionsMet(registrationState, planState);
        var expectedReason = GetExpectedFailureReason(registrationState, planState);

        return (!result.Success
            && !preconditionsMet
            && result.FailureReason == expectedReason).ToProperty()
            .Label($"UserId={userId}: Expected Success=false, FailureReason={expectedReason}, Got Success={result.Success}, FailureReason={result.FailureReason}");
    }

    #endregion

    #region Property 1b: When PendingRegistration is already completed, result fails with AlreadyCompleted

    /// <summary>
    /// Property 1b: For any userId with a completed PendingRegistration, the CheckoutService
    /// SHALL return Success=false with FailureReason=AlreadyCompleted.
    /// **Validates: Requirements 1.1, 1.6, 1.8**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CompletedRegistration_ReturnsAlreadyCompleted(PositiveInt planId)
    {
        var userId = $"user-completed-{planId.Get}";
        var registrationState = new PendingRegistrationState(Exists: true, IsCompleted: true, PlanId: planId.Get);
        var planState = new PlanState(Exists: true, StripePriceId: "price_valid123");

        var result = ExecuteCheckoutPreconditionCheck(userId, registrationState, planState).Result;

        var preconditionsMet = ArePreconditionsMet(registrationState, planState);
        var expectedReason = GetExpectedFailureReason(registrationState, planState);

        return (!result.Success
            && !preconditionsMet
            && result.FailureReason == CheckoutFailureReason.AlreadyCompleted).ToProperty()
            .Label($"PlanId={planId.Get}: Expected FailureReason=AlreadyCompleted, Got FailureReason={result.FailureReason}");
    }

    #endregion

    #region Property 1c: When Plan has null StripePriceId, result fails with PlanNotAvailable

    /// <summary>
    /// Property 1c: For any incomplete PendingRegistration referencing a Plan with null StripePriceId,
    /// the CheckoutService SHALL return Success=false with FailureReason=PlanNotAvailable.
    /// **Validates: Requirements 1.1, 1.6, 1.8**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NullStripePriceId_ReturnsPlanNotAvailable(PositiveInt planId)
    {
        var userId = $"user-nullprice-{planId.Get}";
        var registrationState = new PendingRegistrationState(Exists: true, IsCompleted: false, PlanId: planId.Get);
        var planState = new PlanState(Exists: true, StripePriceId: null);

        var result = ExecuteCheckoutPreconditionCheck(userId, registrationState, planState).Result;

        var preconditionsMet = ArePreconditionsMet(registrationState, planState);
        var expectedReason = GetExpectedFailureReason(registrationState, planState);

        return (!result.Success
            && !preconditionsMet
            && result.FailureReason == CheckoutFailureReason.PlanNotAvailable).ToProperty()
            .Label($"PlanId={planId.Get}: Expected FailureReason=PlanNotAvailable (null StripePriceId), Got FailureReason={result.FailureReason}");
    }

    #endregion

    #region Property 1d: When Plan has empty StripePriceId, result fails with PlanNotAvailable

    /// <summary>
    /// Property 1d: For any incomplete PendingRegistration referencing a Plan with empty/whitespace
    /// StripePriceId, the CheckoutService SHALL return Success=false with FailureReason=PlanNotAvailable.
    /// **Validates: Requirements 1.1, 1.6, 1.8**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(EmptyStripePriceArbitrary) })]
    public Property EmptyStripePriceId_ReturnsPlanNotAvailable(EmptyStripePriceInput input)
    {
        var userId = $"user-emptyprice-{input.PlanId}";
        var registrationState = new PendingRegistrationState(Exists: true, IsCompleted: false, PlanId: input.PlanId);
        var planState = new PlanState(Exists: true, StripePriceId: input.EmptyPriceId);

        var result = ExecuteCheckoutPreconditionCheck(userId, registrationState, planState).Result;

        var preconditionsMet = ArePreconditionsMet(registrationState, planState);

        return (!result.Success
            && !preconditionsMet
            && result.FailureReason == CheckoutFailureReason.PlanNotAvailable).ToProperty()
            .Label($"PlanId={input.PlanId}, StripePriceId='{input.EmptyPriceId}': Expected FailureReason=PlanNotAvailable, Got FailureReason={result.FailureReason}");
    }

    #endregion

    #region Property 1e: When Plan does not exist, result fails with PlanNotAvailable

    /// <summary>
    /// Property 1e: For any incomplete PendingRegistration referencing a Plan that does not exist
    /// in the database, the CheckoutService SHALL return Success=false with FailureReason=PlanNotAvailable.
    /// **Validates: Requirements 1.1, 1.6, 1.8**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PlanDoesNotExist_ReturnsPlanNotAvailable(PositiveInt planId)
    {
        var userId = $"user-noplan-{planId.Get}";
        var registrationState = new PendingRegistrationState(Exists: true, IsCompleted: false, PlanId: planId.Get);
        var planState = new PlanState(Exists: false, StripePriceId: null);

        var result = ExecuteCheckoutPreconditionCheck(userId, registrationState, planState).Result;

        var preconditionsMet = ArePreconditionsMet(registrationState, planState);

        return (!result.Success
            && !preconditionsMet
            && result.FailureReason == CheckoutFailureReason.PlanNotAvailable).ToProperty()
            .Label($"PlanId={planId.Get}: Expected FailureReason=PlanNotAvailable (plan not found), Got FailureReason={result.FailureReason}");
    }

    #endregion

    #region Property 1f: When all preconditions are met, service attempts Stripe session creation

    /// <summary>
    /// Property 1f: For any incomplete PendingRegistration referencing a Plan with a valid
    /// (non-null, non-empty) StripePriceId, the CheckoutService SHALL attempt to create a
    /// Stripe Checkout Session. Since we use a fake API key in tests, this results in a
    /// StripeApiError — which confirms the precondition checks passed and the service
    /// proceeded to the Stripe API call.
    /// **Validates: Requirements 1.1, 1.6, 1.8**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidStripePriceArbitrary) })]
    public Property AllPreconditionsMet_AttemptsStripeSessionCreation(ValidCheckoutInput input)
    {
        var userId = $"user-valid-{input.PlanId}";
        var registrationState = new PendingRegistrationState(Exists: true, IsCompleted: false, PlanId: input.PlanId);
        var planState = new PlanState(Exists: true, StripePriceId: input.StripePriceId);

        var result = ExecuteCheckoutPreconditionCheck(userId, registrationState, planState).Result;

        var preconditionsMet = ArePreconditionsMet(registrationState, planState);

        // When preconditions are met, the service attempts to call Stripe API.
        // With a fake key, this results in either:
        // - StripeApiError (Stripe rejects the fake key) — confirms preconditions passed
        // - Success (unlikely in test, but would also confirm preconditions passed)
        var passedPreconditions = result.Success || result.FailureReason == CheckoutFailureReason.StripeApiError;

        return (preconditionsMet && passedPreconditions).ToProperty()
            .Label($"PlanId={input.PlanId}, StripePriceId='{input.StripePriceId}': " +
                   $"Expected preconditions met and Stripe API attempted, " +
                   $"Got Success={result.Success}, FailureReason={result.FailureReason}");
    }

    #endregion

    #region Property 1g: Completeness — failure reason matches precondition state

    /// <summary>
    /// Property 1g: For any combination of PendingRegistration state and Plan state,
    /// the CheckoutService SHALL return the correct FailureReason that corresponds to
    /// the first failing precondition (checked in order: exists → not completed → plan available).
    /// **Validates: Requirements 1.1, 1.6, 1.8**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(CheckoutPreconditionArbitrary) })]
    public Property FailureReason_MatchesPreconditionState(CheckoutPreconditionInput input)
    {
        var userId = $"user-complete-{input.Seed}";

        var result = ExecuteCheckoutPreconditionCheck(userId, input.Registration, input.Plan).Result;

        var preconditionsMet = ArePreconditionsMet(input.Registration, input.Plan);
        var expectedReason = GetExpectedFailureReason(input.Registration, input.Plan);

        if (preconditionsMet)
        {
            // When preconditions are met, service attempts Stripe API (fails with fake key)
            var passedPreconditions = result.Success || result.FailureReason == CheckoutFailureReason.StripeApiError;
            return passedPreconditions.ToProperty()
                .Label($"Preconditions met: Expected Stripe API attempt, Got Success={result.Success}, FailureReason={result.FailureReason}");
        }
        else
        {
            // When preconditions are NOT met, result should be failure with correct reason
            return (!result.Success && result.FailureReason == expectedReason).ToProperty()
                .Label($"Preconditions not met: Expected FailureReason={expectedReason}, Got Success={result.Success}, FailureReason={result.FailureReason}");
        }
    }

    #endregion
}

#region Custom Arbitraries

/// <summary>
/// Input for testing empty/whitespace StripePriceId values.
/// </summary>
public class EmptyStripePriceInput
{
    public int PlanId { get; set; }
    public string EmptyPriceId { get; set; } = null!;

    public override string ToString() => $"(PlanId={PlanId}, EmptyPriceId='{EmptyPriceId}')";
}

/// <summary>
/// Arbitrary that generates empty/whitespace StripePriceId values.
/// </summary>
public class EmptyStripePriceArbitrary
{
    public static Arbitrary<EmptyStripePriceInput> EmptyStripePriceInputs()
    {
        var emptyPriceGen = Gen.Elements("", " ", "  ", "\t", "\n", "   ");
        var planIdGen = Gen.Choose(1, 1000);

        var gen = from planId in planIdGen
                  from emptyPrice in emptyPriceGen
                  select new EmptyStripePriceInput
                  {
                      PlanId = planId,
                      EmptyPriceId = emptyPrice
                  };

        return Arb.From(gen);
    }
}

/// <summary>
/// Input for testing valid checkout scenarios.
/// </summary>
public class ValidCheckoutInput
{
    public int PlanId { get; set; }
    public string StripePriceId { get; set; } = null!;

    public override string ToString() => $"(PlanId={PlanId}, StripePriceId='{StripePriceId}')";
}

/// <summary>
/// Arbitrary that generates valid StripePriceId values (non-null, non-empty).
/// </summary>
public class ValidStripePriceArbitrary
{
    public static Arbitrary<ValidCheckoutInput> ValidCheckoutInputs()
    {
        var priceIdGen = Gen.Elements(
            "price_abc123",
            "price_xyz789",
            "price_1MoBy5LkdIwHu7ixZhnattbS",
            "price_monthly_pro",
            "price_annual_enterprise");

        var planIdGen = Gen.Choose(1, 100);

        var gen = from planId in planIdGen
                  from priceId in priceIdGen
                  select new ValidCheckoutInput
                  {
                      PlanId = planId,
                      StripePriceId = priceId
                  };

        return Arb.From(gen);
    }
}

/// <summary>
/// Input combining all possible precondition states for completeness testing.
/// </summary>
public class CheckoutPreconditionInput
{
    public int Seed { get; set; }
    public CheckoutPreconditionPropertyTests.PendingRegistrationState Registration { get; set; } = null!;
    public CheckoutPreconditionPropertyTests.PlanState Plan { get; set; } = null!;

    public override string ToString() =>
        $"(Registration=[Exists={Registration.Exists}, IsCompleted={Registration.IsCompleted}], Plan=[Exists={Plan.Exists}, StripePriceId='{Plan.StripePriceId}'])";
}

/// <summary>
/// Arbitrary that generates all combinations of checkout precondition states.
/// Covers: registration exists/not, completed/not, plan exists/not, StripePriceId valid/null/empty.
/// </summary>
public class CheckoutPreconditionArbitrary
{
    public static Arbitrary<CheckoutPreconditionInput> CheckoutPreconditionInputs()
    {
        var existsGen = Gen.Elements(true, false);
        var completedGen = Gen.Elements(true, false);
        var planExistsGen = Gen.Elements(true, false);
        var stripePriceGen = Gen.Frequency(
            Tuple.Create(3, Gen.Elements<string?>("price_abc123", "price_xyz789", "price_monthly_pro")),
            Tuple.Create(1, Gen.Constant<string?>(null)),
            Tuple.Create(1, Gen.Elements<string?>("", " ", "  "))
        );
        var planIdGen = Gen.Choose(1, 100);
        var seedGen = Gen.Choose(1, 100000);

        var gen = from seed in seedGen
                  from exists in existsGen
                  from completed in completedGen
                  from planExists in planExistsGen
                  from stripePrice in stripePriceGen
                  from planId in planIdGen
                  select new CheckoutPreconditionInput
                  {
                      Seed = seed,
                      Registration = new CheckoutPreconditionPropertyTests.PendingRegistrationState(exists, completed, planId),
                      Plan = new CheckoutPreconditionPropertyTests.PlanState(planExists, stripePrice)
                  };

        return Arb.From(gen);
    }
}

#endregion
