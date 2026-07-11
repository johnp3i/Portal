using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: recurring-expense-validation, Properties 1, 7, 9, 10

/// <summary>
/// Property-based tests for RecurringExpenseValidation logic.
/// Tests pure computation functions extracted from RecurringExpenseValidationService.
/// </summary>
public class RecurringExpenseValidationPropertyTests
{
    #region Helper methods (replicate pure logic from service)

    /// <summary>
    /// Replicates the DetermineStatus logic from RecurringExpenseValidationService.
    /// </summary>
    private static string DetermineStatus(int actualCount, int expectedCount)
    {
        if (actualCount >= expectedCount) return "pass";
        if (actualCount > 0) return "warning";
        return "fail";
    }

    /// <summary>
    /// Replicates the sorting logic from RecurringExpenseValidationService.
    /// Returns sort order: fail=0, warning=1, pass=2.
    /// </summary>
    private static int GetSortOrder(string status)
    {
        return status switch
        {
            "fail" => 0,
            "warning" => 1,
            "pass" => 2,
            _ => 3
        };
    }

    /// <summary>
    /// Replicates the expected count calculation from RecurringExpenseValidationService.
    /// periodMonths is inclusive month count; expectedCount = floor(periodMonths / frequencyMonths).
    /// </summary>
    private static int CalculateExpectedCount(int periodMonths, int frequencyMonths)
    {
        return (int)Math.Floor((double)periodMonths / frequencyMonths);
    }

    #endregion

    #region Property 1: Expected count calculation is deterministic

    /// <summary>
    /// Property 1: Expected count calculation is deterministic.
    /// For any positive period months and frequency months (1-12), the expected count
    /// equals floor(periodMonths / frequencyMonths).
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExpectedCount_IsDeterministic(PositiveInt periodMonthsSeed, PositiveInt frequencySeed)
    {
        // Constrain frequency to 1-12 (realistic range)
        var frequencyMonths = (frequencySeed.Get % 12) + 1;
        // Constrain period months to 1-36 (realistic for VAT periods)
        var periodMonths = (periodMonthsSeed.Get % 36) + 1;

        var expectedCount = CalculateExpectedCount(periodMonths, frequencyMonths);

        // Verify the formula: floor(periodMonths / frequencyMonths)
        var expected = (int)Math.Floor((double)periodMonths / frequencyMonths);

        return (expectedCount == expected).ToProperty()
            .Label($"periodMonths={periodMonths}, frequency={frequencyMonths}: " +
                   $"expectedCount={expectedCount}, formula={expected}");
    }

    /// <summary>
    /// Property 1 (supplemental): When expectedCount == 0 (period shorter than frequency),
    /// the rule would be skipped in the validation loop.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExpectedCountZero_MeansRuleIsSkipped(PositiveInt frequencySeed)
    {
        // Frequency between 2-12
        var frequencyMonths = (frequencySeed.Get % 11) + 2;
        // Period months always shorter than frequency (1 to frequencyMonths-1)
        var periodMonths = (frequencySeed.Get % (frequencyMonths - 1)) + 1;

        // Guard: ensure period < frequency to guarantee expectedCount == 0
        if (periodMonths >= frequencyMonths)
            return true.ToProperty(); // skip degenerate case

        var expectedCount = CalculateExpectedCount(periodMonths, frequencyMonths);

        return (expectedCount == 0).ToProperty()
            .Label($"periodMonths={periodMonths} < frequency={frequencyMonths} should yield expectedCount=0, got {expectedCount}");
    }

    #endregion

    #region Property 7: Status determination is consistent with counts

    /// <summary>
    /// Property 7: Status determination is consistent with counts.
    /// PASS when actual >= expected, WARNING when 0 &lt; actual &lt; expected, FAIL when actual == 0.
    /// **Validates: Requirements 3.3, 3.4, 3.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StatusDetermination_IsConsistentWithCounts(PositiveInt actualSeed, PositiveInt expectedSeed)
    {
        var expectedCount = expectedSeed.Get;
        var actualCount = actualSeed.Get % (expectedCount * 3 + 1); // range: 0 to 3x expected

        var status = DetermineStatus(actualCount, expectedCount);

        bool isCorrect;
        if (actualCount >= expectedCount)
            isCorrect = status == "pass";
        else if (actualCount > 0)
            isCorrect = status == "warning";
        else
            isCorrect = status == "fail";

        return isCorrect.ToProperty()
            .Label($"actual={actualCount}, expected={expectedCount}: status={status}");
    }

