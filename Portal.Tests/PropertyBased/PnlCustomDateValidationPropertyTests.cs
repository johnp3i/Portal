using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: profit-loss-summary, Property 6: Custom date validation

/// <summary>
/// Property-based tests for PnlService.ValidateCustomRange.
/// Validates that custom date range validation accepts valid ranges (start &lt;= end)
/// and rejects invalid ones (start &gt; end) with an error message.
/// **Validates: Requirements 2.4, 2.5**
/// </summary>
public class PnlCustomDateValidationPropertyTests
{
    private readonly PnlService _service;

    public PnlCustomDateValidationPropertyTests()
    {
        // ValidateCustomRange is a pure method that doesn't use DbContext or ICurrentTenantService
        _service = new PnlService(null!, null!);
    }

    #region Helpers

    /// <summary>
    /// Generates a valid DateOnly from year/month/day seeds.
    /// Year range: 2000-2100, Month: 1-12, Day: valid for that month.
    /// </summary>
    private static DateOnly GenerateDateOnly(int yearSeed, int monthSeed, int daySeed)
    {
        var year = 2000 + (Math.Abs(yearSeed) % 101); // 2000-2100
        var month = (Math.Abs(monthSeed) % 12) + 1;   // 1-12
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var day = (Math.Abs(daySeed) % daysInMonth) + 1; // 1-daysInMonth
        return new DateOnly(year, month, day);
    }

    #endregion

    /// <summary>
    /// For any pair of dates (startDate, endDate), validation passes iff startDate &lt;= endDate.
    /// When startDate &gt; endDate, IsValid is false and ErrorMessage is non-null.
    /// When startDate &lt;= endDate, IsValid is true and ErrorMessage is null.
    /// **Validates: Requirements 2.4, 2.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidationPassesIffStartIsBeforeOrEqualToEnd(
        PositiveInt yearSeed1, PositiveInt monthSeed1, PositiveInt daySeed1,
        PositiveInt yearSeed2, PositiveInt monthSeed2, PositiveInt daySeed2)
    {
        var startDate = GenerateDateOnly(yearSeed1.Get, monthSeed1.Get, daySeed1.Get);
        var endDate = GenerateDateOnly(yearSeed2.Get, monthSeed2.Get, daySeed2.Get);

        var result = _service.ValidateCustomRange(startDate, endDate);

        var expectedValid = startDate <= endDate;

        var isCorrect = result.IsValid == expectedValid
            && (expectedValid
                ? result.ErrorMessage == null
                : !string.IsNullOrEmpty(result.ErrorMessage));

        return isCorrect
            .ToProperty()
            .Label($"Start={startDate}, End={endDate}, ExpectedValid={expectedValid}, " +
                   $"ActualValid={result.IsValid}, ErrorMessage={result.ErrorMessage ?? "(null)"}");
    }

    /// <summary>
    /// When startDate equals endDate, validation always passes (edge case: same day range).
    /// **Validates: Requirements 2.4, 2.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EqualDatesAlwaysPassValidation(
        PositiveInt yearSeed, PositiveInt monthSeed, PositiveInt daySeed)
    {
        var date = GenerateDateOnly(yearSeed.Get, monthSeed.Get, daySeed.Get);

        var result = _service.ValidateCustomRange(date, date);

        return (result.IsValid && result.ErrorMessage == null)
            .ToProperty()
            .Label($"Date={date}, IsValid={result.IsValid}");
    }

    /// <summary>
    /// When startDate is strictly after endDate, validation always fails with a non-empty error message.
    /// **Validates: Requirements 2.4, 2.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StartAfterEndAlwaysFailsValidation(
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
}
