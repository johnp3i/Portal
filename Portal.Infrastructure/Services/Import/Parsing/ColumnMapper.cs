using System.Globalization;
using Portal.Infrastructure.Models.Import;

namespace Portal.Infrastructure.Services.Import.Parsing;

/// <summary>
/// Applies column mappings to raw row data, producing ParsedRow objects.
/// Resolves source columns by header name or positional index,
/// applies date/decimal format parsing, and skips marked columns.
/// </summary>
public static class ColumnMapper
{
    /// <summary>
    /// Maps raw string rows to ParsedRow objects using the provided column mappings.
    /// </summary>
    /// <param name="rawRows">All rows from the file (including header rows).</param>
    /// <param name="mappings">Column mapping configuration.</param>
    /// <param name="headerRowIndex">0-based index of the header row.</param>
    /// <param name="dataStartRowIndex">0-based index of the first data row.</param>
    /// <returns>List of parsed rows from the data region.</returns>
    public static List<ParsedRow> Map(List<string[]> rawRows, List<ColumnMapping> mappings, int headerRowIndex, int dataStartRowIndex)
    {
        if (rawRows.Count == 0 || dataStartRowIndex >= rawRows.Count)
            return new List<ParsedRow>();

        // Build header name → column index lookup
        var headerLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (headerRowIndex < rawRows.Count)
        {
            var headerRow = rawRows[headerRowIndex];
            for (var i = 0; i < headerRow.Length; i++)
            {
                var name = headerRow[i].Trim();
                if (!string.IsNullOrEmpty(name) && !headerLookup.ContainsKey(name))
                {
                    headerLookup[name] = i;
                }
            }
        }

        var result = new List<ParsedRow>();

        for (var rowIdx = dataStartRowIndex; rowIdx < rawRows.Count; rowIdx++)
        {
            var rawRow = rawRows[rowIdx];

            // Skip entirely empty rows
            if (rawRow.All(f => string.IsNullOrWhiteSpace(f)))
                continue;

            var parsed = new ParsedRow
            {
                RowNumber = rowIdx + 1 // 1-based for display
            };

            foreach (var mapping in mappings)
            {
                if (mapping.IsSkipped)
                    continue;

                var value = ResolveValue(rawRow, mapping, headerLookup);
                if (value != null)
                {
                    parsed.RawValues[mapping.TargetField] = value;
                }

                ApplyValue(parsed, mapping.TargetField, value, mapping.Format);
            }

            // Skip rows that have no valid date (summary rows, section headers, footers)
            // InvoiceDate is required for any importable purchase row.
            if (!parsed.InvoiceDate.HasValue)
                continue;

            result.Add(parsed);
        }

        return result;
    }

    /// <summary>
    /// Attempts auto-detection of column mappings by matching header names to target fields.
    /// </summary>
    public static List<ColumnMapping> AutoDetect(string[] headerRow)
    {
        var mappings = new List<ColumnMapping>();
        var headerAliases = GetHeaderAliases();

        for (var i = 0; i < headerRow.Length; i++)
        {
            var header = headerRow[i].Trim();
            if (string.IsNullOrEmpty(header))
                continue;

            foreach (var (targetField, aliases) in headerAliases)
            {
                if (aliases.Any(a => string.Equals(a, header, StringComparison.OrdinalIgnoreCase)))
                {
                    mappings.Add(new ColumnMapping
                    {
                        SourceColumn = header,
                        SourceIndex = i,
                        TargetField = targetField,
                        IsSkipped = false
                    });
                    break;
                }
            }
        }

        return mappings;
    }

    private static string? ResolveValue(string[] row, ColumnMapping mapping, Dictionary<string, int> headerLookup)
    {
        int? colIndex = null;

        if (mapping.SourceIndex.HasValue)
        {
            colIndex = mapping.SourceIndex.Value;
        }
        else if (!string.IsNullOrEmpty(mapping.SourceColumn) && headerLookup.TryGetValue(mapping.SourceColumn, out var idx))
        {
            colIndex = idx;
        }

        if (colIndex.HasValue && colIndex.Value < row.Length)
        {
            var val = row[colIndex.Value];
            return string.IsNullOrWhiteSpace(val) ? null : val.Trim();
        }

        return null;
    }