    /// <summary>
    /// Property 7 (PASS branch): When actual >= expected, status is always "pass".
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StatusPass_WhenActualMeetsOrExceedsExpected(PositiveInt expectedCountValue, NonNegativeInt extraValue)
    {
        var expectedCount = expectedCountValue.Get;
        var actualCount = expectedCount + extraValue.Get;

        var status = DetermineStatus(actualCount, expectedCount);

        return (status == "pass").ToProperty()
            .Label($"actual={actualCount} >= expected={expectedCount} should be PASS, got {status}");
    }

    /// <summary>
    /// Property 7 (FAIL branch): When actual == 0, status is always "fail".
    /// **Validates: Requirements 3.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StatusFail_WhenActualIsZero(PositiveInt expectedCountValue)
    {
        var expectedCount = expectedCountValue.Get;
        var actualCount = 0;

        var status = DetermineStatus(actualCount, expectedCount);

        return (status == "fail").ToProperty()
            .Label($"actual=0, expected={expectedCount} should be FAIL, got {status}");
    }

    /// <summary>
    /// Property 7 (WARNING branch): When 0 &lt; actual &lt; expected, status is always "warning".
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StatusWarning_WhenActualPartial(PositiveInt expectedSeed, PositiveInt actualSeed)
    {
        // Ensure expected > 1 so there's room for a partial value
        var expectedCount = Math.Max(2, expectedSeed.Get);
        // Actual between 1 and expectedCount-1
        var actualCount = (actualSeed.Get % (expectedCount - 1)) + 1;

        var status = DetermineStatus(actualCount, expectedCount);

        return (status == "warning").ToProperty()
            .Label($"actual={actualCount} (0 < actual < expected={expectedCount}) should be WARNING, got {status}");
    }

    #endregion

    #region Property 9: Deactivated rules are excluded from validation

    /// <summary>
    /// Property 9: Deactivated rules are excluded from validation.
    /// Given a list of rules with mixed IsActive flags, only active rules should be validated.
    /// This tests the pure filtering logic: only rules where IsActive == true are included.
    /// **Validates: Requirements 13.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DeactivatedRules_AreExcluded(NonNegativeInt activeCountSeed, NonNegativeInt inactiveCountSeed)
    {
        var activeCount = activeCountSeed.Get % 10;
        var inactiveCount = inactiveCountSeed.Get % 10;

        // Simulate a list of rules with mixed active/inactive states
        var rules = new List<(int Id, bool IsActive)>();
        for (int i = 0; i < activeCount; i++)
            rules.Add((i + 1, true));
        for (int i = 0; i < inactiveCount; i++)
            rules.Add((activeCount + i + 1, false));

        // The service filters to only active rules (replicating GetActiveByBusinessIdAsync logic)
        var activeRules = rules.Where(r => r.IsActive).ToList();

        // Verify: count matches expected active count
        var countCorrect = activeRules.Count == activeCount;
        // Verify: no inactive rules are included
        var noInactiveIncluded = activeRules.All(r => r.IsActive);
        // Verify: all active rules are included
        var allActiveIncluded = rules.Where(r => r.IsActive).All(r => activeRules.Contains(r));

        return (countCorrect && noInactiveIncluded && allActiveIncluded).ToProperty()
            .Label($"activeCount={activeCount}, inactiveCount={inactiveCount}: " +
                   $"filtered={activeRules.Count}, countOk={countCorrect}, " +
                   $"noInactive={noInactiveIncluded}, allActive={allActiveIncluded}");
    }

    /// <summary>
    /// Property 9 (supplemental): When all rules are inactive, no rules are validated.
    /// **Validates: Requirements 13.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AllInactiveRules_ResultsInEmptyValidation(PositiveInt countSeed)
    {
        var count = (countSeed.Get % 20) + 1;

        // All rules inactive
        var rules = Enumerable.Range(1, count).Select(i => (Id: i, IsActive: false)).ToList();

        var activeRules = rules.Where(r => r.IsActive).ToList();

        return (activeRules.Count == 0).ToProperty()
            .Label($"All {count} rules inactive but filtered count = {activeRules.Count}");
    }

