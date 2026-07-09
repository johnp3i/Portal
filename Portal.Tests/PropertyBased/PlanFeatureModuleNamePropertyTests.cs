using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Constants;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: subscription-plans, Property 4: ModuleName validation

/// <summary>
/// Property-based tests for PlanFeature ModuleName validation.
/// For any string assigned to PlanFeature.ModuleName, the system SHALL accept it if and only if
/// it is a member of the defined PortalModules.All set (customer, quotation, invoice, revenue,
/// purchase, vat, credit, audit, products).
/// **Validates: Requirements 2.3**
/// </summary>
public class PlanFeatureModuleNamePropertyTests
{
    private static readonly HashSet<string> ValidModules = new(PortalModules.All, StringComparer.Ordinal);

    #region Property 4a: Valid module names are accepted

    /// <summary>
    /// Property 4a: For any module name drawn from PortalModules.All, PortalModules.IsValid SHALL return true.
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidModuleNames_AreAccepted(PositiveInt seed)
    {
        // Pick a valid module name from the defined set
        var index = seed.Get % PortalModules.All.Length;
        var moduleName = PortalModules.All[index];

        var isValid = PortalModules.IsValid(moduleName);

        return isValid.ToProperty()
            .Label($"ModuleName='{moduleName}' should be valid but IsValid returned {isValid}");
    }

    #endregion

    #region Property 4b: Invalid module names are rejected

    /// <summary>
    /// Property 4b: For any random string that is NOT a member of PortalModules.All,
    /// PortalModules.IsValid SHALL return false.
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvalidModuleNames_AreRejected(NonEmptyString randomString)
    {
        var candidate = randomString.Get;

        // Skip if the random string happens to be a valid module name
        if (ValidModules.Contains(candidate))
            return true.ToProperty().Label($"Skipped — '{candidate}' is a valid module name");

        var isValid = PortalModules.IsValid(candidate);

        return (!isValid).ToProperty()
            .Label($"ModuleName='{candidate}' should be invalid but IsValid returned {isValid}");
    }

    #endregion

    #region Property 4c: Null or empty strings are rejected

    /// <summary>
    /// Property 4c: Null and empty strings SHALL NOT be accepted as valid module names.
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Fact]
    public void NullAndEmpty_AreRejected()
    {
        Assert.False(PortalModules.IsValid(null!));
        Assert.False(PortalModules.IsValid(string.Empty));
    }

    #endregion

    #region Property 4d: Case sensitivity — uppercase variants are rejected

    /// <summary>
    /// Property 4d: Module names are case-sensitive. Uppercase or mixed-case variants of valid
    /// module names SHALL be rejected.
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CaseSensitive_UppercaseVariants_AreRejected(PositiveInt seed)
    {
        var index = seed.Get % PortalModules.All.Length;
        var validModule = PortalModules.All[index];

        // Create an uppercase variant
        var uppercaseVariant = validModule.ToUpperInvariant();

        // Since all valid modules are lowercase, the uppercase variant should be invalid
        var isValid = PortalModules.IsValid(uppercaseVariant);

        return (!isValid).ToProperty()
            .Label($"ModuleName='{uppercaseVariant}' (uppercase of '{validModule}') should be invalid but IsValid returned {isValid}");
    }

    #endregion

    #region Property 4e: The valid set contains all modules

    /// <summary>
    /// Property 4e: The PortalModules.All set SHALL contain all 24 defined modules.
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Fact]
    public void ValidModuleSet_ContainsAllModules()
    {
        Assert.Equal(24, PortalModules.All.Length);

        // Original 9 modules
        Assert.Contains("customer", PortalModules.All);
        Assert.Contains("quotation", PortalModules.All);
        Assert.Contains("invoice", PortalModules.All);
        Assert.Contains("revenue", PortalModules.All);
        Assert.Contains("purchase", PortalModules.All);
        Assert.Contains("vat", PortalModules.All);
        Assert.Contains("credit", PortalModules.All);
        Assert.Contains("audit", PortalModules.All);
        Assert.Contains("products", PortalModules.All);

        // 14 new module keys
        Assert.Contains("payment_link_manual", PortalModules.All);
        Assert.Contains("payment_reminder_manual", PortalModules.All);
        Assert.Contains("payment_link_auto", PortalModules.All);
        Assert.Contains("payment_reminder_auto", PortalModules.All);
        Assert.Contains("cashflow", PortalModules.All);
        Assert.Contains("pnl", PortalModules.All);
        Assert.Contains("expense_insights", PortalModules.All);
        Assert.Contains("attachments", PortalModules.All);
        Assert.Contains("client_portal", PortalModules.All);
        Assert.Contains("activity_timeline", PortalModules.All);
        Assert.Contains("audit_log", PortalModules.All);
        Assert.Contains("api", PortalModules.All);
        Assert.Contains("webhooks", PortalModules.All);
        Assert.Contains("multi_currency", PortalModules.All);
        Assert.Contains("schedule_payments", PortalModules.All);
    }

    #endregion
}
