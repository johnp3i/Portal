using FsCheck;
using FsCheck.Xunit;
using Portal.Web.Filters;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: subscription-permission-gating, Property 2: Non-module controllers bypass plan checks

/// <summary>
/// Property-based tests for non-module controller bypass behavior.
/// For any request targeting a non-module controller (Home, Account, Demo, Admin, MyBusiness, Billing, SetupWizard, Dashboard),
/// the PlanPermissionFilter SHALL allow the request without evaluating plan permissions.
/// This is verified by confirming that ModuleControllerMap.ResolveModule returns null for all exempt controllers.
/// **Validates: Requirements 3.4**
/// </summary>
public class NonModuleControllerBypassPropertyTests
{
    /// <summary>
    /// The exempt controllers that must NOT resolve to any module.
    /// These match the NonModuleControllers set in PlanPermissionFilter.
    /// </summary>
    private static readonly string[] ExemptControllers = { "Home", "Account", "Demo", "Admin", "MyBusiness", "Billing", "SetupWizard", "Dashboard" };

    #region Property 2: Non-module controllers bypass plan checks

    /// <summary>
    /// Property 2: For any controller drawn from the exempt list,
    /// ModuleControllerMap.ResolveModule SHALL return null, confirming that the controller
    /// is not mapped to any module and will bypass plan permission checks.
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExemptController_IsNotResolvedToModule(PositiveInt seed)
    {
        var controller = ExemptControllers[seed.Get % ExemptControllers.Length];
        var module = ModuleControllerMap.ResolveModule(controller);

        return (module == null).ToProperty()
            .Label($"Exempt controller '{controller}' should not resolve to any module but got '{module}'");
    }

    /// <summary>
    /// Property 2a: Case-insensitive verification — exempt controllers bypass regardless of casing.
    /// ModuleControllerMap uses StringComparer.OrdinalIgnoreCase, so we verify that
    /// various casings of exempt controller names also resolve to null.
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExemptController_CaseInsensitive_IsNotResolvedToModule(PositiveInt seed)
    {
        var controller = ExemptControllers[seed.Get % ExemptControllers.Length];

        // Apply random casing transformation
        var cased = (seed.Get % 3) switch
        {
            0 => controller.ToUpperInvariant(),
            1 => controller.ToLowerInvariant(),
            _ => controller
        };

        var module = ModuleControllerMap.ResolveModule(cased);

        return (module == null).ToProperty()
            .Label($"Exempt controller '{cased}' (original: '{controller}') should not resolve to any module but got '{module}'");
    }

    #endregion

    #region Exhaustive verification

    /// <summary>
    /// Exhaustive test verifying all exempt controllers resolve to null.
    /// Ensures no exempt controller was accidentally added to ModuleControllerMap.
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Fact]
    public void AllExemptControllers_ResolveToNull()
    {
        foreach (var controller in ExemptControllers)
        {
            var module = ModuleControllerMap.ResolveModule(controller);
            Assert.Null(module);
        }
    }

    #endregion
}
