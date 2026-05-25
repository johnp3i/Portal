using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: dashboard-upgrade, Property 11: VAT period selection logic

/// <summary>
/// Property-based tests for VAT period selection logic in GetVatSummaryAsync.
/// Validates that the selected period is the open period (IsSubmitted = 0) with the latest
/// PeriodEndDate; if no open period exists, it falls back to the period with the most recent
/// PeriodEndDate regardless of submission status.
/// Tested as a pure computation over generated VAT submission and period data.
/// **Validates: Requirements 7.1**
/// </summary>
public class DashboardVatPeriodSelectionPropertyTests
{
    private const int TestBusinessId = 1;

    #region Test Infrastructure

    /// <summary>
    /// Represents a VAT submission joined with its period data, mirroring the SQL query result.
    /// </summary>
    private class VatSubmissionWithPeriod
    {
        public int SubmissionId { get; set; }
        public int BusinessId { get; set; }
        public decimal TotalOutputVat { get; set; }
        public decimal TotalInputVat { get; set; }
        public decimal NetVatPayable { get; set; }
        public bool IsSubmitted { get; set; }
        public DateOnly PeriodEndDate { get; set; }
        public string PeriodLabel { get; set; } = null!;
    }

    /// <summary>
    /// Simulates the VAT period selection logic from GetVatSummaryAsync:
    /// ORDER BY CASE WHEN IsSubmitted = 0 THEN 0 ELSE 1 END, PeriodEndDate DESC
    /// Takes the first result (TOP 1).
    /// </summary>
    private static VatSubmissionWithPeriod? SelectVatPeriod(
        List<VatSubmissionWithPeriod> submissions, int businessId)
    {
        return submissions
            .Where(s => s.BusinessId == businessId)
            .OrderBy(s => s.IsSubmitted ? 1 : 0)
            .ThenByDescending(s => s.PeriodEndDate)
            .FirstOrDefault();
    }

    /// <summary>
    /// Generates a DateOnly from a seed value, producing dates within a reasonable range.
    /// </summary>
    private static DateOnly GeneratePeriodEndDate(int seed)
    {
        // Generate dates between 2023-01-01 and 2026-12-31 (4 years range)
        var baseDate = new DateOnly(2023, 1, 1);
        var dayOffset = Math.Abs(seed) % (365 * 4);
        return baseDate.AddDays(dayOffset);
    }

    /// <summary>
    /// Generates a period label from a seed (e.g., "Mar-May 2024").
    /// </summary>
    private static string GeneratePeriodLabel(int seed)
    {
        var date = GeneratePeriodEndDate(seed);
        return $"Period ending {date:MMM yyyy}";
    }

    /// <summary>
    /// Generates a positive decimal amount from a seed.
    /// </summary>
    private static decimal GenerateAmount(int seed)
    {
        var raw = Math.Abs(seed) % 999999 + 1;
        return raw / 100m;
    }

    /// <summary>
    /// Creates a VatSubmissionWithPeriod with controlled parameters.
    /// </summary>
    private static VatSubmissionWithPeriod CreateSubmission(
        int id, int businessId, int dateSeed, bool isSubmitted, int amountSeed)
    {
        var outputVat = GenerateAmount(amountSeed);
        var inputVat = GenerateAmount(amountSeed + 7);
        return new VatSubmissionWithPeriod
        {
            SubmissionId = id,
            BusinessId = businessId,
            TotalOutputVat = outputVat,
            TotalInputVat = inputVat,
            NetVatPayable = outputVat - inputVat,
            IsSubmitted = isSubmitted,
            PeriodEndDate = GeneratePeriodEndDate(dateSeed),
            PeriodLabel = GeneratePeriodLabel(dateSeed)
        };
    }

    #endregion

    #region Property 11: VAT period selection logic

