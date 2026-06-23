using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: expense-categorisation-insights, Property 4: Custom range validation

/// <summary>
/// Property-based tests for ExpenseInsightsService.ValidateCustomRange.
/// Validates that custom date range validation:
/// - Accepts when startDate &lt;= endDate AND (endDate.DayNumber - startDate.DayNumber) &lt;= 366
/// - Rejects with error when startDate &gt; endDate
/// - Rejects with error when range exceeds 366 days
/// **Validates: Requirements 2.6, 2.7**
/// </summary>
public class ExpenseInsightsCustomRangeValidationPropertyTests
{
    private readonly ExpenseInsightsService _service;

    public ExpenseInsightsCustomRangeValidationPropertyTests()
    {
        // ValidateCustomRange is a pure method that doesn't use DbContext or ICurrentTenantService
        _service = new ExpenseInsightsService(null!, null!);
    }

    #region Helpers

    /// <summary>
    /// Generates a valid DateOnly from year/month/day seeds.
    /// Year range: 2000-2050, Month: 1-12, Day: valid for that month.
    /// </summary>
    private static DateOnly GenerateDateOnly(int yearSeed, int monthSeed, int daySeed)
    {
        var year = 2000 + (Math.Abs(yearSeed) % 51); // 2000-2050
        var month = (Math.Abs(monthSeed) % 12) + 1;   // 1-12
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var day = (Math.Abs(daySeed) % daysInMonth) + 1; // 1-daysInMonth
        return new DateOnly(year, month, day);
    }

    #endregion

    /// <summary>
    /// For any pair of DateOnly values, the validation result matches the spec:
    /// - Accept when startDate &lt;= endDate AND range &lt;= 366 days
    /// - Reject when startDate &gt; endDate
    /// - Reject when range &gt; 366 days
    /// **Validates: Requirements 2.6, 2.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidationMatchesSpecForAnyDatePair(
        PositiveInt yearSeed1, PositiveInt monthSeed1, PositiveInt daySeed1,
        PositiveInt yearSeed2, PositiveInt monthSeed2, PositiveInt daySeed2)
    {
        var startDate = GenerateDateOnly(yearSeed1.Get, monthSeed1.Get, daySeed1.Get);
        var endDate = GenerateDateOnly(yearSeed2.Get, monthSeed2.Get, daySeed2.Get);

        var result = _service.ValidateCustomRange(startDate, endDate);

        var daysDiff = endDate.DayNumber - startDate.DayNumber;
        bool expectedValid = startDate <= endDate && daysDiff <= 366;

        var isCorrect = result.IsValid == expectedValid
            && (expectedValid
                ? result.ErrorMessage == null
                : !string.IsNullOrEmpty(result.ErrorMessage));

        return isCorrect
            .ToProperty()
            .Label($"Start={startDate}, End={endDate}, DaysDiff={daysDiff}, " +
                   $"ExpectedValid={expectedValid}, ActualValid={result.IsValid}, " +
                   $"ErrorMessage={result.ErrorMessage ?? "(null)"}");
    }

    /// <summary>
    /// When startDate &gt; endDate, validation always rejects with an error message.
    /// **Validates: Requirements 2.6, 2.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StartAfterEndAlwaysRejects(
        PositiveInt yearSeed1, PositiveInt monthSeed1, PositiveInt daySeed1,
        PositiveInt offsetDays)
    {
        var endDate = GenerateDateOnly(yearSeed1.Get, monthSeed1.Get, daySeed1.Get);
        // Ensure startDate is strictly after endDate by adding at least 1 day
        var daysToAdd = (offsetDays.Get % 3650) + 1; // 1 to 3650 days ahead
        var startDate = endDate.AddDays(daysToAdd);

        var result = _service.ValidateCustomRange(startDate, endDate);

        return (!result.IsValid && !string.IsNullOrEmpty(result.ErrorMessage))
            .ToProperty()
            .Label($"Start={startDate}, End={endDate}, IsValid={result.IsValid}, " +
                   $"ErrorMessage={result.ErrorMessage ?? "(null)"}");
    }

    /// <summary>
    /// When range exceeds 366 days (but start &lt;= end), validation always rejects with an error message.
    /// **Validates: Requirements 2.6, 2.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RangeExceeding366DaysAlwaysRejects(
        PositiveInt yearSeed, PositiveInt monthSeed, PositiveInt daySeed,
        PositiveInt extraDays)
    {
        var startDate = GenerateDateOnly(yearSeed.Get, monthSeed.Get, daySeed.Get);
        // Add more than 366 days to ensure range exceeds limit
        var daysToAdd = 367 + (extraDays.Get % 3000); // 367 to 3366 days
        var endDate = startDate.AddDays(daysToAdd);

        var result = _service.ValidateCustomRange(startDate, endDate);

        return (!result.IsValid && !string.IsNullOrEmpty(result.ErrorMessage))
            .ToProperty()
            .Label($"Start={startDate}, End={endDate}, DaysDiff={endDate.DayNumber - startDate.DayNumber}, " +
                   $"IsValid={result.IsValid}, ErrorMessage={result.ErrorMessage ?? "(null)"}");
    }

    /// <summary>
    /// When startDate equals endDate (range = 0 days), validation always accepts.
    /// **Validates: Requirements 2.6, 2.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EqualDatesAlwaysAccepted(
        PositiveInt yearSeed, PositiveInt monthSeed, PositiveInt daySeed)
    {
        var date = GenerateDateOnly(yearSeed.Get, monthSeed.Get, daySeed.Get);

        var result = _service.ValidateCustomRange(date, date);

        return (result.IsValid && result.ErrorMessage == null)
            .ToProperty()
            .Label($"Date={date}, IsValid={result.IsValid}");
    }

    /// <summary>
    /// When range is exactly 366 days, validation accepts (boundary case).
    /// **Validates: Requirements 2.6, 2.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RangeExactly366DaysAccepted(
        PositiveInt yearSeed, PositiveInt monthSeed, PositiveInt daySeed)
    {
        var startDate = GenerateDateOnly(yearSeed.Get, monthSeed.Get, daySeed.Get);
        var endDate = startDate.AddDays(366);

        var result = _service.ValidateCustomRange(startDate, endDate);

        return (result.IsValid && result.ErrorMessage == null)
            .ToProperty()
            .Label($"Start={startDate}, End={endDate}, DaysDiff=366, IsValid={result.IsValid}");
    }
}
