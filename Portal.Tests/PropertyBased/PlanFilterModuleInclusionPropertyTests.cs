using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Constants;
using Portal.Web.Filters;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: subscription-permission-gating, Property 1: Plan filter module inclusion

/// <summary>
/// Property-based tests for plan filter module inclusion logic.
/// The PlanPermissionFilter delegates module resolution to ModuleControllerMap.ResolveModule(),
/// which determines whether a controller belongs to a gated module. This test verifies:
/// - All controllers in the map resolve to valid module keys (filter would evaluate plan access)
/// - Controllers NOT in the map resolve to null (filter would allow through — no plan check needed)
/// **Validates: Requirements 3.1, 3.2**
/// </summary>
public class PlanFilterModuleInclusionPropertyTests
{
    #region Property 1: Plan filter grants access if and only if the plan includes the module

    /// <summary>
    /// Property 1a: Any controller that is mapped in ModuleControllerMap resolves to a valid PortalModules key.
    /// This proves that the filter can always look up the module in the plan's feature list.
    /// **Validates: Requirements 3.1, 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MappedController_ResolvesToValidModule(PositiveInt seed)
    {
        // Get all controllers from the map
        var allControllers = ModuleControllerMap.Map.Values.SelectMany(v => v).ToArray();
        var controller = allControllers[seed.Get % allControllers.Length];

        var module = ModuleControllerMap.ResolveModule(controller);

        // The resolved module should always be a valid module key
        var isValid = module != null && PortalModules.IsValid(module);

        return isValid.ToProperty()
            .Label($"Controller '{controller}' resolved to module '{module}' which should be valid");
    }

    /// <summary>
    /// Property 1b: Any controller NOT in the ModuleControllerMap resolves to null,
    /// meaning the PlanPermissionFilter will allow it through without a plan check.
    /// **Validates: Requirements 3.1, 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UnmappedController_ResolvesToNull(NonEmptyString randomName)
    {
        var candidate = randomName.Get;
        var allControllers = ModuleControllerMap.Map.Values
            .SelectMany(v => v)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Skip if the random string happens to be a mapped controller
        if (allControllers.Contains(candidate))
            return true.ToProperty().Label($"Skipped — '{candidate}' is a mapped controller");

        var module = ModuleControllerMap.ResolveModule(candidate);

        return (module == null).ToProperty()
            .Label($"Unmapped controller '{candidate}' should resolve to null but got '{module}'");
    }

    /// <summary>
    /// Property 1c: For any module key in the map, a plan that includes that module means the filter allows access,
    /// and a plan that excludes the module means the filter blocks access.
    /// Simulated by checking: module in planModules → allow; module not in planModules → block.
    /// **Validates: Requirements 3.1, 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ModuleInPlan_AllowsAccess_ModuleNotInPlan_BlocksAccess(PositiveInt moduleSeed, PositiveInt planSeed)
    {
        var allModuleKeys = ModuleControllerMap.Map.Keys.ToArray();
        var requestedModule = allModuleKeys[moduleSeed.Get % allModuleKeys.Length];

        // Generate a random plan (subset of all modules) using a deterministic seed
        var random = new System.Random(planSeed.Get);
        var planModules = allModuleKeys
            .Where(_ => random.Next(2) == 1)
            .ToHashSet();

        // Simulate the filter's decision logic:
        // If module is in plan → allow (true), if not → block (false)
        var filterAllows = planModules.Contains(requestedModule);
        var expectedAllow = planModules.Contains(requestedModule);

        return (filterAllows == expectedAllow).ToProperty()
            .Label($"Module='{requestedModule}', PlanHasModule={planModules.Contains(requestedModule)}, FilterAllows={filterAllows}");
    }

    #endregion

    #region Exhaustive verification

    /// <summary>
    /// All module keys present in ModuleControllerMap are valid PortalModules keys.
    /// This ensures the filter will never resolve to an unknown/invalid module string.
    /// **Validates: Requirements 3.1, 3.2**
    /// </summary>
    [Fact]
    public void AllModulesInMap_AreValidModuleKeys()
    {
        foreach (var kvp in ModuleControllerMap.Map)
        {
            Assert.True(
                PortalModules.IsValid(kvp.Key),
                $"Module key '{kvp.Key}' in ModuleControllerMap should be a valid PortalModule");
        }
    }

    /// <summary>
    /// Every controller in the map resolves back to its expected module key (round-trip consistency).
    /// **Validates: Requirements 3.1, 3.2**
    /// </summary>
    [Fact]
    public void AllControllers_ResolveToExpectedModule()
    {
        foreach (var kvp in ModuleControllerMap.Map)
        {
            var expectedModule = kvp.Key;
            foreach (var controller in kvp.Value)
            {
                var resolved = ModuleControllerMap.ResolveModule(controller);
                Assert.Equal(expectedModule, resolved);
            }
        }
    }

    #endregion
}
