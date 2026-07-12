using Portal.Infrastructure.Entities.Import;
using Portal.Infrastructure.Models.Import;

namespace Portal.Infrastructure.Services.Import;

/// <summary>
/// Low-level file parsing service that coordinates CSV/Excel parsing with column mapping.
/// </summary>
public interface IFileParsingService
{
    List<ParsedRow> ParseCsv(Stream stream, ParserTemplate template);
    List<ParsedRow> ParseExcel(Stream stream, ParserTemplate template);
    List<ParsedRow> AutoDetectAndParse(Stream stream, string fileExtension);
}
