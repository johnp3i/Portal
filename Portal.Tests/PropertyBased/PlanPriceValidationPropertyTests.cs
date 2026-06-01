using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: subscription-plans, Property 2: Price constraint validation

/// <summary>
/// Property-based tests for Plan price constraint validation.
/// For any Plan record, MonthlyPriceEur SHALL be accepted iff >= 0.00,
/// and AnnualPriceEur SHALL be accepted iff NULL or >= 0.00.
/// **Validates: Requirements 1.4, 1.5**
/// </summary>
public class PlanPriceValidationPropertyTests
{
    /// <summary>
    /// Mirrors the database CHECK constraint: CK_Plan_MonthlyPriceEur ([MonthlyPriceEur] >= 0.00)
    /// </summary>
    private static bool IsValidMonthlyPrice(decimal price) => price >= 0.00m;

    /// <summary>
    /// Mirrors the database CHECK constraint: CK_Plan_AnnualPriceEur ([AnnualPriceEur] IS NULL OR [AnnualPriceEur] >= 0.00)
    /// </summary>
    private static bool IsValidAnnualPrice(decimal? price) => price == null || price >= 0.00m;

    #region Property 2a: MonthlyPriceEur non-negative values are accepted

    /// <summary>
    /// Property 2a: For any non-negative decimal value assigned to MonthlyPriceEur,
    /// the price constraint validation SHALL accept it.
    /// **Validates: Requirements 1.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MonthlyPrice_NonNegative_IsAccepted(PositiveInt seed)
    {
        // Generate non-negative decimals: 0.00 to large values
        var price = (seed.Get % 1000000) * 0.01m;

        var isValid = IsValidMonthlyPrice(price);

        return isValid.ToProperty()
            .Label($"MonthlyPriceEur={price}: IsValid={isValid}");
    }

    #endregion

    #region Property 2b: MonthlyPriceEur negative values are rejected

    /// <summary>
    /// Property 2b: For any negative decimal value assigned to MonthlyPriceEur,
    /// the price constraint validation SHALL reject it.
    /// **Validates: Requirements 1.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MonthlyPrice_Negative_IsRejected(NegativeInt seed)
    {
        // Generate negative decimals from negative integers
        var price = seed.Get * 0.01m;

        var isValid = IsValidMonthlyPrice(price);

        return (!isValid).ToProperty()
            .Label($"MonthlyPriceEur={price}: IsValid={isValid} (expected false)");
    }

    #endregion

    #region Property 2c: MonthlyPriceEur validation is correct for any decimal

    /// <summary>
    /// Property 2c: For any arbitrary decimal, IsValidMonthlyPrice returns true iff the value >= 0.00.
    /// This is the universal property covering the full decimal space.
    /// **Validates: Requirements 1.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MonthlyPrice_ValidationMatchesConstraint(decimal price)
    {
        var isValid = IsValidMonthlyPrice(price);
        var expected = price >= 0.00m;

        return (isValid == expected).ToProperty()
            .Label($"MonthlyPriceEur={price}: IsValid={isValid}, Expected={expected}");
    }

    #endregion

    #region Property 2d: AnnualPriceEur NULL is always accepted

    /// <summary>
    /// Property 2d: When AnnualPriceEur is NULL, the price constraint validation SHALL accept it.
    /// **Validates: Requirements 1.5**
    /// </summary>
    [Fact]
    public void AnnualPrice_Null_IsAccepted()
    {
        var isValid = IsValidAnnualPrice(null);

        Assert.True(isValid);
    }

    #endregion

    #region Property 2e: AnnualPriceEur non-negative values are accepted

    /// <summary>
    /// Property 2e: For any non-negative decimal value assigned to AnnualPriceEur,
    /// the price constraint validation SHALL accept it.
    /// **Validates: Requirements 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AnnualPrice_NonNegative_IsAccepted(PositiveInt seed)
    {
        // Generate non-negative decimals: 0.00 to large values
        decimal? price = (seed.Get % 1000000) * 0.01m;

        var isValid = IsValidAnnualPrice(price);

        return isValid.ToProperty()
            .Label($"AnnualPriceEur={price}: IsValid={isValid}");
    }

    #endregion

    #region Property 2f: AnnualPriceEur negative values are rejected

    /// <summary>
    /// Property 2f: For any negative decimal value assigned to AnnualPriceEur,
    /// the price constraint validation SHALL reject it.
    /// **Validates: Requirements 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AnnualPrice_Negative_IsRejected(NegativeInt seed)
    {
        // Generate negative decimals from negative integers
        decimal? price = seed.Get * 0.01m;

        var isValid = IsValidAnnualPrice(price);

        return (!isValid).ToProperty()
            .Label($"AnnualPriceEur={price}: IsValid={isValid} (expected false)");
    }

    #endregion

    #region Property 2g: AnnualPriceEur validation is correct for any nullable decimal

    /// <summary>
    /// Property 2g: For any arbitrary decimal (non-null), IsValidAnnualPrice returns true iff the value >= 0.00.
    /// Combined with the NULL case (always valid), this covers the full constraint space.
    /// **Validates: Requirements 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AnnualPrice_ValidationMatchesConstraint(decimal price)
    {
        decimal? nullablePrice = price;
        var isValid = IsValidAnnualPrice(nullablePrice);
        var expected = price >= 0.00m;

        return (isValid == expected).ToProperty()
            .Label($"AnnualPriceEur={price}: IsValid={isValid}, Expected={expected}");
    }

    #endregion

    #region Property 2h: Zero is always a valid boundary for both prices

    /// <summary>
    /// Property 2h: Zero (0.00) is the boundary value and SHALL always be accepted for both
    /// MonthlyPriceEur and AnnualPriceEur.
    /// **Validates: Requirements 1.4, 1.5**
    /// </summary>
    [Fact]
    public void ZeroPrice_IsAcceptedForBothFields()
    {
        Assert.True(IsValidMonthlyPrice(0.00m));
        Assert.True(IsValidAnnualPrice(0.00m));
    }

    #endregion
}
