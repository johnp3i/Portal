using System.Text.Json;
using Portal.Infrastructure.Entities.Import;
using Portal.Infrastructure.Models.Import;
using Portal.Infrastructure.Services.Import.Parsing;

namespace Portal.Infrastructure.Services.Import;

/// <summary>
/// Coordinates CSV/Excel parsing with column mapping to produce ParsedRow lists.
/// </summary>
public class FileParsingService : IFileParsingService
{
    public List<ParsedRow> ParseCsv(Stream stream, ParserTemplate template)
    {
        var rawRows = CsvParser.Parse(stream);
        var mappings = DeserializeMappings(template.ColumnMappingsJson);
        var headerRowIndex = template.HeaderRow - 1; // Convert 1-based to 0-based
        var dataStartRowIndex = template.DataStartRow - 1;

        return ColumnMapper.Map(rawRows, mappings, headerRowIndex, dataStartRowIndex);
    }

    public List<ParsedRow> ParseExcel(Stream stream, ParserTemplate template)
    {
        var rawRows = ExcelParser.Parse(stream, template.SheetName);
        var mappings = DeserializeMappings(template.ColumnMappingsJson);
        var headerRowIndex = template.HeaderRow - 1;
        var dataStartRowIndex = template.DataStartRow - 1;

        return ColumnMapper.Map(rawRows, mappings, headerRowIndex, dataStartRowIndex);
    }

    public List<ParsedRow> AutoDetectAndParse(Stream stream, string fileExtension)
    {
        List<string[]> rawRows;

        if (fileExtension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            rawRows = CsvParser.Parse(stream);
        }
        else
        {
            rawRows = ExcelParser.Parse(stream);
        }

        if (rawRows.Count < 2)
            return new List<ParsedRow>();

        // Auto-detect mappings from header row (first row)
        var headerRow = rawRows[0];
        var mappings = ColumnMapper.AutoDetect(headerRow);

        if (mappings.Count == 0)
            return new List<ParsedRow>();

        // Default: header = row 0, data starts at row 1
        return ColumnMapper.Map(rawRows, mappings, 0, 1);
    }

    private static List<ColumnMapping> DeserializeMappings(string json)
    {
        return JsonSerializer.Deserialize<List<ColumnMapping>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<ColumnMapping>();
    }
}