    /// <summary>
    /// Property 11: When open periods exist, the selected period is the open period
    /// (IsSubmitted = 0) with the latest PeriodEndDate.
    /// **Validates: Requirements 7.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property VatPeriodSelection_SelectsOpenPeriodWithLatestEndDate(
        PositiveInt[] dateSeeds, PositiveInt[] amountSeeds, bool[] submittedFlags)
    {
        if (dateSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var count = Math.Min(dateSeeds.Length, 15);
        var submissions = new List<VatSubmissionWithPeriod>();

        // Ensure at least one open period exists
        var hasOpenPeriod = false;
        for (int i = 0; i < count; i++)
        {
            var isSubmitted = submittedFlags.Length > 0 && submittedFlags[i % submittedFlags.Length];
            if (!isSubmitted) hasOpenPeriod = true;

            var amountSeed = amountSeeds.Length > 0 ? amountSeeds[i % amountSeeds.Length].Get : i + 1;
            submissions.Add(CreateSubmission(
                i + 1, TestBusinessId, dateSeeds[i].Get, isSubmitted, amountSeed));
        }

        // If no open period was generated, force the first one to be open
        if (!hasOpenPeriod)
        {
            submissions[0] = CreateSubmission(
                1, TestBusinessId, dateSeeds[0].Get, isSubmitted: false,
                amountSeeds.Length > 0 ? amountSeeds[0].Get : 1);
        }

        var selected = SelectVatPeriod(submissions, TestBusinessId);

        // The expected result: among open periods, the one with latest PeriodEndDate
        var expectedOpenPeriod = submissions
            .Where(s => s.BusinessId == TestBusinessId && !s.IsSubmitted)
            .OrderByDescending(s => s.PeriodEndDate)
            .First();

        var isCorrect = selected != null
            && selected.SubmissionId == expectedOpenPeriod.SubmissionId
            && !selected.IsSubmitted;

        return isCorrect.ToProperty()
            .Label($"Expected open period ID={expectedOpenPeriod.SubmissionId} " +
                   $"(EndDate={expectedOpenPeriod.PeriodEndDate}), " +
                   $"Got ID={selected?.SubmissionId} (EndDate={selected?.PeriodEndDate}, " +
                   $"IsSubmitted={selected?.IsSubmitted})");
    }

    /// <summary>
    /// Property 11: When no open periods exist, the selected period falls back to the
    /// period with the most recent PeriodEndDate regardless of submission status.
    /// **Validates: Requirements 7.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property VatPeriodSelection_FallsBackToMostRecentWhenNoOpenPeriod(
        PositiveInt[] dateSeeds, PositiveInt[] amountSeeds)
    {
        if (dateSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var count = Math.Min(dateSeeds.Length, 15);
        var submissions = new List<VatSubmissionWithPeriod>();

        // All periods are submitted (no open periods)
        for (int i = 0; i < count; i++)
        {
            var amountSeed = amountSeeds.Length > 0 ? amountSeeds[i % amountSeeds.Length].Get : i + 1;
            submissions.Add(CreateSubmission(
                i + 1, TestBusinessId, dateSeeds[i].Get, isSubmitted: true, amountSeed));
        }

        var selected = SelectVatPeriod(submissions, TestBusinessId);

        // Expected: the submitted period with the most recent PeriodEndDate
        var expectedPeriod = submissions
            .Where(s => s.BusinessId == TestBusinessId)
            .OrderByDescending(s => s.PeriodEndDate)
            .First();

        var isCorrect = selected != null
            && selected.SubmissionId == expectedPeriod.SubmissionId;

        return isCorrect.ToProperty()
            .Label($"Expected most recent period ID={expectedPeriod.SubmissionId} " +
                   $"(EndDate={expectedPeriod.PeriodEndDate}), " +
                   $"Got ID={selected?.SubmissionId} (EndDate={selected?.PeriodEndDate})");
    }

    /// <summary>
    /// Property 11: An open period with an earlier PeriodEndDate is preferred over a
    /// submitted period with a later PeriodEndDate.
    /// **Validates: Requirements 7.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property VatPeriodSelection_OpenPeriodPreferredOverSubmittedEvenWithEarlierDate(
        PositiveInt dateSeedOpen, PositiveInt dateSeedSubmitted, PositiveInt amountSeed)
    {
        // Create an open period with an earlier date
        var openSubmission = CreateSubmission(
            1, TestBusinessId, dateSeedOpen.Get % 500, isSubmitted: false, amountSeed.Get);

        // Create a submitted period with a later date (offset ensures later)
        var submittedSubmission = CreateSubmission(
            2, TestBusinessId, dateSeedSubmitted.Get % 500 + 600, isSubmitted: true, amountSeed.Get + 100);

        var submissions = new List<VatSubmissionWithPeriod> { openSubmission, submittedSubmission };

        var selected = SelectVatPeriod(submissions, TestBusinessId);

        // The open period should always be selected over the submitted one
        var isCorrect = selected != null
            && selected.SubmissionId == openSubmission.SubmissionId
            && !selected.IsSubmitted;

        return isCorrect.ToProperty()
            .Label($"Open period (EndDate={openSubmission.PeriodEndDate}) should be preferred over " +
                   $"submitted period (EndDate={submittedSubmission.PeriodEndDate}). " +
                   $"Selected ID={selected?.SubmissionId}, IsSubmitted={selected?.IsSubmitted}");
    }

    /// <summary>
    /// Property 11: When multiple open periods exist, the one with the latest PeriodEndDate wins.
    /// **Validates: Requirements 7.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property VatPeriodSelection_MultipleOpenPeriods_LatestEndDateWins(
        PositiveInt[] dateSeeds, PositiveInt[] amountSeeds)
    {
        if (dateSeeds.Length < 2)
            return true.ToProperty().Label("Need at least 2 periods — trivially true");

        var count = Math.Min(dateSeeds.Length, 10);
        var submissions = new List<VatSubmissionWithPeriod>();

        // All periods are open
        for (int i = 0; i < count; i++)
        {
            var amountSeed = amountSeeds.Length > 0 ? amountSeeds[i % amountSeeds.Length].Get : i + 1;
            submissions.Add(CreateSubmission(
                i + 1, TestBusinessId, dateSeeds[i].Get, isSubmitted: false, amountSeed));
        }

        var selected = SelectVatPeriod(submissions, TestBusinessId);

        // Expected: the open period with the latest PeriodEndDate
        var expectedPeriod = submissions
            .OrderByDescending(s => s.PeriodEndDate)
            .First();

        var isCorrect = selected != null
            && selected.SubmissionId == expectedPeriod.SubmissionId;

        return isCorrect.ToProperty()
            .Label($"Expected latest open period ID={expectedPeriod.SubmissionId} " +
                   $"(EndDate={expectedPeriod.PeriodEndDate}), " +
                   $"Got ID={selected?.SubmissionId} (EndDate={selected?.PeriodEndDate})");
    }

    /// <summary>
    /// Property 11: When no submissions exist for the business, no period is selected.
    /// **Validates: Requirements 7.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property VatPeriodSelection_NoSubmissions_ReturnsNull(
        PositiveInt[] dateSeeds, PositiveInt[] amountSeeds)
    {
        if (dateSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var count = Math.Min(dateSeeds.Length, 10);
        var submissions = new List<VatSubmissionWithPeriod>();
        var otherBusinessId = 99;

        // Create submissions for a different business
        for (int i = 0; i < count; i++)
        {
            var amountSeed = amountSeeds.Length > 0 ? amountSeeds[i % amountSeeds.Length].Get : i + 1;
            submissions.Add(CreateSubmission(
                i + 1, otherBusinessId, dateSeeds[i].Get, isSubmitted: false, amountSeed));
        }

        var selected = SelectVatPeriod(submissions, TestBusinessId);

        return (selected == null).ToProperty()
            .Label($"Expected null for TestBusinessId={TestBusinessId} when all submissions " +
                   $"belong to BusinessId={otherBusinessId}");
    }

    /// <summary>
    /// Property 11: Mixed scenario with open and submitted periods across multiple businesses.
    /// Only periods for the target business are considered, and open periods take priority.
    /// **Validates: Requirements 7.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property VatPeriodSelection_MixedScenario_CorrectPeriodSelected(
        PositiveInt[] dateSeeds, PositiveInt[] amountSeeds, bool[] submittedFlags, bool[] sameBusinessFlags)
    {
        if (dateSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var count = Math.Min(dateSeeds.Length, 20);
        var submissions = new List<VatSubmissionWithPeriod>();

        for (int i = 0; i < count; i++)
        {
            var isSubmitted = submittedFlags.Length > 0 && submittedFlags[i % submittedFlags.Length];
            var isSameBusiness = sameBusinessFlags.Length > 0 && sameBusinessFlags[i % sameBusinessFlags.Length];
            var businessId = isSameBusiness ? TestBusinessId : 99;
            var amountSeed = amountSeeds.Length > 0 ? amountSeeds[i % amountSeeds.Length].Get : i + 1;

            submissions.Add(CreateSubmission(
                i + 1, businessId, dateSeeds[i].Get, isSubmitted, amountSeed));
        }

        var selected = SelectVatPeriod(submissions, TestBusinessId);

        // Compute expected independently
        var businessSubmissions = submissions.Where(s => s.BusinessId == TestBusinessId).ToList();

        if (businessSubmissions.Count == 0)
        {
            return (selected == null).ToProperty()
                .Label("No submissions for business — expected null");
        }

        var openPeriods = businessSubmissions.Where(s => !s.IsSubmitted).ToList();

        VatSubmissionWithPeriod expectedPeriod;
        if (openPeriods.Any())
        {
            // Open period with latest PeriodEndDate
            expectedPeriod = openPeriods.OrderByDescending(s => s.PeriodEndDate).First();
        }
        else
        {
            // Fallback: most recent PeriodEndDate regardless of status
            expectedPeriod = businessSubmissions.OrderByDescending(s => s.PeriodEndDate).First();
        }

        var isCorrect = selected != null
            && selected.SubmissionId == expectedPeriod.SubmissionId;

        return isCorrect.ToProperty()
            .Label($"Expected period ID={expectedPeriod.SubmissionId} " +
                   $"(EndDate={expectedPeriod.PeriodEndDate}, IsSubmitted={expectedPeriod.IsSubmitted}), " +
                   $"Got ID={selected?.SubmissionId} " +
                   $"(EndDate={selected?.PeriodEndDate}, IsSubmitted={selected?.IsSubmitted}), " +
                   $"OpenPeriods={openPeriods.Count}, TotalForBusiness={businessSubmissions.Count}");
    }

    #endregion
}
