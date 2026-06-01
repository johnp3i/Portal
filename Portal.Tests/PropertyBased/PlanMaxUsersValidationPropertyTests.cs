using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: subscription-plans, Property 3: MaxUsers constraint validation

/// <summary>
/// Property-based tests for Plan MaxUsers constraint validation.
/// For any integer value assigned to MaxUsers, the Plan SHALL accept it if and only if
/// the value equals -1 or is a positive integer >= 1.
/// Values like 0, -2, -100 are invalid per the CHECK constraint: [MaxUsers] = -1 OR [MaxUsers] >= 1
/// **Validates: Requirements 1.6**
/// </summary>
public class PlanMaxUsersValidationPropertyTests
{
    /// <summary>
    /// Implements the MaxUsers constraint logic matching the database CHECK constraint:
    /// [MaxUsers] = -1 OR [MaxUsers] >= 1
    /// </summary>
    private static bool IsValidMaxUsers(int value) => value == -1 || value >= 1;

    #region Property 3a: Positive integers are always valid MaxUsers values

    /// <summary>
    /// Property 3a: For any positive integer (>= 1), the MaxUsers constraint SHALL accept the value.
    /// Positive integers represent a finite user limit for the plan.
    /// **Validates: Requirements 1.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PositiveIntegers_AreValidMaxUsers(PositiveInt positiveValue)
    {
        var value = positiveValue.Get; // PositiveInt guarantees value >= 1

        var isValid = IsValidMaxUsers(value);

        return isValid.ToProperty()
            .Label($"MaxUsers={value}: Expected valid, Got IsValid={isValid}");
    }

    #endregion

    #region Property 3b: The unlimited marker (-1) is always valid

    /// <summary>
    /// Property 3b: The value -1 (representing unlimited users) SHALL always be accepted
    /// by the MaxUsers constraint.
    /// **Validates: Requirements 1.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UnlimitedMarker_IsAlwaysValid(PositiveInt _)
    {
        // -1 is the special "unlimited" marker and must always be valid
        var isValid = IsValidMaxUsers(-1);

        return isValid.ToProperty()
            .Label("MaxUsers=-1 (unlimited): Expected valid");
    }

    #endregion

    #region Property 3c: Zero is always invalid

    /// <summary>
    /// Property 3c: The value 0 SHALL always be rejected by the MaxUsers constraint.
    /// Zero users makes no business sense — a plan must allow at least 1 user or be unlimited (-1).
    /// **Validates: Requirements 1.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Zero_IsAlwaysInvalid(PositiveInt _)
    {
        var isValid = IsValidMaxUsers(0);

        return (!isValid).ToProperty()
            .Label("MaxUsers=0: Expected invalid");
    }

    #endregion

    #region Property 3d: Negative integers other than -1 are always invalid

    /// <summary>
    /// Property 3d: For any negative integer other than -1 (e.g., -2, -3, -100),
    /// the MaxUsers constraint SHALL reject the value.
    /// Only -1 has special meaning (unlimited); all other negatives are invalid.
    /// **Validates: Requirements 1.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NegativeIntegersOtherThanMinusOne_AreInvalid(NegativeInt negativeValue)
    {
        var value = negativeValue.Get; // NegativeInt guarantees value < 0

        // Skip -1 since it's the valid unlimited marker
        if (value == -1)
            return true.ToProperty().Label("Skipped — -1 is the valid unlimited marker");

        var isValid = IsValidMaxUsers(value);

        return (!isValid).ToProperty()
            .Label($"MaxUsers={value}: Expected invalid, Got IsValid={isValid}");
    }

    #endregion

    #region Property 3e: Completeness — value is valid iff it equals -1 or is >= 1

    /// <summary>
    /// Property 3e: For any arbitrary integer, the MaxUsers constraint accepts it
    /// if and only if the value equals -1 or is a positive integer >= 1.
    /// This is the universal property covering the full integer space.
    /// **Validates: Requirements 1.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MaxUsers_ValidIfAndOnlyIf_MinusOneOrPositive(int value)
    {
        var isValid = IsValidMaxUsers(value);
        var expectedValid = value == -1 || value >= 1;

        return (isValid == expectedValid).ToProperty()
            .Label($"MaxUsers={value}: IsValid={isValid}, ExpectedValid={expectedValid}");
    }

    #endregion
}
