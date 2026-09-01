using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Handles Sales Invoice CSV import into ExternalSalesRecord.
/// Each CSV row = one transaction record (no grouping, unlike Z-Report import).
/// </summary>
public class SalesImportService : ISalesImportService
{
    private readonly ExternalSalesRecordRepository _repository;
    private readonly RevenueSourceRepository _revenueSourceRepository;
    private readonly ExternalPlatformRepository _externalPlatformRepository;
    private readonly VatSubmissionPeriodRepository _vatSubmissionPeriodRepository;
    private readonly AuditLogRepository _auditLogRepository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly PortalDbContext _portalDbContext;

    public SalesImportService(
        ExternalSalesRecordRepository repository,
        RevenueSourceRepository revenueSourceRepository,
        ExternalPlatformRepository externalPlatformRepository,
        VatSubmissionPeriodRepository vatSubmissionPeriodRepository,
        AuditLogRepository auditLogRepository,
        ICurrentTenantService currentTenantService,
        PortalDbContext portalDbContext)
    {
        _repository = repository;
        _revenueSourceRepository = revenueSourceRepository;
        _externalPlatformRepository = externalPlatformRepository;
        _vatSubmissionPeriodRepository = vatSubmissionPeriodRepository;
        _auditLogRepository = auditLogRepository;
        _currentTenantService = currentTenantService;
        _portalDbContext = portalDbContext;
    }

    public async Task<ServiceResult<SalesImportPreview>> ParseAndPreviewAsync(
        Stream fileStream, string fileName, int? revenueSourceId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        // Validate revenue source if provided
        string? sourceName = null;
        if (revenueSourceId.HasValue)
        {
            var source = await _revenueSourceRepository.GetByIdAndBusinessIdAsync(revenueSourceId.Value, businessId);
            if (source == null)
                return ServiceResult<SalesImportPreview>.Fail("Revenue source not found.");
            if (!source.IsActive)
                return ServiceResult<SalesImportPreview>.Fail("Revenue source is inactive.");
            sourceName = source.Name;
        }

        // Validate file
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        if (extension != ".csv")
            return ServiceResult<SalesImportPreview>.Fail("Only CSV files are accepted.");
        if (fileStream.Length > 5 * 1024 * 1024)
            return ServiceResult<SalesImportPreview>.Fail("File size exceeds the 5 MB limit.");

        // Parse CSV
        var (rows, parseError) = ParseCsvRows(fileStream);
        if (parseError != null)
            return ServiceResult<SalesImportPreview>.Fail(parseError);
        if (rows.Count == 0)
            return ServiceResult<SalesImportPreview>.Fail("No data rows found in the file.");
        if (rows.Count > 500)
            return ServiceResult<SalesImportPreview>.Fail("File contains more than 500 data rows.");

        // Validate and check duplicates
        var errors = new List<string>();
        var duplicateCount = 0;

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];

            if (row.TransactionDate == default)
            {
                row.IsValid = false;
                row.ValidationError = "Invalid transaction date.";
                continue;
            }
            if (row.NetAmount < 0)
            {
                row.IsValid = false;
                row.ValidationError = "Net amount cannot be negative.";
                continue;
            }
            if (row.VatAmount < 0)
            {
                row.IsValid = false;
                row.ValidationError = "VAT amount cannot be negative.";
                continue;
            }

