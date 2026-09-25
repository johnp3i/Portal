using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Entities.Sales;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Repositories.Sales;
using Portal.Infrastructure.Services.Import.Parsing;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Parses the prospecting workbook and imports rows into a campaign. Reuses the shared
/// <see cref="ExcelParser"/> (ClosedXML) for reading; mapping/validation is prospect-specific
/// because the generic ColumnMapper is bound to invoice/purchase target fields.
/// </summary>
public class ProspectImportService : IProspectImportService
{
    private readonly ProspectRepository _prospectRepository;
    private readonly ProspectCampaignRepository _campaignRepository;
    private readonly AuditLogRepository _auditLogRepository;
    private readonly ICurrentTenantService _tenantService;
    private readonly PortalDbContext _context;

    // The workbook sheet holding the prospects. Other sheets are ignored.
    private const string ProspectsSheetName = "30 Prospects";
    private const int MaxRows = 1000;

    // Header aliases per target field (case-insensitive). Matches the Chaplin Pro workbook
    // headers but tolerant of small variations.
    private static readonly Dictionary<string, string[]> HeaderAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ImportRowId"] = new[] { "ID", "Ref", "Row" },
        ["Name"] = new[] { "Prospect", "Name", "Company", "Business" },
        ["Segment"] = new[] { "Segment" },
        ["Location"] = new[] { "Location", "Area", "City" },
        ["BusinessType"] = new[] { "Business Type", "BusinessType", "Type" },
        ["PublicContactRole"] = new[] { "Public Contact / Role", "Public Contact", "Contact / Role", "Contact", "Role" },
        ["Phone"] = new[] { "Phone", "Telephone", "Tel" },
        ["Email"] = new[] { "Email", "E-mail" },
        ["Website"] = new[] { "Official Website", "Website", "Web", "URL" },
        ["PublicEvidence"] = new[] { "Public Evidence", "Evidence" },
        ["WhyFit"] = new[] { "Why Chaplin Pro May Fit", "Why Fit", "WhyFit", "Why It Fits", "Fit" },
        ["IcpFitScore"] = new[] { "ICP Fit /5", "ICP Fit", "ICP /5", "ICP" },
        ["PainProbabilityScore"] = new[] { "Pain Probability /5", "Pain Probability", "Pain /5", "Pain" },
        ["AccessibilityScore"] = new[] { "Accessibility /5", "Accessibility", "Access /5", "Access" },
        ["LearningValueScore"] = new[] { "Learning Value /5", "Learning Value", "Learning /5", "Learning" },
        ["RecommendedFirstContact"] = new[] { "Recommended First Contact", "First Contact", "Recommended Contact" },
        ["ResearchSourceUrl"] = new[] { "Research Source", "Source", "Research", "Source URL" }
        // "Total /20" and "Priority" are intentionally NOT mapped — they are recomputed.
    };

    public ProspectImportService(
        ProspectRepository prospectRepository,
        ProspectCampaignRepository campaignRepository,
        AuditLogRepository auditLogRepository,
        ICurrentTenantService tenantService,
        PortalDbContext context)
    {
        _prospectRepository = prospectRepository;
        _campaignRepository = campaignRepository;
        _auditLogRepository = auditLogRepository;
        _tenantService = tenantService;
        _context = context;
    }

    public async Task<ServiceResult<ProspectImportPreview>> ParseAndPreviewAsync(Stream fileStream, string fileName, int campaignId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
            if (extension != ".xlsx")
                return ServiceResult<ProspectImportPreview>.Fail("Only .xlsx files are accepted for prospect import.");

            if (fileStream.Length > 5 * 1024 * 1024)
                return ServiceResult<ProspectImportPreview>.Fail("File size exceeds the 5 MB limit.");

            var campaign = await _campaignRepository.GetByIdAsync(campaignId, businessId);
            if (campaign == null)
                return ServiceResult<ProspectImportPreview>.Fail("Campaign not found.");

            // Parse the specific sheet. Fall back to the first sheet if the named one is absent.
            List<string[]> rawRows;
            try
            {
                rawRows = ExcelParser.Parse(fileStream, ProspectsSheetName);
            }
            catch (Exception ex)
            {
                // Sheet not found (or unreadable) — surface a clear message rather than throwing.
                return ServiceResult<ProspectImportPreview>.Fail(
                    $"Could not read the '{ProspectsSheetName}' sheet. Ensure the workbook contains it. ({ex.Message})");
            }

            if (rawRows.Count < 2)
                return ServiceResult<ProspectImportPreview>.Fail("No data rows found in the workbook.");

            // Header row = row 0. Build target-field -> column-index map.
            var headerRow = rawRows[0];
            var columnMap = BuildColumnMap(headerRow);

            var preview = new ProspectImportPreview
            {
                FileName = fileName,
                ProspectCampaignId = campaignId,
                CampaignName = campaign.Name
            };

            if (!columnMap.ContainsKey("Name"))
            {
                preview.FileErrors.Add("Required column 'Prospect' (name) was not found in the header row.");
                return ServiceResult<ProspectImportPreview>.Ok(preview);
            }

            var dataRows = rawRows.Skip(1).ToList();
            if (dataRows.Count > MaxRows)
                return ServiceResult<ProspectImportPreview>.Fail($"Workbook contains more than {MaxRows} data rows.");

            var rows = new List<ProspectImportRow>();
            for (int i = 0; i < dataRows.Count; i++)
            {
                var raw = dataRows[i];
                if (raw.All(string.IsNullOrWhiteSpace))
                    continue;

                var row = MapRow(raw, columnMap, excelRowNumber: i + 2); // +2: header is row 1, data starts row 2
                rows.Add(row);
            }

            // Duplicate detection within the campaign (idempotent re-import).
            foreach (var row in rows.Where(r => r.IsValid))
            {
                var existingId = await _prospectRepository.FindForImportAsync(
                    campaignId, businessId, row.ImportRowId, row.Name, row.Website, row.Email);
                if (existingId.HasValue)
                {
                    row.IsDuplicate = true;
                    row.DuplicateOfId = existingId.Value;
                }
            }

            preview.Rows = rows;
            preview.TotalRows = rows.Count;
            preview.ValidRows = rows.Count(r => r.IsValid);
            preview.InvalidRows = rows.Count(r => !r.IsValid);
            preview.DuplicateRows = rows.Count(r => r.IsValid && r.IsDuplicate);
            preview.NewRows = rows.Count(r => r.IsValid && !r.IsDuplicate);

            return ServiceResult<ProspectImportPreview>.Ok(preview);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult<ProspectImportResult>> ConfirmImportAsync(ProspectImportPreview preview)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            if (preview.FileErrors.Count > 0)
                return ServiceResult<ProspectImportResult>.Fail("The file could not be imported: " + string.Join(" ", preview.FileErrors));

            // Re-validate the campaign still exists for this tenant.
            var campaign = await _campaignRepository.GetByIdAsync(preview.ProspectCampaignId, businessId);
            if (campaign == null)
                return ServiceResult<ProspectImportResult>.Fail("Campaign not found.");

            var importable = preview.Rows.Where(r => r.IsValid).ToList();
            if (importable.Count == 0)
                return ServiceResult<ProspectImportResult>.Fail("There are no valid rows to import.");

            var result = new ProspectImportResult
            {
                SkippedInvalidCount = preview.Rows.Count(r => !r.IsValid)
            };

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var row in importable)
                {
                    if (row.IsDuplicate && row.DuplicateOfId.HasValue)
                    {
                        // Safe re-import: refresh the existing prospect's research + scores.
                        var existing = await _prospectRepository.GetByIdAsync(row.DuplicateOfId.Value, businessId);
                        if (existing == null)
                        {
                            // Was deleted since preview — treat as new instead of failing.
                            await InsertRowAsync(row, preview.ProspectCampaignId, businessId);
                            result.CreatedCount++;
                            continue;
                        }

                        ApplyRowToEntity(row, existing);
                        await _prospectRepository.UpdateAsync(existing);
                        result.UpdatedCount++;
                    }
                    else
                    {
                        await InsertRowAsync(row, preview.ProspectCampaignId, businessId);
                        result.CreatedCount++;
                    }
                }

                await _auditLogRepository.InsertAsync(new AuditLog
                {
                    BusinessId = businessId,
                    Action = "ProspectBulkImport",
                    TableName = "sales.Prospect",
                    RecordId = $"Campaign:{preview.ProspectCampaignId}",
                    NewValues = $"Imported from '{preview.FileName}': {result.CreatedCount} created, {result.UpdatedCount} updated, {result.SkippedInvalidCount} skipped.",
                    Timestamp = DateTime.UtcNow
                });

                await transaction.CommitAsync();
                return ServiceResult<ProspectImportResult>.Ok(result);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private async Task InsertRowAsync(ProspectImportRow row, int campaignId, int businessId)
    {
        var entity = new Prospect
        {
            BusinessId = businessId,
            ProspectCampaignId = campaignId,
            Status = 1, // Research
            ImportRowId = row.ImportRowId
        };
        ApplyRowToEntity(row, entity);
        await _prospectRepository.InsertAsync(entity);
    }

    private static void ApplyRowToEntity(ProspectImportRow row, Prospect entity)
    {
        entity.Name = row.Name;
        entity.Segment = row.Segment;
        entity.Location = row.Location;
        entity.BusinessType = row.BusinessType;
        entity.PublicContactRole = row.PublicContactRole;
        entity.Phone = row.Phone;
        entity.Email = row.Email;
        entity.Website = row.Website;
        entity.PublicEvidence = row.PublicEvidence;
        entity.WhyFit = row.WhyFit;
        entity.ResearchSourceUrl = row.ResearchSourceUrl;
        entity.RecommendedFirstContact = row.RecommendedFirstContact;
        entity.IcpFitScore = row.IcpFitScore;
        entity.PainProbabilityScore = row.PainProbabilityScore;
        entity.AccessibilityScore = row.AccessibilityScore;
        entity.LearningValueScore = row.LearningValueScore;
        // ImportRowId is only set on insert (identity anchor); keep existing on update.
        if (!string.IsNullOrWhiteSpace(row.ImportRowId) && string.IsNullOrWhiteSpace(entity.ImportRowId))
            entity.ImportRowId = row.ImportRowId;
    }

    /// <summary>Resolve each target field to a source column index using header aliases.</summary>
    private static Dictionary<string, int> BuildColumnMap(string[] headerRow)
    {
        var map = new Dictionary<string, int>();
        for (int i = 0; i < headerRow.Length; i++)
        {
            var header = headerRow[i]?.Trim();
            if (string.IsNullOrEmpty(header))
                continue;

            foreach (var (field, aliases) in HeaderAliases)
            {
                if (map.ContainsKey(field))
                    continue;
                if (aliases.Any(a => string.Equals(a, header, StringComparison.OrdinalIgnoreCase)))
                {
                    map[field] = i;
                    break;
                }
            }
        }
        return map;
    }

    private static ProspectImportRow MapRow(string[] raw, Dictionary<string, int> map, int excelRowNumber)
    {
        string? Get(string field)
        {
            if (!map.TryGetValue(field, out var idx) || idx >= raw.Length)
                return null;
            var v = raw[idx]?.Trim();
            return string.IsNullOrWhiteSpace(v) ? null : v;
        }

        var row = new ProspectImportRow
        {
            RowNumber = excelRowNumber,
            ImportRowId = Truncate(Get("ImportRowId"), 40),
            Name = Get("Name") ?? string.Empty,
            Segment = Truncate(Get("Segment"), 100),
            Location = Truncate(Get("Location"), 150),
            BusinessType = Truncate(Get("BusinessType"), 150),
            PublicContactRole = Truncate(Get("PublicContactRole"), 200),
            Phone = Truncate(Get("Phone"), 60),
            Email = Truncate(Get("Email"), 320),
            Website = Truncate(Get("Website"), 300),
            PublicEvidence = Get("PublicEvidence"),
            WhyFit = Get("WhyFit"),
            RecommendedFirstContact = Truncate(Get("RecommendedFirstContact"), 300),
            ResearchSourceUrl = Truncate(Get("ResearchSourceUrl"), 500)
        };

        if (string.IsNullOrWhiteSpace(row.Name))
            row.Errors.Add($"Row {excelRowNumber}: prospect name is required.");

        row.IcpFitScore = ParseScore(Get("IcpFitScore"), "ICP Fit", excelRowNumber, row.Errors);
        row.PainProbabilityScore = ParseScore(Get("PainProbabilityScore"), "Pain Probability", excelRowNumber, row.Errors);
        row.AccessibilityScore = ParseScore(Get("AccessibilityScore"), "Accessibility", excelRowNumber, row.Errors);
        row.LearningValueScore = ParseScore(Get("LearningValueScore"), "Learning Value", excelRowNumber, row.Errors);

        // Total + Priority are always recomputed, never trusted from the workbook.
        row.Total = ProspectScoring.ComputeTotal(row.IcpFitScore, row.PainProbabilityScore, row.AccessibilityScore, row.LearningValueScore);
        row.Priority = ProspectScoring.ComputePriority(row.Total);

        return row;
    }

    /// <summary>Parses a 0-5 score. Blank = 0. Out-of-range / non-numeric = validation error.</summary>
    private static byte ParseScore(string? value, string label, int rowNumber, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        // Cells may arrive as "3" or "3.0" (ExcelParser emits numbers via "G").
        var trimmed = value.Trim();
        if (double.TryParse(trimmed, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d))
        {
            var rounded = (int)Math.Round(d);
            if (rounded < 0 || rounded > 5)
            {
                errors.Add($"Row {rowNumber}: {label} score '{value}' is out of range (0-5).");
                return 0;
            }
            return (byte)rounded;
        }

        errors.Add($"Row {rowNumber}: {label} score '{value}' is not a number.");
        return 0;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }

    // The exact header row of the "30 Prospects" sheet (matches the original workbook). The two
    // computed columns are included for familiarity, but the importer ignores and recomputes them.
    private static readonly string[] TemplateHeaders =
    {
        "ID", "Prospect", "Segment", "Location", "Business Type", "Public Contact / Role",
        "Phone", "Email", "Official Website", "Public Evidence", "Why It May Fit",
        "ICP Fit /5", "Pain Probability /5", "Accessibility /5", "Learning Value /5",
        "Total /20", "Priority", "Recommended First Contact", "Research Source"
    };

    // Realistic example rows (generic, product-neutral) so a first-time user sees the shape.
    private static readonly object?[][] TemplateExampleRows =
    {
        new object?[]
        {
            "A01", "Acme Events Co.", "Event / Conference", "Nicosia", "Conference organiser",
            "Operations Manager", "", "", "https://example-acme-events.com",
            "Runs multi-day conferences with external speakers and sponsors.",
            "Coordinating speakers, sponsors and files across teams is manual today.",
            5, 4, 5, 4, 18, "A", "Call operations manager", "https://example-acme-events.com/about"
        },
        new object?[]
        {
            "A02", "Harbour View Venue", "Venue", "Limassol", "Events venue",
            "Events Coordinator", "", "", "https://example-harbourview.com",
            "Hosts corporate events; publishes an events calendar.",
            "Likely juggles client requirements over email and spreadsheets.",
            4, 3, 4, 3, 14, "B", "Email events coordinator", "https://example-harbourview.com/events"
        },
        new object?[]
        {
            "A03", "Bright Agency", "Marketing / Creative", "Nicosia", "Creative agency",
            "Account Director", "", "", "https://example-bright.com",
            "Manages campaigns with multiple client stakeholders.",
            "Approval and asset collaboration is a plausible pain point.",
            3, 3, 3, 3, 12, "B", "Intro email", "https://example-bright.com/work"
        }
    };

    public byte[] GenerateTemplate()
    {
        try
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add(ProspectsSheetName);

            // Header row
            for (int c = 0; c < TemplateHeaders.Length; c++)
            {
                var cell = sheet.Cell(1, c + 1);
                cell.Value = TemplateHeaders[c];
                cell.Style.Font.Bold = true;
            }

            // Example rows
            for (int r = 0; r < TemplateExampleRows.Length; r++)
            {
                var row = TemplateExampleRows[r];
                for (int c = 0; c < row.Length; c++)
                {
                    var value = row[c];
                    var cell = sheet.Cell(r + 2, c + 1);
                    if (value is int i) cell.Value = i;
                    else cell.Value = value?.ToString() ?? string.Empty;
                }
            }

            sheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
