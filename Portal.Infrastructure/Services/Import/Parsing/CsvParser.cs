namespace Portal.Infrastructure.Services.Import.Parsing;

/// <summary>
/// RFC 4180-compliant CSV parser.
/// Handles quoted fields with embedded commas, newlines, and escaped double-quotes.
/// Preserves whitespace in quoted fields; trims whitespace in unquoted fields.
/// </summary>
public static class CsvParser
{
    /// <summary>
    /// Parses a CSV stream into a list of rows, each row being an array of field values.
    /// </summary>
    /// <param name="stream">The input stream (UTF-8 assumed).</param>
    /// <param name="delimiter">Field delimiter (defaults to comma).</param>
    /// <returns>List of string arrays representing each row.</returns>
    public static List<string[]> Parse(Stream stream, char delimiter = ',')
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var content = reader.ReadToEnd();
        return ParseContent(content, delimiter);
    }

    /// <summary>
    /// Parses CSV content string into a list of rows.
    /// </summary>
    public static List<string[]> ParseContent(string content, char delimiter = ',')
    {
        // Strip UTF-8 BOM if present
        if (content.Length > 0 && content[0] == '\uFEFF')
        {
            content = content.Substring(1);
        }

        var rows = new List<string[]>();
        var fields = new List<string>();
        var fieldBuilder = new System.Text.StringBuilder();
        var inQuotes = false;
        var fieldStarted = false;
        var isQuotedField = false;

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];

            if (!fieldStarted)
            {
                // Beginning of a new field
                fieldStarted = true;
                isQuotedField = false;
                fieldBuilder.Clear();

                if (c == '"')
                {
                    inQuotes = true;
                    isQuotedField = true;
                    continue;
                }
            }

            if (inQuotes)
            {
                if (c == '"')
                {
                    // Check for escaped quote (double quote "")
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        fieldBuilder.Append('"');
                        i++; // Skip next quote
                    }
                    else
                    {
                        // End of quoted field
                        inQuotes = false;
                    }
                }
                else
                {
                    fieldBuilder.Append(c);
                }
            }
            else
            {
                if (c == delimiter)
                {
                    // End of field
                    fields.Add(FinalizeField(fieldBuilder.ToString(), isQuotedField));
                    fieldStarted = false;
                }
                else if (c == '\r')
                {
                    // Possible end of row (\r\n or standalone \r)
                    fields.Add(FinalizeField(fieldBuilder.ToString(), isQuotedField));
                    rows.Add(fields.ToArray());
                    fields.Clear();
                    fieldStarted = false;

                    // Skip \n if it follows \r
                    if (i + 1 < content.Length && content[i + 1] == '\n')
                    {
                        i++;
                    }
                }
                else if (c == '\n')
                {
                    // End of row (standalone \n)
                    fields.Add(FinalizeField(fieldBuilder.ToString(), isQuotedField));
                    rows.Add(fields.ToArray());
                    fields.Clear();
                    fieldStarted = false;
                }
                else
                {
                    fieldBuilder.Append(c);
                }
            }
        }

        // Handle last field/row (file may not end with newline)
        if (fieldStarted || fields.Count > 0)
        {
            fields.Add(FinalizeField(fieldBuilder.ToString(), isQuotedField));
            rows.Add(fields.ToArray());
        }

        return rows;
    }

    /// <summary>
    /// Formats a row of values as a CSV line (for round-trip support).
    /// </summary>
    public static string FormatRow(string[] values, char delimiter = ',')
    {
        var fields = new string[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            fields[i] = FormatField(values[i], delimiter);
        }
        return string.Join(delimiter, fields);
    }

    private static string FinalizeField(string value, bool isQuotedField)
    {
        // Quoted fields preserve whitespace; unquoted fields are trimmed
        return isQuotedField ? value : value.Trim();
    }

    private static string FormatField(string value, char delimiter)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        // Quote the field if it contains delimiter, quotes, or newlines
        var needsQuoting = value.Contains(delimiter) ||
                           value.Contains('"') ||
                           value.Contains('\r') ||
                           value.Contains('\n');

        if (!needsQuoting)
            return value;

        // Escape internal quotes by doubling them
        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }
}