    private static void ApplyValue(ParsedRow row, string targetField, string? value, string? format)
    {
        if (value == null)
            return;

        switch (targetField)
        {
            case ImportTargetFields.InvoiceDate:
                row.InvoiceDate = ParseDate(value, format);
                break;
            case ImportTargetFields.InvoiceNumber:
                row.InvoiceNumber = value;
                break;
            case ImportTargetFields.Description:
                row.Description = value;
                break;
            case ImportTargetFields.AmountExcludingVat:
                row.AmountExcludingVat = ParseDecimal(value, format);
                break;
            case ImportTargetFields.VatAmount:
                row.VatAmount = ParseDecimal(value, format);
                break;
            case ImportTargetFields.TotalAmount:
                row.TotalAmount = ParseDecimal(value, format);
                break;
            case ImportTargetFields.PurchaseOriginType:
                row.PurchaseOriginTypeName = value;
                break;
            case ImportTargetFields.Country:
                row.Country = value;
                break;
            case ImportTargetFields.Notes:
                row.Notes = value;
                break;
        }
    }

    private static DateOnly? ParseDate(string value, string? format)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Try explicit format first
        if (!string.IsNullOrEmpty(format))
        {
            if (DateOnly.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
                return exact;
        }

        // Try common formats
        var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy", "dd-MM-yyyy", "d/M/yyyy", "dd.MM.yyyy" };
        foreach (var f in formats)
        {
            if (DateOnly.TryParseExact(value, f, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return parsed;
        }

        // Last resort: generic parse
        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var generic))
            return generic;

        return null;
    }

    private static decimal? ParseDecimal(string value, string? format)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Remove currency symbols and whitespace
        value = value.Replace("€", "").Replace("$", "").Replace("£", "").Trim();

        // Handle comma as decimal separator if format specifies it
        if (format == ",")
        {
            // Comma is decimal separator, period is thousands
            value = value.Replace(".", "").Replace(",", ".");
        }
        else if (format == ".")
        {
            // Period is decimal separator, comma is thousands
            value = value.Replace(",", "");
        }
        else
        {
            // Auto-detect: if last separator is comma and has 1-2 digits after, treat as decimal
            var lastComma = value.LastIndexOf(',');
            var lastDot = value.LastIndexOf('.');

            if (lastComma > lastDot && value.Length - lastComma <= 3)
            {
                // Comma is likely the decimal separator
                value = value.Replace(".", "").Replace(",", ".");
            }
            else
            {
                // Period is likely the decimal separator
                value = value.Replace(",", "");
            }
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
            return result;

        return null;
    }

    private static Dictionary<string, string[]> GetHeaderAliases()
    {
        return new Dictionary<string, string[]>
        {
            [ImportTargetFields.InvoiceDate] = new[] { "Date", "Invoice Date", "InvoiceDate", "Inv Date", "Transaction Date" },
            [ImportTargetFields.InvoiceNumber] = new[] { "Invoice Number", "InvoiceNumber", "Invoice No", "Invoice #", "Inv No", "Reference" },
            [ImportTargetFields.Description] = new[] { "Description", "Desc", "Details", "Item", "Narration" },
            [ImportTargetFields.AmountExcludingVat] = new[] { "Amount Excl VAT", "AmountExcludingVat", "Net Amount", "Net", "Excl VAT", "Amount Ex VAT", "Subtotal" },
            [ImportTargetFields.VatAmount] = new[] { "VAT Amount", "VatAmount", "VAT", "Tax", "Tax Amount" },
            [ImportTargetFields.TotalAmount] = new[] { "Total Amount", "TotalAmount", "Total", "Gross", "Amount", "Gross Amount" },
            [ImportTargetFields.PurchaseOriginType] = new[] { "Origin Type", "PurchaseOriginType", "Origin", "Type" },
            [ImportTargetFields.Country] = new[] { "Country", "Origin Country" },
            [ImportTargetFields.Notes] = new[] { "Notes", "Note", "Comments", "Memo" }
        };
    }
}
