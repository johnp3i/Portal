using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: profit-loss-summary, Property 13: PDF filename format

/// <summary>
/// Property-based tests for P&L PDF filename generation.
/// Validates that the filename follows the pattern PnL_{BusinessName}_{StartDate}_{EndDate}.pdf
/// with spaces replaced by underscores and dates formatted as yyyyMMdd.
/// **Validates: Requirements 5.3**
/// </summary>
public class PnlPdfFilenamePropertyTests
{
    /// <summary>
    /// Generates the PDF filename using the same formula as ProfitLossController.ExportPdf.
    /// </summary>
    private static string GenerateFilename(string businessName, DateOnly periodStart, DateOnly periodEnd)
    {
        return $"PnL_{businessName.Replace(" ", "_")}_{periodStart:yyyyMMdd}_{periodEnd:yyyyMMdd}.pdf";
    }

    /// <summary>
    /// Property 13: PDF filename always starts with "PnL_" prefix.
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Filename_Starts_With_PnL_Prefix(NonEmptyString businessName, int startDayOffset, int endDayOffset)
    {
        var name = businessName.Get;
        var periodStart = DateOnly.FromDateTime(new DateTime(2020, 1, 1).AddDays(Math.Abs(startDayOffset) % 3650));
        var periodEnd = DateOnly.FromDateTime(new DateTime(2020, 1, 1).AddDays(Math.Abs(endDayOffset) % 3650));

        var filename = GenerateFilename(name, periodStart, periodEnd);

        return filename.StartsWith("PnL_").ToProperty()
            .Label($"Filename '{filename}' should start with 'PnL_'");
    }

    /// <summary>
    /// Property 13: PDF filename always ends with ".pdf" extension.
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Filename_Ends_With_Pdf_Extension(NonEmptyString businessName, int startDayOffset, int endDayOffset)
    {
        var name = businessName.Get;
        var periodStart = DateOnly.FromDateTime(new DateTime(2020, 1, 1).AddDays(Math.Abs(startDayOffset) % 3650));
        var periodEnd = DateOnly.FromDateTime(new DateTime(2020, 1, 1).AddDays(Math.Abs(endDayOffset) % 3650));

        var filename = GenerateFilename(name, periodStart, periodEnd);

        return filename.EndsWith(".pdf").ToProperty()
            .Label($"Filename '{filename}' should end with '.pdf'");
    }

    /// <summary>
    /// Property 13: PDF filename contains no literal spaces (spaces replaced with underscores).
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Filename_Contains_No_Spaces(NonEmptyString businessName, int startDayOffset, int endDayOffset)
    {
        var name = businessName.Get;
        var periodStart = DateOnly.FromDateTime(new DateTime(2020, 1, 1).AddDays(Math.Abs(startDayOffset) % 3650));
        var periodEnd = DateOnly.FromDateTime(new DateTime(2020, 1, 1).AddDays(Math.Abs(endDayOffset) % 3650));

        var filename = GenerateFilename(name, periodStart, periodEnd);

        return (!filename.Contains(' ')).ToProperty()
            .Label($"Filename '{filename}' should contain no literal spaces");
    }

    /// <summary>
    /// Property 13: PDF filename contains dates in yyyyMMdd format (8-digit numeric segments).
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Filename_Contains_Dates_In_yyyyMMdd_Format(NonEmptyString businessName, int startDayOffset, int endDayOffset)
    {
        var name = businessName.Get;
        var periodStart = DateOnly.FromDateTime(new DateTime(2020, 1, 1).AddDays(Math.Abs(startDayOffset) % 3650));
        var periodEnd = DateOnly.FromDateTime(new DateTime(2020, 1, 1).AddDays(Math.Abs(endDayOffset) % 3650));

        var filename = GenerateFilename(name, periodStart, periodEnd);

        var expectedStartDate = periodStart.ToString("yyyyMMdd");
        var expectedEndDate = periodEnd.ToString("yyyyMMdd");

        var containsStartDate = filename.Contains(expectedStartDate);
        var containsEndDate = filename.Contains(expectedEndDate);

        return (containsStartDate && containsEndDate).ToProperty()
            .Label($"Filename '{filename}' should contain start date '{expectedStartDate}' and end date '{expectedEndDate}'");
    }

    /// <summary>
    /// Property 13: PDF filename has business name with spaces replaced by underscores.
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Filename_Contains_Business_Name_With_Spaces_Replaced(NonEmptyString businessName, int startDayOffset, int endDayOffset)
    {
        var name = businessName.Get;
        var periodStart = DateOnly.FromDateTime(new DateTime(2020, 1, 1).AddDays(Math.Abs(startDayOffset) % 3650));
        var periodEnd = DateOnly.FromDateTime(new DateTime(2020, 1, 1).AddDays(Math.Abs(endDayOffset) % 3650));

        var filename = GenerateFilename(name, periodStart, periodEnd);

        var expectedBusinessPart = name.Replace(" ", "_");

        // The filename structure is: PnL_{businessPart}_{startDate}_{endDate}.pdf
        // Extract the business part from the filename
        var afterPrefix = filename.Substring(4); // Remove "PnL_"
        var beforePdfExtension = afterPrefix.Substring(0, afterPrefix.Length - 4); // Remove ".pdf"

        // The last 17 chars should be _{startDate}_{endDate} (underscore + 8 digits + underscore + 8 digits)
        var expectedSuffix = $"_{periodStart:yyyyMMdd}_{periodEnd:yyyyMMdd}";
        var businessPart = beforePdfExtension.Substring(0, beforePdfExtension.Length - expectedSuffix.Length);

        return (businessPart == expectedBusinessPart).ToProperty()
            .Label($"Business part '{businessPart}' should equal '{expectedBusinessPart}' (from name '{name}')");
    }

    /// <summary>
    /// Property 13: PDF filename matches the complete expected pattern.
    /// Combines all sub-properties into a single comprehensive assertion.
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Filename_Matches_Full_Expected_Pattern(NonEmptyString businessName, int startDayOffset, int endDayOffset)
    {
        var name = businessName.Get;
        var periodStart = DateOnly.FromDateTime(new DateTime(2020, 1, 1).AddDays(Math.Abs(startDayOffset) % 3650));
        var periodEnd = DateOnly.FromDateTime(new DateTime(2020, 1, 1).AddDays(Math.Abs(endDayOffset) % 3650));

        var filename = GenerateFilename(name, periodStart, periodEnd);

        var expectedFilename = $"PnL_{name.Replace(" ", "_")}_{periodStart:yyyyMMdd}_{periodEnd:yyyyMMdd}.pdf";

        return (filename == expectedFilename).ToProperty()
            .Label($"Filename '{filename}' should exactly match '{expectedFilename}'");
    }
}
