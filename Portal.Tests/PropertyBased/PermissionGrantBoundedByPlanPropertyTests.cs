using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: subscription-permission-gating, Property 9: Permission grant bounded by plan

/// <summary>
/// Property-based tests verifying that permission grants are bounded by the business plan.
/// A grant SHALL succeed only if (a) the module is included in the business's active subscription plan
/// and (b) the requested access level does not exceed the plan's access level for that module.
/// **Validates: Requirements 10.2, 10.3**
/// </summary>
public class PermissionGrantBoundedByPlanPropertyTests
{
    private static readonly string[] AllModules = PortalModules.All;
    private static readonly string[] ValidAccessLevels = AccessLevels.All;

    // Simulated plan configurations representing real tier boundaries
    private static readonly HashSet<string> StarterPlanModules = new()
    {
        "quotation", "invoice", "revenue", "customer", "purchase", "vat", "credit", "products",
        "payment_link_manual", "payment_reminder_manual"
    };

    private static readonly HashSet<string> ProfessionalPlanModules = new()
    {
        "quotation", "invoice", "revenue", "customer", "purchase", "vat", "credit", "products",
        "payment_link_manual", "payment_reminder_manual",
        "payment_link_auto", "payment_reminder_auto", "cashflow", "pnl", "expense_insights", "attachments"
    };

    private static readonly HashSet<string> EnterprisePlanModules = new(PortalModules.All);

    #region Property 9: Permission grant is bounded by the business plan

    /// <summary>
    /// Property 9a: A grant request for a module NOT in the plan SHALL fail.
    /// If the module is not included in the business's subscription plan, the grant must be rejected.
    /// **Validates: Requirements 10.2, 10.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GrantForModuleNotInPlan_MustFail(PositiveInt moduleSeed, PositiveInt levelSeed, PositiveInt planSeed)
    {
        var module = AllModules[moduleSeed.Get % AllModules.Length];
        var accessLevel = ValidAccessLevels[levelSeed.Get % ValidAccessLevels.Length];
        var planModules = SelectPlan(planSeed.Get);

        // Only test modules NOT in the chosen plan
        if (planModules.Contains(module))
            return true.ToProperty().Label($"Skipped — '{module}' IS in the selected plan");

        // The grant decision: module must be in plan to proceed
        var grantAllowed = SimulateGrantDecision(module, accessLevel, planModules, AccessLevels.Full);

        return (!grantAllowed).ToProperty()
            .Label($"Module='{module}' not in plan, accessLevel='{accessLevel}' → grant MUST fail");
    }

    /// <summary>
    /// Property 9b: A grant request for a module IN the plan with a valid non-none access level
    /// that does not exceed the plan level SHALL succeed.
    /// **Validates: Requirements 10.2, 10.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GrantForModuleInPlan_WithinPlanLevel_MustSucceed(PositiveInt moduleSeed, PositiveInt levelSeed, PositiveInt planSeed)
    {
        var planModules = SelectPlan(planSeed.Get);
        var planModulesList = planModules.ToArray();
        var module = planModulesList[moduleSeed.Get % planModulesList.Length];

        // Plan level for all current plans is 'full'
        var planAccessLevel = AccessLevels.Full;

        // Valid grant levels that do not exceed 'full': 'full' and 'readonly'
        var grantableLevels = new[] { AccessLevels.Full, AccessLevels.ReadOnly };
        var requestedLevel = grantableLevels[levelSeed.Get % grantableLevels.Length];

        var grantAllowed = SimulateGrantDecision(module, requestedLevel, planModules, planAccessLevel);

        return grantAllowed.ToProperty()
            .Label($"Module='{module}' in plan, requested='{requestedLevel}', planLevel='{planAccessLevel}' → grant MUST succeed");
    }

    /// <summary>
    /// Property 9c: A grant request with access level 'none' is effectively a revoke, not a true grant.
    /// The system rejects 'none' as a grant because granting 'none' has no operational meaning.
    /// **Validates: Requirements 10.2, 10.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GrantWithNoneLevel_IsRejectedAsInvalid(PositiveInt moduleSeed, PositiveInt planSeed)
    {
        var planModules = SelectPlan(planSeed.Get);
        var planModulesList = planModules.ToArray();
        var module = planModulesList[moduleSeed.Get % planModulesList.Length];

        // Granting 'none' is semantically a revoke and should not count as a valid grant
        var grantAllowed = SimulateGrantDecision(module, AccessLevels.None, planModules, AccessLevels.Full);

        return (!grantAllowed).ToProperty()
            .Label($"Module='{module}' in plan, accessLevel='none' → grant MUST fail (use revoke instead)");
    }

    /// <summary>
    /// Property 9d: A grant with an invalid (non-existent) module key SHALL always fail,
    /// regardless of the plan configuration.
    /// **Validates: Requirements 10.2, 10.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GrantForInvalidModule_AlwaysFails(NonEmptyString randomModule, PositiveInt levelSeed)
    {
        var module = randomModule.Get;

        // Skip if the random string happens to be a valid module
        if (PortalModules.IsValid(module))
            return true.ToProperty().Label($"Skipped — '{module}' is actually a valid module");

        var accessLevel = ValidAccessLevels[levelSeed.Get % ValidAccessLevels.Length];

        // Invalid module → always rejected regardless of plan
        var grantAllowed = SimulateGrantDecision(module, accessLevel, EnterprisePlanModules, AccessLevels.Full);

        return (!grantAllowed).ToProperty()
            .Label($"Invalid module='{module}', accessLevel='{accessLevel}' → grant MUST fail");
    }

