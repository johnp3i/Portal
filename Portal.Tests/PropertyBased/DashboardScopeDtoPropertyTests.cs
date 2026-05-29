using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Models;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: scoped-dashboard, Properties 1-3: DashboardScopeDto correctness

/// <summary>
/// Property-based tests for DashboardScopeDto ensuring permission-to-visibility mapping,
/// privileged user full access, and HasAnyKpiSection correctness.
/// </summary>
public class DashboardScopeDtoPropertyTests
{
    #region Test Infrastructure

    /// <summary>
    /// All modules that DashboardScopeDto maps from permissions.
    /// </summary>
    private static readonly string[] ScopedModules =
    {
        PortalModules.Revenue,
        PortalModules.Invoice,
        PortalModules.Quotation,
        PortalModules.Purchase,
        PortalModules.Vat,
        PortalModules.Customer
    };

    /// <summary>
    /// Generates a random access level from the valid set: "full", "readonly", "none".
    /// </summary>
    private static Gen<string> GenAccessLevel =>
        Gen.Elements(AccessLevels.Full, AccessLevels.ReadOnly, AccessLevels.None);

    /// <summary>
    /// Generates a permission dictionary with a random access level for each scoped module.
    /// </summary>
    private static Gen<Dictionary<string, string>> GenPermissionDictionary =>
        Gen.Sequence(ScopedModules.Select(module =>
            GenAccessLevel.Select(level => new KeyValuePair<string, string>(module, level))))
        .Select(pairs => pairs.ToDictionary(p => p.Key, p => p.Value));

    /// <summary>
    /// Gets the scope flag value for a given module from a DashboardScopeDto.
    /// </summary>
    private static bool GetScopeFlag(DashboardScopeDto scope, string module) => module switch
    {
        PortalModules.Revenue => scope.ShowRevenue,
        PortalModules.Invoice => scope.ShowInvoice,
        PortalModules.Quotation => scope.ShowQuotation,
        PortalModules.Purchase => scope.ShowPurchase,
        PortalModules.Vat => scope.ShowVat,
        PortalModules.Customer => scope.ShowCustomer,
        _ => throw new ArgumentException($"Unknown module: {module}")
    };

    #endregion

    #region Property 1: Permission-to-visibility mapping is a biconditional on access level

    /// <summary>
    /// Feature: scoped-dashboard, Property 1: Permission-to-visibility mapping is a biconditional on access level
    /// For any module and any access level, the corresponding scope flag is true iff the access level is not "none".
    /// **Validates: Requirements 2.1, 2.2, 3.1, 3.2, 4.1, 4.2, 5.1, 5.2, 6.1, 6.2, 9.1, 9.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PermissionToVisibility_IsBiconditionalOnAccessLevel()
    {
        return Prop.ForAll(GenPermissionDictionary.ToArbitrary(), permissions =>
        {
            var scope = DashboardScopeDto.FromPermissions(permissions);

            var allCorrect = ScopedModules.All(module =>
            {
                var accessLevel = permissions[module];
                var expectedVisible = accessLevel != AccessLevels.None;
                var actualVisible = GetScopeFlag(scope, module);
                return actualVisible == expectedVisible;
            });

            return allCorrect.ToProperty()
                .Label($"Permissions: {string.Join(", ", permissions.Select(p => $"{p.Key}={p.Value}"))}");
        });
    }

    #endregion

    #region Property 2: Privileged users always receive full access scope

    /// <summary>
    /// Feature: scoped-dashboard, Property 2: Privileged users always receive full access scope
    /// DashboardScopeDto.FullAccess() always returns all flags as true regardless of any input.
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FullAccess_AlwaysReturnsAllFlagsTrue(int arbitraryInput)
    {
        // The arbitrary input demonstrates that FullAccess is independent of any external state
        var scope = DashboardScopeDto.FullAccess();

        var allTrue = scope.ShowRevenue
                   && scope.ShowInvoice
                   && scope.ShowQuotation
                   && scope.ShowPurchase
                   && scope.ShowVat
                   && scope.ShowCustomer
                   && scope.HasAnyKpiSection;

        return allTrue.ToProperty()
            .Label($"FullAccess flags: Revenue={scope.ShowRevenue}, Invoice={scope.ShowInvoice}, " +
                   $"Quotation={scope.ShowQuotation}, Purchase={scope.ShowPurchase}, " +
                   $"Vat={scope.ShowVat}, Customer={scope.ShowCustomer}, " +
                   $"HasAnyKpiSection={scope.HasAnyKpiSection}");
    }

    #endregion

    #region Property 3: HasAnyKpiSection is true iff at least one KPI-bearing module is visible

    /// <summary>
    /// Feature: scoped-dashboard, Property 3: HasAnyKpiSection is true iff at least one KPI-bearing module is visible
    /// For any DashboardScopeDto with random boolean flags, HasAnyKpiSection equals
    /// (ShowRevenue || ShowInvoice || ShowQuotation || ShowPurchase || ShowVat).
    /// **Validates: Requirements 8.1, 8.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property HasAnyKpiSection_TrueIffAtLeastOneKpiBearingModuleVisible(
        bool showRevenue, bool showInvoice, bool showQuotation,
        bool showPurchase, bool showVat, bool showCustomer)
    {
        var scope = new DashboardScopeDto
        {
            ShowRevenue = showRevenue,
            ShowInvoice = showInvoice,
            ShowQuotation = showQuotation,
            ShowPurchase = showPurchase,
            ShowVat = showVat,
            ShowCustomer = showCustomer
        };

        var expectedHasAnyKpi = showRevenue || showInvoice || showQuotation || showPurchase || showVat;
        var actualHasAnyKpi = scope.HasAnyKpiSection;

        return (actualHasAnyKpi == expectedHasAnyKpi).ToProperty()
            .Label($"HasAnyKpiSection={actualHasAnyKpi}, expected={expectedHasAnyKpi} " +
                   $"(Revenue={showRevenue}, Invoice={showInvoice}, Quotation={showQuotation}, " +
                   $"Purchase={showPurchase}, Vat={showVat}, Customer={showCustomer})");
    }

    #endregion
}
