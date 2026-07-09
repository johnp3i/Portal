using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Constants;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: subscription-permission-gating, Property 8: Module key validation

/// <summary>
/// Property-based tests for module key validation.
/// For any string, PortalModules.IsValid(s) SHALL return true if and only if s is contained
/// in the PortalModules.All array.
/// **Validates: Requirements 8.2**
/// </summary>
public class ModuleKeyValidationPropertyTests
{
    private static readonly HashSet<string> ValidModules = new(PortalModules.All, StringComparer.Ordinal);

    /// <summary>
    /// Property 8a: For any module key drawn from PortalModules.All, IsValid SHALL return true.
    /// **Validates: Requirements 8.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidModuleKey_ReturnsTrue(PositiveInt seed)
    {
        var index = seed.Get % PortalModules.All.Length;
        var module = PortalModules.All[index];
        return PortalModules.IsValid(module).ToProperty()
            .Label($"Module '{module}' should be valid");
    }

    /// <summary>
    /// Property 8b: For any random string NOT in PortalModules.All, IsValid SHALL return false.
    /// **Validates: Requirements 8.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvalidModuleKey_ReturnsFalse(NonEmptyString randomString)
    {
        var candidate = randomString.Get;
        if (ValidModules.Contains(candidate))
            return true.ToProperty().Label($"Skipped valid module '{candidate}'");

        return (!PortalModules.IsValid(candidate)).ToProperty()
            .Label($"Module '{candidate}' should be invalid");
    }

    /// <summary>
    /// Property 8c: Null input SHALL return false.
    /// **Validates: Requirements 8.2**
    /// </summary>
    [Fact]
    public void NullInput_ReturnsFalse()
    {
        Assert.False(PortalModules.IsValid(null!));
    }

    /// <summary>
    /// Property 8d: Empty string SHALL return false.
    /// **Validates: Requirements 8.2**
    /// </summary>
    [Fact]
    public void EmptyString_ReturnsFalse()
    {
        Assert.False(PortalModules.IsValid(string.Empty));
    }

    /// <summary>
    /// Property 8e: PortalModules.All SHALL contain exactly 24 modules.
    /// **Validates: Requirements 8.2**
    /// </summary>
    [Fact]
    public void All_Contains_23_Modules()
    {
        Assert.Equal(24, PortalModules.All.Length);
    }
}
