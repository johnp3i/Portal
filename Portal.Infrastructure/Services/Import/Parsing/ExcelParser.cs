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
