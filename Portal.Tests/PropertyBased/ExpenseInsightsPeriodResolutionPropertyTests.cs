using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.ExpenseInsights;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: expense-categorisation-insights, Property 3: Period resolution correctness

/// <summary>
/// Property-based tests for ExpenseInsightsService.ResolvePeriod.
/// Validates that period resolution produces correct date boundaries for all period types.
/// **Validates: Requirements 2.2, 2.3, 2.4, 2.5**
/// </summary>
public class ExpenseInsightsPeriodResolutionPropertyTests
{
    private readonly ExpenseInsightsService _service;

    public ExpenseInsightsPeriodResolutionPropertyTests()
    {
        // ResolvePeriod is a pure method that doesn't use DbContext or ICurrentTenantService
        _service = new ExpenseInsightsService(null!, null!);
    }

    #region Helpers

    /// <summary>
    /// Generates a valid DateTime from year/month/day seeds within range 2000-2050.
    /// </summary>
    private static DateTime GenerateDate(int yearSeed, int monthSeed, int daySeed)
    {
        var year = 2000 + (Math.Abs(yearSeed) % 51); // 2000-2050
        var month = (Math.Abs(monthSeed) % 12) + 1;  // 1-12
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var day = (Math.Abs(daySeed) % daysInMonth) + 1; // 1-daysInMonth
        return new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Utc);
    }

    #endregion

    #region CurrentMonth

    /// <summary>
    /// For any reference date, ResolvePeriod(CurrentMonth) returns:
    /// StartDate == 1st of that month, EndDate == reference date as DateOnly.
    /// **Validates: Requirements 2.2, 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CurrentMonth_StartDateIsFirstOfMonth_EndDateIsReferenceDate(
        PositiveInt yearSeed, PositiveInt monthSeed, PositiveInt daySeed)
    {
        var referenceDate = GenerateDate(yearSeed.Get, monthSeed.Get, daySeed.Get);

        var result = _service.ResolvePeriod(PnlPeriodType.CurrentMonth, referenceDate);

        var expectedStart = new DateOnly(referenceDate.Year, referenceDate.Month, 1);
        var expectedEnd = DateOnly.FromDateTime(referenceDate);

        return (result.StartDate == expectedStart && result.EndDate == expectedEnd)
            .ToProperty()
            .Label($"Date={referenceDate:yyyy-MM-dd}, Expected={expectedStart}..{expectedEnd}, " +
                   $"Actual={result.StartDate}..{result.EndDate}");
    }

    #endregion

    #region PreviousMonth

    /// <summary>
    /// For any reference date, ResolvePeriod(PreviousMonth) returns:
    /// StartDate == 1st of prior month, EndDate == last day of prior month.
    /// **Validates: Requirements 2.2, 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PreviousMonth_StartDateIsFirstOfPriorMonth_EndDateIsLastDayOfPriorMonth(
        PositiveInt yearSeed, PositiveInt monthSeed, PositiveInt daySeed)
    {
        var referenceDate = GenerateDate(yearSeed.Get, monthSeed.Get, daySeed.Get);

        var result = _service.ResolvePeriod(PnlPeriodType.PreviousMonth, referenceDate);

        var previousMonth = referenceDate.AddMonths(-1);
        var expectedStart = new DateOnly(previousMonth.Year, previousMonth.Month, 1);
        var expectedEnd = new DateOnly(previousMonth.Year, previousMonth.Month,
            DateTime.DaysInMonth(previousMonth.Year, previousMonth.Month));

        return (result.StartDate == expectedStart && result.EndDate == expectedEnd)
            .ToProperty()
            .Label($"Date={referenceDate:yyyy-MM-dd}, Expected={expectedStart}..{expectedEnd}, " +
                   $"Actual={result.StartDate}..{result.EndDate}");
    }

    /// <summary>
    /// When reference date is in January, PreviousMonth resolves to December of the prior year.
    /// **Validates: Requirements 2.2, 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PreviousMonth_JanuaryReference_ReturnsDecemberOfPriorYear(
        PositiveInt yearSeed, PositiveInt daySeed)
    {
        var year = 2000 + (Math.Abs(yearSeed.Get) % 51);
        var daysInJan = DateTime.DaysInMonth(year, 1);
        var day = (Math.Abs(daySeed.Get) % daysInJan) + 1;
        var referenceDate = new DateTime(year, 1, day, 12, 0, 0, DateTimeKind.Utc);

        var result = _service.ResolvePeriod(PnlPeriodType.PreviousMonth, referenceDate);

        var expectedStart = new DateOnly(year - 1, 12, 1);
        var expectedEnd = new DateOnly(year - 1, 12, 31);

        return (result.StartDate == expectedStart && result.EndDate == expectedEnd)
            .ToProperty()
            .Label($"Date={referenceDate:yyyy-MM-dd}, Expected={expectedStart}..{expectedEnd}, " +
                   $"Actual={result.StartDate}..{result.EndDate}");
    }

    #endregion

    #region CurrentQuarter

    /// <summary>
    /// For any reference date, ResolvePeriod(CurrentQuarter) returns:
    /// StartDate == 1st of quarter (Jan/Apr/Jul/Oct), EndDate == reference date as DateOnly.
    /// **Validates: Requirements 2.2, 2.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CurrentQuarter_StartDateIsQuarterBoundary_EndDateIsReferenceDate(
        PositiveInt yearSeed, PositiveInt monthSeed, PositiveInt daySeed)
    {
        var referenceDate = GenerateDate(yearSeed.Get, monthSeed.Get, daySeed.Get);

        var result = _service.ResolvePeriod(PnlPeriodType.CurrentQuarter, referenceDate);

        var quarterMonth = ((referenceDate.Month - 1) / 3) * 3 + 1;
        var expectedStart = new DateOnly(referenceDate.Year, quarterMonth, 1);
        var expectedEnd = DateOnly.FromDateTime(referenceDate);

        return (result.StartDate == expectedStart && result.EndDate == expectedEnd)
            .ToProperty()
            .Label($"Date={referenceDate:yyyy-MM-dd}, QuarterStartMonth={quarterMonth}, " +
                   $"Expected={expectedStart}..{expectedEnd}, Actual={result.StartDate}..{result.EndDate}");
    }

    /// <summary>
    /// Quarter start month is always one of Jan(1), Apr(4), Jul(7), or Oct(10).
    /// **Validates: Requirements 2.2, 2.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CurrentQuarter_StartMonthIsAlwaysValidQuarterBoundary(
        PositiveInt yearSeed, PositiveInt monthSeed, PositiveInt daySeed)
    {
        var referenceDate = GenerateDate(yearSeed.Get, monthSeed.Get, daySeed.Get);

        var result = _service.ResolvePeriod(PnlPeriodType.CurrentQuarter, referenceDate);

        var validQuarterMonths = new[] { 1, 4, 7, 10 };
        var startMonth = result.StartDate.Month;

        return validQuarterMonths.Contains(startMonth)
            .ToProperty()
            .Label($"Date={referenceDate:yyyy-MM-dd}, QuarterStartMonth={startMonth}");
    }

    #endregion

    #region CurrentYear

    /// <summary>
    /// For any reference date, ResolvePeriod(CurrentYear) returns:
    /// StartDate == Jan 1 of that year, EndDate == reference date as DateOnly.
    /// **Validates: Requirements 2.2, 2.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CurrentYear_StartDateIsJanFirst_EndDateIsReferenceDate(
        PositiveInt yearSeed, PositiveInt monthSeed, PositiveInt daySeed)
    {
        var referenceDate = GenerateDate(yearSeed.Get, monthSeed.Get, daySeed.Get);

        var result = _service.ResolvePeriod(PnlPeriodType.CurrentYear, referenceDate);

        var expectedStart = new DateOnly(referenceDate.Year, 1, 1);
        var expectedEnd = DateOnly.FromDateTime(referenceDate);

        return (result.StartDate == expectedStart && result.EndDate == expectedEnd)
            .ToProperty()
            .Label($"Date={referenceDate:yyyy-MM-dd}, Expected={expectedStart}..{expectedEnd}, " +
                   $"Actual={result.StartDate}..{result.EndDate}");
    }

    #endregion

    #region Leap Year Scenarios

    /// <summary>
    /// When the reference date is Feb 29 of a leap year, CurrentMonth correctly returns
    /// StartDate == Feb 1 and EndDate == Feb 29.
    /// **Validates: Requirements 2.2, 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CurrentMonth_LeapYearFeb29_ReturnsCorrectRange(PositiveInt seed)
    {
        var leapYears = Enumerable.Range(2000, 51)
            .Where(y => DateTime.IsLeapYear(y))
            .ToList();

        var year = leapYears[Math.Abs(seed.Get) % leapYears.Count];
        var referenceDate = new DateTime(year, 2, 29, 12, 0, 0, DateTimeKind.Utc);

        var result = _service.ResolvePeriod(PnlPeriodType.CurrentMonth, referenceDate);

        var expectedStart = new DateOnly(year, 2, 1);
        var expectedEnd = new DateOnly(year, 2, 29);

        return (result.StartDate == expectedStart && result.EndDate == expectedEnd)
            .ToProperty()
            .Label($"LeapYear={year}, Expected={expectedStart}..{expectedEnd}, " +
                   $"Actual={result.StartDate}..{result.EndDate}");
    }

    /// <summary>
    /// When the reference date is in March of a leap year, PreviousMonth correctly returns
    /// Feb 1 to Feb 29 (not Feb 28).
    /// **Validates: Requirements 2.2, 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PreviousMonth_MarchInLeapYear_ReturnsFebWith29Days(PositiveInt seed)
    {
        var leapYears = Enumerable.Range(2000, 51)
            .Where(y => DateTime.IsLeapYear(y))
            .ToList();

        var year = leapYears[Math.Abs(seed.Get) % leapYears.Count];
        var day = (Math.Abs(seed.Get) % 31) + 1;
        var referenceDate = new DateTime(year, 3, day, 12, 0, 0, DateTimeKind.Utc);

        var result = _service.ResolvePeriod(PnlPeriodType.PreviousMonth, referenceDate);

        var expectedStart = new DateOnly(year, 2, 1);
        var expectedEnd = new DateOnly(year, 2, 29);

        return (result.StartDate == expectedStart && result.EndDate == expectedEnd)
            .ToProperty()
            .Label($"LeapYear={year}, March Day={day}, Expected={expectedStart}..{expectedEnd}, " +
                   $"Actual={result.StartDate}..{result.EndDate}");
    }

    #endregion
}
