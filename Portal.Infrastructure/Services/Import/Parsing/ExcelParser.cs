using ClosedXML.Excel;

namespace Portal.Infrastructure.Services.Import.Parsing;

/// <summary>
/// Excel file parser using ClosedXML. Supports .xlsx format.
/// Reads a specified worksheet (or first by default) and returns rows as string arrays.
/// </summary>
public static class ExcelParser
{
    /// <summary>
    /// Parses an Excel stream into a list of rows, each row being an array of field values.
    /// </summary>
    /// <param name="stream">The input stream containing an .xlsx file.</param>
    /// <param name="sheetName">Optional worksheet name. If null, reads the first worksheet.</param>
    /// <returns>List of string arrays representing each row.</returns>
    public static List<string[]> Parse(Stream stream, string? sheetName = null)
    {
        using var workbook = new XLWorkbook(stream);

        var worksheet = string.IsNullOrWhiteSpace(sheetName)
            ? workbook.Worksheets.First()
            : workbook.Worksheet(sheetName);

        return ReadRows(worksheet);
    }

    /// <summary>
    /// Parses a prospecting workbook tolerantly. The prospects sheet name is variable
    /// (e.g. "20 Prospects", "30 Prospects") because the count is chosen by the user, so we
    /// resolve the sheet by pattern rather than an exact name:
    ///   1. exact preferredName if present,
    ///   2. any sheet named "{number} Prospects" (case-insensitive),
    ///   3. any sheet whose name contains "Prospects",
    ///   4. the first sheet as a last resort.
    /// </summary>
    /// <param name="stream">The input stream containing an .xlsx file.</param>
    /// <param name="preferredName">The preferred sheet name to try first (optional).</param>
    /// <param name="resolvedSheetName">The name of the sheet that was actually read.</param>
    /// <returns>List of string arrays representing each row.</returns>
    public static List<string[]> ParseProspects(Stream stream, string? preferredName, out string resolvedSheetName)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = ResolveProspectsSheet(workbook, preferredName);
        resolvedSheetName = worksheet.Name;
        return ReadRows(worksheet);
    }

    private static IXLWorksheet ResolveProspectsSheet(XLWorkbook workbook, string? preferredName)
    {
        // 1. Exact preferred name.
        if (!string.IsNullOrWhiteSpace(preferredName))
        {
            var exact = workbook.Worksheets
                .FirstOrDefault(w => string.Equals(w.Name, preferredName, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
                return exact;
        }

        // 2. Any "{number} Prospects" sheet (e.g. "20 Prospects").
        var numbered = workbook.Worksheets
            .FirstOrDefault(w => System.Text.RegularExpressions.Regex.IsMatch(
                w.Name, PROSPECTS_SHEET_PATTERN, System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        if (numbered != null)
            return numbered;

        // 3. Any sheet whose name contains "Prospect".
        var contains = workbook.Worksheets
            .FirstOrDefault(w => w.Name.IndexOf("prospect", StringComparison.OrdinalIgnoreCase) >= 0);
        if (contains != null)
            return contains;

        // 4. First sheet as a last resort.
        return workbook.Worksheets.First();
    }

    private const string PROSPECTS_SHEET_PATTERN = @"^\s*\d+\s+prospects\s*$";

    private static List<string[]> ReadRows(IXLWorksheet worksheet)
    {
        var rows = new List<string[]>();
        var range = worksheet.RangeUsed();

        if (range == null)
            return rows;

        var rowCount = range.RowCount();
        var colCount = range.ColumnCount();

        for (var r = 1; r <= rowCount; r++)
        {
            var fields = new string[colCount];
            for (var c = 1; c <= colCount; c++)
            {
                var cell = worksheet.Cell(r, c);
                fields[c - 1] = GetCellValue(cell);
            }
            rows.Add(fields);
        }

        return rows;
    }

    private static string GetCellValue(IXLCell cell)
    {
        if (cell.IsEmpty())
            return string.Empty;

        // For dates, return in ISO format for consistent parsing downstream
        if (cell.DataType == XLDataType.DateTime)
        {
            var dt = cell.GetDateTime();
            return dt.ToString("yyyy-MM-dd");
        }

        // For numbers, return with full precision
        if (cell.DataType == XLDataType.Number)
        {
            return cell.GetDouble().ToString("G");
        }

        return cell.GetString().Trim();
    }
}
