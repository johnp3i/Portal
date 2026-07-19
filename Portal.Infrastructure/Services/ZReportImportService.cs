using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Handles Z-Report bulk import: parses CSV rows, groups by DateFrom+DateTo+ZNumber,
/// validates, detects duplicates, and bulk-inserts RevenueSummary + RevenueSummaryLines.
/// </summary>
public class ZReportImportService : IZReportImportService
{
    private readonly RevenueSummaryRepository _revenueSummaryRepository;
    private readonly RevenueSourceRepository _revenueSourceRepository;
    private readonly AuditLogRepository _auditLogRepository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly PortalDbContext _portalDbContext;

    public ZReportImportService(
        RevenueSummaryRepository revenueSummaryRepository,
        RevenueSourceRepository revenueSourceRepository,
        AuditLogRepository auditLogRepository,
        ICurrentTenantService currentTenantService,
        PortalDbContext portalDbContext)
    {
        _revenueSummaryRepository = revenueSummaryRepository;
        _revenueSourceRepository = revenueSourceRepository;
        _auditLogRepository = auditLogRepository;
        _currentTenantService = currentTenantService;
        _portalDbContext = portalDbContext;
    }

    /// <inheritdoc />
    public async Task<ServiceResult<ZReportImportPreview>> ParseAndPreviewAsync(
        Stream fileStream, string fileName, int revenueSourceId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        // Validate revenue source
        var source = await _revenueSourceRepository.GetByIdAndBusinessIdAsync(revenueSourceId, businessId);
        if (source == null)
            return ServiceResult<ZReportImportPreview>.Fail("Revenue source not found.");
        if (!source.IsActive)
            return ServiceResult<ZReportImportPreview>.Fail("Revenue source is inactive.");

        // Validate file extension
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        if (extension != ".csv")
            return ServiceResult<ZReportImportPreview>.Fail("Only CSV files are accepted for Z-Report import.");

        // Validate file size (5 MB max)
        if (fileStream.Length > 5 * 1024 * 1024)
            return ServiceResult<ZReportImportPreview>.Fail("File size exceeds the 5 MB limit.");

        // Parse CSV
        var (rows, parseError) = ParseCsvRows(fileStream);
        if (parseError != null)
            return ServiceResult<ZReportImportPreview>.Fail(parseError);

        if (rows.Count == 0)
            return ServiceResult<ZReportImportPreview>.Fail("No data rows found in the file.");

        if (rows.Count > 500)
            return ServiceResult<ZReportImportPreview>.Fail("File contains more than 500 data rows.");

        // Group rows by DateFrom + DateTo + ZNumber
        var groups = rows
            .GroupBy(r => new { r.DateFrom, r.DateTo, r.ZNumber })
            .Select(g => new ZReportImportGroup
            {
                DateFrom = g.Key.DateFrom,
                DateTo = g.Key.DateTo,
                ZReportNumber = g.Key.ZNumber,
                ExportDate = g.First().ExportDate,
                Lines = g.Select(r => new ZReportImportLine
                {
                    VatRate = r.VatRate,
                    NetAmount = r.NetAmount,
                    VatAmount = r.VatAmount,
                    DiscountAmount = r.DiscountAmount
                }).ToList()
            })
            .ToList();

        // Compute totals per group
        foreach (var group in groups)
        {
            group.TotalNet = group.Lines.Sum(l => l.NetAmount);
            group.TotalVat = group.Lines.Sum(l => l.VatAmount);
            group.TotalGross = group.TotalNet + group.TotalVat;
            group.TotalDiscount = group.Lines.Sum(l => l.DiscountAmount ?? 0m);
        }

        // Validate groups
        var errors = new List<string>();
        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            if (g.DateFrom == default)
                errors.Add($"Group {i + 1}: Invalid 'Date From'.");
            if (g.DateTo != default && g.DateTo < g.DateFrom)
                errors.Add($"Group {i + 1} (Z-{g.ZReportNumber}): 'Date To' is before 'Date From'.");
            if (string.IsNullOrWhiteSpace(g.ZReportNumber))
                errors.Add($"Group {i + 1}: Z-Report Number is required.");

            foreach (var line in g.Lines)
            {
                if (line.VatRate < 0 || line.VatRate > 100)
                    errors.Add($"Group {i + 1} (Z-{g.ZReportNumber}): VAT rate {line.VatRate} is out of range.");
                if (line.NetAmount < 0)
                    errors.Add($"Group {i + 1} (Z-{g.ZReportNumber}): Negative net amount.");
            }
        }

