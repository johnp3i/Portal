using FsCheck;
using FsCheck.Xunit;
using System.Text.RegularExpressions;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: subscription-plans, Property 1: Slug format validation

/// <summary>
/// Property-based tests for Plan slug format validation.
/// Validates that the slug validation logic correctly accepts strings matching
/// the pattern ^[a-z0-9]+(-[a-z0-9]+)*$ and rejects all others.
/// **Validates: Requirements 1.3**
/// </summary>
public class PlanSlugValidationPropertyTests
{
    #region Test Infrastructure

    private static readonly Regex SlugPattern = new(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

    /// <summary>
    /// Validates whether a slug matches the required format:
    /// lowercase alphanumeric segments separated by single hyphens.
    /// </summary>
    private static bool IsValidSlug(string slug)
    {
        return !string.IsNullOrEmpty(slug) && SlugPattern.IsMatch(slug);
    }

    /// <summary>
    /// Generator for valid slugs: one or more lowercase alphanumeric segments separated by single hyphens.
    /// Examples: "abc", "hello-world", "plan1-tier2-v3"
    /// </summary>
    private static Arbitrary<string> ValidSlugArbitrary()
    {
        var segmentGen = Gen.Choose(1, 8)
            .SelectMany(len =>
                Gen.ArrayOf(len, Gen.Elements(
                    "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray()))
                .Select(chars => new string(chars)));

        var slugGen = Gen.Choose(1, 4)
            .SelectMany(segmentCount =>
                Gen.ArrayOf(segmentCount, segmentGen)
                .Select(segments => string.Join("-", segments)));

        return Arb.From(slugGen);
    }

    /// <summary>
    /// Generator for invalid slugs covering multiple failure modes:
    /// uppercase letters, spaces, special characters, leading/trailing hyphens,
    /// consecutive hyphens, and empty strings.
    /// </summary>
    private static Arbitrary<string> InvalidSlugArbitrary()
    {
        var generators = new[]
        {
            // Uppercase letters
            Gen.Elements("Hello", "PLAN", "Business-Plan", "planA", "aBc"),
            // Spaces
            Gen.Elements("hello world", " plan", "plan ", "my plan"),
            // Special characters
            Gen.Elements("plan!", "plan@tier", "plan#1", "plan$", "plan.v2", "plan_v2"),
            // Leading hyphen
            Gen.Elements("-plan", "-hello-world", "-a"),
            // Trailing hyphen
            Gen.Elements("plan-", "hello-world-", "a-"),
            // Consecutive hyphens
            Gen.Elements("plan--tier", "hello---world", "a--b--c"),
            // Empty or whitespace
            Gen.Elements("", " ", "  "),
            // Only hyphens
            Gen.Elements("-", "--", "---"),
            // Mixed invalid patterns
            Gen.Elements("Plan-1", "plan-Tier", "123-ABC", "hello world-plan")
        };

        var invalidSlugGen = Gen.OneOf(generators);
        return Arb.From(invalidSlugGen);
    }

    #endregion

    #region Property 1: Slug format validation

    /// <summary>
    /// Property 1a: All valid slugs (lowercase alphanumeric segments separated by single hyphens)
    /// are accepted by the validation logic.
    /// **Validates: Requirements 1.3**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(PlanSlugValidationPropertyTests) })]
    public Property ValidSlugs_AreAccepted(ValidSlug slug)
    {
        return IsValidSlug(slug.Value).ToProperty()
            .Label($"Expected valid slug '{slug.Value}' to be accepted");
    }

    /// <summary>
    /// Property 1b: All invalid slugs (uppercase, spaces, special chars, leading/trailing hyphens,
    /// consecutive hyphens) are rejected by the validation logic.
    /// **Validates: Requirements 1.3**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(PlanSlugValidationPropertyTests) })]
    public Property InvalidSlugs_AreRejected(InvalidSlug slug)
    {
        return (!IsValidSlug(slug.Value)).ToProperty()
            .Label($"Expected invalid slug '{slug.Value}' to be rejected");
    }

    #endregion

    #region Wrapper Types and Arbitraries

    /// <summary>
    /// Wrapper type for valid slugs to enable FsCheck arbitrary registration.
    /// </summary>
    public class ValidSlug
    {
        public string Value { get; }
        public ValidSlug(string value) => Value = value;
        public override string ToString() => Value;
    }

    /// <summary>
    /// Wrapper type for invalid slugs to enable FsCheck arbitrary registration.
    /// </summary>
    public class InvalidSlug
    {
        public string Value { get; }
        public InvalidSlug(string value) => Value = value;
        public override string ToString() => Value;
    }

    public static Arbitrary<ValidSlug> ValidSlugArb()
    {
        var segmentGen = Gen.Choose(1, 8)
            .SelectMany(len =>
                Gen.ArrayOf(len, Gen.Elements(
                    "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray()))
                .Select(chars => new string(chars)));

        var slugGen = Gen.Choose(1, 4)
            .SelectMany(segmentCount =>
                Gen.ArrayOf(segmentCount, segmentGen)
                .Select(segments => string.Join("-", segments)));

        return Arb.From(slugGen.Select(s => new ValidSlug(s)));
    }

    public static Arbitrary<InvalidSlug> InvalidSlugArb()
    {
        var generators = new[]
        {
            Gen.Elements("Hello", "PLAN", "Business-Plan", "planA", "aBc"),
            Gen.Elements("hello world", " plan", "plan ", "my plan"),
            Gen.Elements("plan!", "plan@tier", "plan#1", "plan$", "plan.v2", "plan_v2"),
            Gen.Elements("-plan", "-hello-world", "-a"),
            Gen.Elements("plan-", "hello-world-", "a-"),
            Gen.Elements("plan--tier", "hello---world", "a--b--c"),
            Gen.Elements("", " ", "  "),
            Gen.Elements("-", "--", "---"),
            Gen.Elements("Plan-1", "plan-Tier", "123-ABC", "hello world-plan")
        };

        var invalidSlugGen = Gen.OneOf(generators);
        return Arb.From(invalidSlugGen.Select(s => new InvalidSlug(s)));
    }

    #endregion
}