            // Duplicate detection (only if invoice number is provided)
            if (!string.IsNullOrWhiteSpace(row.InvoiceNumber))
            {
                // Exact duplicate: same source + invoice + date
                var isDuplicate = await _repository.ExistsDuplicateAsync(
                    businessId, revenueSourceId, row.InvoiceNumber.Trim(), row.TransactionDate);
                if (isDuplicate)
                {
                    row.IsDuplicate = true;
                    duplicateCount++;
                }
                else
                {
                    // Cross-source warning: same invoice + date exists under a different source
                    var otherSource = await _repository.FindCrossSourceDuplicateAsync(
                        businessId, revenueSourceId, row.InvoiceNumber.Trim(), row.TransactionDate);
                    if (otherSource != null)
                    {
                        row.HasCrossSourceWarning = true;
                        row.CrossSourceWarning = $"Same invoice exists under \"{otherSource}\"";
                    }
                }
            }
        }

        var validRows = rows.Count(r => r.IsValid && !r.IsDuplicate);
        var batchTotal = rows.Where(r => r.IsValid && !r.IsDuplicate).Sum(r => r.TotalAmount);

        return ServiceResult<SalesImportPreview>.Ok(new SalesImportPreview
        {
            FileName = fileName,
            RevenueSourceId = revenueSourceId,
            RevenueSourceName = sourceName,
            TotalRows = rows.Count,
            ValidRows = validRows,
            DuplicateCount = duplicateCount,
            BatchTotal = batchTotal,
            Rows = rows,
            Errors = errors
        });
    }

    public async Task<ServiceResult<SalesImportResult>> ConfirmImportAsync(
        SalesImportPreview preview, List<int>? excludeRowIndexes = null)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        var rowsToImport = preview.Rows
            .Where((r, idx) => r.IsValid && !r.IsDuplicate && !(excludeRowIndexes?.Contains(idx) ?? false))
            .ToList();

        if (rowsToImport.Count == 0)
            return ServiceResult<SalesImportResult>.Fail("No valid rows to import.");

        var importedCount = 0;
        var totalAmount = 0m;

        using var transaction = await _portalDbContext.Database.BeginTransactionAsync();

        try
        {
            foreach (var row in rowsToImport)
            {
                var record = new ExternalSalesRecord
                {
                    BusinessId = businessId,
                    RevenueSourceId = preview.RevenueSourceId,
                    TransactionDate = row.TransactionDate,
                    InvoiceNumber = row.InvoiceNumber?.Trim(),
                    CustomerId = row.CustomerId,
                    NetAmount = row.NetAmount,
                    VatAmount = row.VatAmount,
                    TotalAmount = row.TotalAmount,
                    Description = row.Description?.Trim(),
                    PaymentMethod = row.PaymentMethod?.Trim(),
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                };

                await _repository.InsertAsync(record);
                importedCount++;
                totalAmount += row.TotalAmount;
            }

            await _auditLogRepository.InsertAsync(new AuditLog
            {
                BusinessId = businessId,
                Action = "SalesInvoiceImport",
                TableName = "revenue.ExternalSalesRecord",
                RecordId = $"Batch:{importedCount}",
                NewValues = $"Imported {importedCount} sales records from '{preview.FileName}'. Total: {totalAmount:N2}",
                Timestamp = DateTime.UtcNow
            });

            await transaction.CommitAsync();

            return ServiceResult<SalesImportResult>.Ok(new SalesImportResult
            {
                ImportedCount = importedCount,
                TotalAmount = totalAmount
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // EXTERNAL PLATFORM IMPORT PATH
    // ════════════════════════════════════════════════════════════════════

    public async Task<ServiceResult<SalesImportPreview>> ParseAndPreviewForPlatformAsync(
        Stream fileStream, string fileName, int externalPlatformId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        // Validate platform (tenant-owned + active)
        var platform = await _externalPlatformRepository.GetByIdAndBusinessIdAsync(externalPlatformId, businessId);
        if (platform == null)
            return ServiceResult<SalesImportPreview>.Fail("External platform not found.");
        if (!platform.IsActive)
            return ServiceResult<SalesImportPreview>.Fail("External platform is inactive.");

        // Validate file
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        if (extension != ".csv")
            return ServiceResult<SalesImportPreview>.Fail("Only CSV files are accepted.");
        if (fileStream.Length > 5 * 1024 * 1024)
            return ServiceResult<SalesImportPreview>.Fail("File size exceeds the 5 MB limit.");

        // Enforce the canonical import contract: all required headers must be present.
        var headerError = ValidateCanonicalHeaders(fileStream);
        if (headerError != null)
            return ServiceResult<SalesImportPreview>.Fail(headerError);

        // Parse CSV
        var (rows, parseError) = ParseCsvRows(fileStream);
        if (parseError != null)
            return ServiceResult<SalesImportPreview>.Fail(parseError);
        if (rows.Count == 0)
            return ServiceResult<SalesImportPreview>.Fail("No data rows found in the file.");
        if (rows.Count > 1000)
            return ServiceResult<SalesImportPreview>.Fail("File contains more than 1000 data rows.");

        var expectedPrefix = $"{platform.PlatformCode}-INV-";
        var duplicateCount = 0;

        // Memoize VAT period resolution per distinct date to avoid N queries
        var periodCache = new Dictionary<DateOnly, (int? Id, string Label)>();

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];

            if (row.TransactionDate == default)
            {
                row.IsValid = false;
                row.ValidationError = "Invalid transaction date.";
                continue;
            }
            if (row.NetAmount < 0)
            {
                row.IsValid = false;
                row.ValidationError = "Net amount cannot be negative.";
                continue;
            }
            if (row.VatAmount < 0)
            {
                row.IsValid = false;
                row.ValidationError = "VAT amount cannot be negative.";
                continue;
            }

            // Prefix validation (non-blocking warning)
            if (!string.IsNullOrWhiteSpace(row.InvoiceNumber) &&
                !row.InvoiceNumber.Trim().StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                row.HasPrefixWarning = true;
                row.PrefixWarning = $"Invoice number does not start with \"{expectedPrefix}\".";
            }

            // Duplicate detection (only if invoice number provided)
            if (!string.IsNullOrWhiteSpace(row.InvoiceNumber))
            {
                var isDuplicate = await _repository.ExistsDuplicateByPlatformAsync(
                    businessId, externalPlatformId, row.InvoiceNumber.Trim(), row.TransactionDate);
                if (isDuplicate)
                {
                    row.IsDuplicate = true;
                    duplicateCount++;
                }
                else
                {
                    var otherSource = await _repository.FindCrossSourceOrPlatformDuplicateAsync(
                        businessId, externalPlatformId, row.InvoiceNumber.Trim(), row.TransactionDate);
                    if (otherSource != null)
                    {
                        row.HasCrossSourceWarning = true;
                        row.CrossSourceWarning = $"Same invoice exists under \"{otherSource}\"";
                    }
                }
            }

            // Resolve target VAT period label for preview
            if (!periodCache.TryGetValue(row.TransactionDate, out var resolved))
            {
                var period = await _vatSubmissionPeriodRepository.GetCoveringUnsubmittedPeriodAsync(businessId, row.TransactionDate);
                if (period != null)
                {
                    resolved = (period.Id, string.IsNullOrWhiteSpace(period.PeriodLabel) ? "Assigned" : period.PeriodLabel);
                }
                else
                {
                    // Distinguish "no period" from "submitted-locked" for a clearer preview
                    var anyCovering = await _vatSubmissionPeriodRepository.GetByDateAndBusinessIdAsync(row.TransactionDate, businessId);
                    resolved = anyCovering != null
                        ? ((int?)null, "Locked — period submitted")
                        : ((int?)null, "Unassigned");
                }
                periodCache[row.TransactionDate] = resolved;
            }
            row.TargetPeriodLabel = resolved.Label;
        }

        var validRows = rows.Count(r => r.IsValid && !r.IsDuplicate);
        var batchTotal = rows.Where(r => r.IsValid && !r.IsDuplicate).Sum(r => r.TotalAmount);

        return ServiceResult<SalesImportPreview>.Ok(new SalesImportPreview
        {
            FileName = fileName,
            ExternalPlatformId = platform.Id,
            ExternalPlatformName = platform.Name,
            ExternalPlatformCode = platform.PlatformCode,
            TotalRows = rows.Count,
            ValidRows = validRows,
            DuplicateCount = duplicateCount,
            BatchTotal = batchTotal,
            Rows = rows,
            Errors = new List<string>()
        });
    }

    public async Task<ServiceResult<SalesImportResult>> ConfirmImportForPlatformAsync(
        SalesImportPreview preview, List<int>? excludeRowIndexes = null)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        if (preview.ExternalPlatformId == null)
            return ServiceResult<SalesImportResult>.Fail("No external platform associated with this import.");

        // Re-validate platform ownership at commit time
        var platform = await _externalPlatformRepository.GetByIdAndBusinessIdAsync(preview.ExternalPlatformId.Value, businessId);
        if (platform == null)
            return ServiceResult<SalesImportResult>.Fail("External platform not found.");

        var rowsToImport = preview.Rows
            .Where((r, idx) => r.IsValid && !r.IsDuplicate && !(excludeRowIndexes?.Contains(idx) ?? false))
            .ToList();

        if (rowsToImport.Count == 0)
            return ServiceResult<SalesImportResult>.Fail("No valid rows to import.");

        var importedCount = 0;
        var totalAmount = 0m;

        // Re-resolve VAT periods at commit time (respect periods submitted after preview)
        var periodCache = new Dictionary<DateOnly, int?>();

        using var transaction = await _portalDbContext.Database.BeginTransactionAsync();

        try
        {
            foreach (var row in rowsToImport)
            {
                if (!periodCache.TryGetValue(row.TransactionDate, out var periodId))
                {
                    var period = await _vatSubmissionPeriodRepository.GetCoveringUnsubmittedPeriodAsync(businessId, row.TransactionDate);
                    periodId = period?.Id;
                    periodCache[row.TransactionDate] = periodId;
                }

                var record = new ExternalSalesRecord
                {
                    BusinessId = businessId,
                    RevenueSourceId = null,
                    ExternalPlatformId = preview.ExternalPlatformId,
                    TransactionDate = row.TransactionDate,
                    InvoiceNumber = row.InvoiceNumber?.Trim(),
                    CustomerId = null,
                    NetAmount = row.NetAmount,
                    VatAmount = row.VatAmount,
                    TotalAmount = row.TotalAmount,
                    Description = row.Description?.Trim(),
                    PaymentMethod = row.PaymentMethod?.Trim(),
                    VatSubmissionPeriodId = periodId,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                };

                await _repository.InsertAsync(record);
                importedCount++;
                totalAmount += row.TotalAmount;
            }

            await _auditLogRepository.InsertAsync(new AuditLog
            {
                BusinessId = businessId,
                Action = "ExternalPlatformSalesImport",
                TableName = "revenue.ExternalSalesRecord",
                RecordId = $"Batch:{importedCount}",
                NewValues = $"Imported {importedCount} sales records for platform '{platform.Name}' ({platform.PlatformCode}) from '{preview.FileName}'. Total: {totalAmount:N2}",
                Timestamp = DateTime.UtcNow
            });

            await transaction.CommitAsync();

            return ServiceResult<SalesImportResult>.Ok(new SalesImportResult
            {
                ImportedCount = importedCount,
                TotalAmount = totalAmount
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Validates that the CSV header contains all required canonical-contract columns.
    /// Reads only the header line, then rewinds the stream so ParseCsvRows can re-read from the start.
    /// Returns an error message when a required column is missing, or null when the header is valid.
    /// </summary>
    private static string? ValidateCanonicalHeaders(Stream stream)
    {
        var originalPosition = stream.CanSeek ? stream.Position : 0;

        // leaveOpen: true so we don't dispose the underlying stream
        using (var reader = new StreamReader(stream, leaveOpen: true))
        {
            var headerLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine))
            {
                if (stream.CanSeek) stream.Position = originalPosition;
                return "File is empty.";
            }

            var separator = headerLine.Contains(';') ? ';' : ',';
            var headers = headerLine
                .Split(separator)
                .Select(h => h.Trim().Replace(" ", "").ToLowerInvariant())
                .ToHashSet();

            // Required canonical columns (matched loosely against the parser's accepted aliases)
            var required = new (string Label, string[] Accepted)[]
            {
                ("InvoiceNumber", new[] { "invoicenumber", "invoiceno", "invoice" }),
                ("InvoiceDate",   new[] { "invoicedate", "date", "transactiondate" }),
                ("NetAmount",     new[] { "netamount", "net", "amount" }),
                ("VatAmount",     new[] { "vatamount", "vat", "tax" }),
                ("TotalAmount",   new[] { "totalamount", "total", "gross" })
            };

            var missing = required
                .Where(r => !r.Accepted.Any(a => headers.Contains(a)))
                .Select(r => r.Label)
                .ToList();

            if (stream.CanSeek) stream.Position = originalPosition;

            if (missing.Count > 0)
                return $"The file is missing required column(s): {string.Join(", ", missing)}.";
        }

        return null;
    }

    private (List<SalesImportRow> Rows, string? Error) ParseCsvRows(Stream stream)
    {
        var rows = new List<SalesImportRow>();
        using var reader = new StreamReader(stream);

        var headerLine = reader.ReadLine();
        if (headerLine == null) return (rows, "File is empty.");

        var separator = headerLine.Contains(';') ? ';' : ',';
        var headers = headerLine.Split(separator).Select(h => h.Trim().ToLowerInvariant()).ToArray();

        var dateIdx = FindCol(headers, "date", "transaction date", "transactiondate", "transaction_date");
        var invIdx = FindCol(headers, "invoice no", "invoice number", "invoicenumber", "invoice_number", "invoice");
        var netIdx = FindCol(headers, "net", "net amount", "netamount", "amount");
        var vatIdx = FindCol(headers, "vat", "vat amount", "vatamount", "tax");
        var totalIdx = FindCol(headers, "total", "total amount", "totalamount", "gross");
        var descIdx = FindCol(headers, "description", "desc", "item");
        var payMethodIdx = FindCol(headers, "payment method", "paymentmethod", "payment", "method");
        var customerIdx = FindCol(headers, "customer", "customer id", "customerid", "customer_id");

        if (dateIdx == -1) return (rows, "Required column 'Date' not found in header.");
        if (netIdx == -1 && totalIdx == -1) return (rows, "Required column 'Net' or 'Total' not found in header.");

        int lineNumber = 1;
        while (!reader.EndOfStream)
        {
            lineNumber++;
            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = line.Split(separator);
            var row = new SalesImportRow();

            if (!TryParseDate(GetCol(cols, dateIdx), out var date))
            {
                row.IsValid = false;
                row.ValidationError = $"Row {lineNumber}: Invalid date.";
                rows.Add(row);
                continue;
            }
            row.TransactionDate = date;

            row.InvoiceNumber = GetCol(cols, invIdx);
            row.Description = GetCol(cols, descIdx);
            row.PaymentMethod = GetCol(cols, payMethodIdx);

            // Customer ID (optional — integer only)
            if (customerIdx >= 0)
            {
                var custVal = GetCol(cols, customerIdx);
                if (!string.IsNullOrWhiteSpace(custVal) && int.TryParse(custVal, out var custId))
                    row.CustomerId = custId;
            }

            // Amounts
            TryParseDec(GetCol(cols, netIdx), out var net);
            TryParseDec(GetCol(cols, vatIdx), out var vat);
            TryParseDec(GetCol(cols, totalIdx), out var total);

            row.NetAmount = net;
            row.VatAmount = vat;
            row.TotalAmount = total > 0 ? total : net + vat;

            rows.Add(row);
        }

        return (rows, null);
    }

    private static int FindCol(string[] headers, params string[] candidates)
    {
        for (int i = 0; i < headers.Length; i++)
            foreach (var c in candidates)
                if (headers[i] == c || headers[i].Replace(" ", "") == c.Replace(" ", ""))
                    return i;
        return -1;
    }

    private static string? GetCol(string[] cols, int idx) =>
        idx >= 0 && idx < cols.Length ? cols[idx].Trim() : null;

    private static bool TryParseDate(string? v, out DateOnly result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(v)) return false;
        string[] fmts = { "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd", "d/M/yyyy" };
        foreach (var f in fmts)
            if (DateOnly.TryParseExact(v, f, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                return true;
        return DateOnly.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }

    private static bool TryParseDec(string? v, out decimal result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(v)) return false;
        v = v.Replace(",", ".");
        return decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }
}