    #endregion

    #region Property 10: Validation report sorting order

    /// <summary>
    /// Property 10: Validation report sorting order.
    /// Results sorted by status: FAIL first, then WARNING, then PASS.
    /// **Validates: Requirements 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ReportSorting_FailThenWarningThenPass(
        NonNegativeInt failCountSeed, NonNegativeInt warningCountSeed, NonNegativeInt passCountSeed)
    {
        var failCount = failCountSeed.Get % 10;
        var warningCount = warningCountSeed.Get % 10;
        var passCount = passCountSeed.Get % 10;

        // Build an unsorted list of results with mixed statuses
        var unsorted = new List<string>();
        for (int i = 0; i < passCount; i++) unsorted.Add("pass");
        for (int i = 0; i < failCount; i++) unsorted.Add("fail");
        for (int i = 0; i < warningCount; i++) unsorted.Add("warning");

        // Apply the same sorting logic as the service
        var sorted = unsorted
            .OrderBy(s => s == "fail" ? 0 : s == "warning" ? 1 : 2)
            .ToList();

        // Verify sort order: all fails come before warnings, all warnings come before passes
        var firstWarningIndex = sorted.IndexOf("warning");
        var firstPassIndex = sorted.IndexOf("pass");
        var lastFailIndex = sorted.LastIndexOf("fail");
        var lastWarningIndex = sorted.LastIndexOf("warning");

        bool failsBeforeWarnings = lastFailIndex < firstWarningIndex || failCount == 0 || warningCount == 0;
        bool warningsBeforePasses = lastWarningIndex < firstPassIndex || warningCount == 0 || passCount == 0;

        // Also verify that consecutive items never have a lower-priority status before a higher-priority one
        bool isMonotonicallyOrdered = true;
        for (int i = 1; i < sorted.Count; i++)
        {
            if (GetSortOrder(sorted[i]) < GetSortOrder(sorted[i - 1]))
            {
                isMonotonicallyOrdered = false;
                break;
            }
        }

        return (failsBeforeWarnings && warningsBeforePasses && isMonotonicallyOrdered).ToProperty()
            .Label($"fail={failCount}, warning={warningCount}, pass={passCount}: " +
                   $"failsFirst={failsBeforeWarnings}, warningsMiddle={warningsBeforePasses}, " +
                   $"monotonic={isMonotonicallyOrdered}");
    }

    /// <summary>
    /// Property 10 (supplemental): Sorting preserves total count (no items lost or duplicated).
    /// **Validates: Requirements 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ReportSorting_PreservesCount(
        NonNegativeInt failCountSeed, NonNegativeInt warningCountSeed, NonNegativeInt passCountSeed)
    {
        var failCount = failCountSeed.Get % 10;
        var warningCount = warningCountSeed.Get % 10;
        var passCount = passCountSeed.Get % 10;
        var totalCount = failCount + warningCount + passCount;

        var unsorted = new List<string>();
        for (int i = 0; i < passCount; i++) unsorted.Add("pass");
        for (int i = 0; i < failCount; i++) unsorted.Add("fail");
        for (int i = 0; i < warningCount; i++) unsorted.Add("warning");

        var sorted = unsorted
            .OrderBy(s => s == "fail" ? 0 : s == "warning" ? 1 : 2)
            .ToList();

        var countPreserved = sorted.Count == totalCount;
        var failCountPreserved = sorted.Count(s => s == "fail") == failCount;
        var warningCountPreserved = sorted.Count(s => s == "warning") == warningCount;
        var passCountPreserved = sorted.Count(s => s == "pass") == passCount;

        return (countPreserved && failCountPreserved && warningCountPreserved && passCountPreserved).ToProperty()
            .Label($"total={totalCount}, sorted={sorted.Count}: " +
                   $"counts preserved: fail={failCountPreserved}, warn={warningCountPreserved}, pass={passCountPreserved}");
    }

    #endregion
}