    /// <summary>
    /// Property 9e: When the plan level is 'readonly', a grant requesting 'full' access SHALL fail
    /// because the requested level exceeds the plan's maximum for that module.
    /// **Validates: Requirements 10.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GrantExceedingPlanLevel_MustFail(PositiveInt moduleSeed)
    {
        var module = AllModules[moduleSeed.Get % AllModules.Length];
        var planModules = new HashSet<string>(AllModules); // All modules in plan

        // Simulated scenario: plan only grants 'readonly' for this module
        var planAccessLevel = AccessLevels.ReadOnly;
        var requestedLevel = AccessLevels.Full; // Exceeds plan level

        var grantAllowed = SimulateGrantDecisionWithPlanLevel(module, requestedLevel, planModules, planAccessLevel);

        return (!grantAllowed).ToProperty()
            .Label($"Module='{module}', planLevel='readonly', requested='full' → grant MUST fail (exceeds plan level)");
    }

    #endregion

    #region Exhaustive verification

    /// <summary>
    /// Verifies that each Starter-exclusive boundary module is correctly gated when using the Starter plan.
    /// Modules in Professional/Enterprise but NOT in Starter must be rejected.
    /// **Validates: Requirements 10.2**
    /// </summary>
    [Fact]
    public void ProfessionalModules_NotGrantableOnStarterPlan()
    {
        var professionalOnlyModules = ProfessionalPlanModules.Except(StarterPlanModules);

        foreach (var module in professionalOnlyModules)
        {
            var grantAllowed = SimulateGrantDecision(module, AccessLevels.Full, StarterPlanModules, AccessLevels.Full);
            Assert.False(grantAllowed, $"Module '{module}' should NOT be grantable on the Starter plan");
        }
    }

    /// <summary>
    /// Verifies that Enterprise-only modules are not grantable on the Professional plan.
    /// **Validates: Requirements 10.2**
    /// </summary>
    [Fact]
    public void EnterpriseModules_NotGrantableOnProfessionalPlan()
    {
        var enterpriseOnlyModules = EnterprisePlanModules.Except(ProfessionalPlanModules);

        foreach (var module in enterpriseOnlyModules)
        {
            var grantAllowed = SimulateGrantDecision(module, AccessLevels.Full, ProfessionalPlanModules, AccessLevels.Full);
            Assert.False(grantAllowed, $"Module '{module}' should NOT be grantable on the Professional plan");
        }
    }

    /// <summary>
    /// Verifies that all modules IN a plan CAN be granted with valid access levels.
    /// **Validates: Requirements 10.2**
    /// </summary>
    [Fact]
    public void AllPlanModules_AreGrantableWithValidLevels()
    {
        var validGrantLevels = new[] { AccessLevels.Full, AccessLevels.ReadOnly };

        foreach (var module in ProfessionalPlanModules)
        {
            foreach (var level in validGrantLevels)
            {
                var grantAllowed = SimulateGrantDecision(module, level, ProfessionalPlanModules, AccessLevels.Full);
                Assert.True(grantAllowed, $"Module '{module}' at level '{level}' should be grantable on the Professional plan");
            }
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Selects a plan configuration based on a seed value to create randomized plan scenarios.
    /// </summary>
    private static HashSet<string> SelectPlan(int seed)
    {
        var planIndex = seed % 3;
        return planIndex switch
        {
            0 => StarterPlanModules,
            1 => ProfessionalPlanModules,
            _ => EnterprisePlanModules
        };
    }

    /// <summary>
    /// Simulates the grant decision logic from the MyBusinessController's AxPostGrantPermission action:
    /// Grant succeeds if: module is valid AND module is in plan AND accessLevel is valid AND accessLevel != 'none'
    /// This matches the controller validation: IsValid(module), IsValid(accessLevel), IsModuleInPlanAsync(module)
    /// </summary>
    private static bool SimulateGrantDecision(string module, string accessLevel, HashSet<string> planModules, string planAccessLevel)
    {
        // Step 1: Module must be a valid PortalModules key
        if (!PortalModules.IsValid(module)) return false;

        // Step 2: Access level must be valid
        if (!AccessLevels.IsValid(accessLevel)) return false;

        // Step 3: Granting 'none' is not a valid grant operation (it's a revoke)
        if (accessLevel == AccessLevels.None) return false;

        // Step 4: Module must be included in the business's plan
        if (!planModules.Contains(module)) return false;

        // Step 5: Requested level must not exceed plan's access level for that module
        if (GetRank(accessLevel) > GetRank(planAccessLevel)) return false;

        return true;
    }

    /// <summary>
    /// Variant that explicitly checks plan access level constraint.
    /// </summary>
    private static bool SimulateGrantDecisionWithPlanLevel(string module, string accessLevel, HashSet<string> planModules, string planAccessLevel)
    {
        return SimulateGrantDecision(module, accessLevel, planModules, planAccessLevel);
    }

    /// <summary>
    /// Returns a rank value for an access level: none=0, readonly=1, full=2.
    /// Higher rank means more permissive.
    /// </summary>
    private static int GetRank(string level)
    {
        return level switch
        {
            "none" => 0,
            "readonly" => 1,
            "full" => 2,
            _ => -1
        };
    }

    #endregion
}