        // Duplicate detection
        var duplicates = new List<int>();
        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            if (!string.IsNullOrWhiteSpace(g.ZReportNumber))
            {
                var existingId = await _revenueSummaryRepository.FindDuplicateAsync(
                    businessId, revenueSourceId, g.ZReportNumber.Trim());
                if (existingId.HasValue)
                {
                    duplicates.Add(i);
                    g.IsDuplicate = true;
                    g.DuplicateOfId = existingId.Value;
                }
            }
        }

        return ServiceResult<ZReportImportPreview>.Ok(new ZReportImportPreview
        {
            FileName = fileName,
            RevenueSourceId = revenueSourceId,
            RevenueSourceName = source.Name,
            TotalCsvRows = rows.Count,
            TotalGroups = groups.Count,
            ValidGroups = groups.Count - errors.Count(e => true), // simplified
            DuplicateCount = duplicates.Count,
            Groups = groups,
            Errors = errors
        });
    }

    /// <inheritdoc />
    public async Task<ServiceResult<ZReportImportResult>> ConfirmImportAsync(
        ZReportImportPreview preview, List<int>? excludeGroupIndexes = null)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        var groupsToImport = preview.Groups
            .Where((g, idx) => !(excludeGroupIndexes?.Contains(idx) ?? false))
            .Where(g => !g.IsDuplicate || g.ImportDuplicateAnyway)
            .ToList();

        if (groupsToImport.Count == 0)
            return ServiceResult<ZReportImportResult>.Fail("No Z-Reports to import.");

        var importedCount = 0;
        var totalGross = 0m;

        using var transaction = await _portalDbContext.Database.BeginTransactionAsync();

        try
        {
            foreach (var group in groupsToImport)
            {
                var summary = new RevenueSummary
                {
                    BusinessId = businessId,
                    RevenueSourceId = preview.RevenueSourceId,
                    SummaryDate = group.DateFrom,
                    PeriodEndDate = group.DateTo != group.DateFrom ? group.DateTo : null,
                    ZReportNumber = group.ZReportNumber?.Trim(),
                    TotalNet = group.TotalNet,
                    TotalVat = group.TotalVat,
                    TotalGross = group.TotalGross,
                    TotalDiscount = group.TotalDiscount > 0 ? group.TotalDiscount : null,
                    ExportedAtUtc = group.ExportDate,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                };

                var summaryId = await _revenueSummaryRepository.InsertAsync(summary);

                foreach (var line in group.Lines)
                {
                    await _revenueSummaryRepository.InsertLineAsync(new RevenueSummaryLine
                    {
                        RevenueSummaryId = summaryId,
                        VatRate = line.VatRate,
                        NetAmount = line.NetAmount,
                        VatAmount = line.VatAmount,
                        TotalAmount = line.NetAmount + line.VatAmount,
                        DiscountAmount = line.DiscountAmount > 0 ? line.DiscountAmount : null,
                        CreatedAtUtc = DateTime.UtcNow
                    });
                }

                importedCount++;
                totalGross += group.TotalGross;
            }

            await _auditLogRepository.InsertAsync(new AuditLog
            {
                BusinessId = businessId,
                Action = "ZReportBulkImport",
                TableName = "revenue.RevenueSummary",
                RecordId = $"Batch:{importedCount}",
                NewValues = $"Imported {importedCount} Z-Reports from '{preview.FileName}'. Total Gross: {totalGross:N2}",
                Timestamp = DateTime.UtcNow
            });

            await transaction.CommitAsync();

            return ServiceResult<ZReportImportResult>.Ok(new ZReportImportResult
            {
                ImportedCount = importedCount,
                TotalGross = totalGross
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Parses CSV file into raw typed rows. Expected columns:
    /// Date From, Date To, Z-Number, VAT Rate, Net Sales, VAT Amount, Discount, Export Date
    /// </summary>
    private (List<ZReportCsvRow> Rows, string? Error) ParseCsvRows(Stream stream)
    {
        var rows = new List<ZReportCsvRow>();

        using var reader = new StreamReader(stream);
        var headerLine = reader.ReadLine();
        if (headerLine == null)
            return (rows, "File is empty.");

        // Detect separator (comma or semicolon)
        var separator = headerLine.Contains(';') ? ';' : ',';
        var headers = headerLine.Split(separator).Select(h => h.Trim().ToLowerInvariant()).ToArray();

        // Find column indexes
        var dateFromIdx = FindColumnIndex(headers, "date from", "datefrom", "from");
        var dateToIdx = FindColumnIndex(headers, "date to", "dateto", "to");
        var zNumberIdx = FindColumnIndex(headers, "z-number", "znumber", "z number", "z_number", "zreport");
        var vatRateIdx = FindColumnIndex(headers, "vat rate", "vatrate", "vat %", "vat_rate", "rate");
        var netIdx = FindColumnIndex(headers, "net sales", "netsales", "net", "net_sales", "net amount");
        var vatAmountIdx = FindColumnIndex(headers, "vat amount", "vatamount", "vat");
        var discountIdx = FindColumnIndex(headers, "discount", "disc");
        var exportDateIdx = FindColumnIndex(headers, "export date", "exportdate", "export_date", "exported");

        if (dateFromIdx == -1) return (rows, "Required column 'Date From' not found in header.");
        if (zNumberIdx == -1) return (rows, "Required column 'Z-Number' not found in header.");
        if (vatRateIdx == -1) return (rows, "Required column 'VAT Rate' not found in header.");
        if (netIdx == -1) return (rows, "Required column 'Net Sales' not found in header.");
        if (vatAmountIdx == -1) return (rows, "Required column 'VAT Amount' not found in header.");

        int lineNumber = 1;
        while (!reader.EndOfStream)
        {
            lineNumber++;
            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = line.Split(separator);

            var row = new ZReportCsvRow();

            // Parse Date From (required)
            if (!TryParseDate(GetCol(cols, dateFromIdx), out var dateFrom))
                return (rows, $"Row {lineNumber}: Invalid 'Date From' value.");
            row.DateFrom = dateFrom;

            // Parse Date To (optional — defaults to DateFrom)
            if (dateToIdx >= 0 && TryParseDate(GetCol(cols, dateToIdx), out var dateTo))
                row.DateTo = dateTo;
            else
                row.DateTo = dateFrom;

            // Z-Number (required)
            row.ZNumber = GetCol(cols, zNumberIdx)?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(row.ZNumber))
                return (rows, $"Row {lineNumber}: Z-Number is empty.");

            // VAT Rate
            if (!TryParseDecimal(GetCol(cols, vatRateIdx), out var vatRate))
                return (rows, $"Row {lineNumber}: Invalid 'VAT Rate' value.");
            row.VatRate = vatRate;

            // Net Sales
            if (!TryParseDecimal(GetCol(cols, netIdx), out var netAmount))
                return (rows, $"Row {lineNumber}: Invalid 'Net Sales' value.");
            row.NetAmount = netAmount;

            // VAT Amount
            if (!TryParseDecimal(GetCol(cols, vatAmountIdx), out var vatAmount))
                return (rows, $"Row {lineNumber}: Invalid 'VAT Amount' value.");
            row.VatAmount = vatAmount;

            // Discount (optional)
            if (discountIdx >= 0 && TryParseDecimal(GetCol(cols, discountIdx), out var discount))
                row.DiscountAmount = discount;

            // Export Date (optional)
            if (exportDateIdx >= 0 && TryParseDateTime(GetCol(cols, exportDateIdx), out var exportDt))
                row.ExportDate = exportDt;

            rows.Add(row);
        }

        return (rows, null);
    }

    private static int FindColumnIndex(string[] headers, params string[] candidates)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            foreach (var candidate in candidates)
            {
                if (headers[i] == candidate || headers[i].Replace(" ", "") == candidate.Replace(" ", ""))
                    return i;
            }
        }
        return -1;
    }

    private static string? GetCol(string[] cols, int index)
    {
        if (index < 0 || index >= cols.Length) return null;
        return cols[index].Trim();
    }

    private static bool TryParseDate(string? value, out DateOnly result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        // Try multiple date formats
        string[] formats = { "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd", "d/M/yyyy", "dd-MM-yyyy" };
        foreach (var fmt in formats)
        {
            if (DateOnly.TryParseExact(value, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                return true;
        }
        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }

    private static bool TryParseDateTime(string? value, out DateTime result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        string[] formats = { "dd/MM/yyyy HH:mm", "MM/dd/yyyy HH:mm", "yyyy-MM-dd HH:mm", "dd/MM/yyyy", "yyyy-MM-ddTHH:mm" };
        foreach (var fmt in formats)
        {
            if (DateTime.TryParseExact(value, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                return true;
        }
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }

    private static bool TryParseDecimal(string? value, out decimal result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;
        // Handle both dot and comma as decimal separators
        value = value.Replace(",", ".");
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }
}

// ═══════════════════════════════════════════════════════════
// Models
// ═══════════════════════════════════════════════════════════

internal class ZReportCsvRow
{
    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }
    public string ZNumber { get; set; } = null!;
    public decimal VatRate { get; set; }
    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal? DiscountAmount { get; set; }
    public DateTime? ExportDate { get; set; }
}
