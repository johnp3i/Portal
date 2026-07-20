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
    private readonly AuditLogRepository _auditLogRepository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly PortalDbContext _portalDbContext;

    public SalesImportService(
        ExternalSalesRecordRepository repository,
        RevenueSourceRepository revenueSourceRepository,
        AuditLogRepository auditLogRepository,
        ICurrentTenantService currentTenantService,
        PortalDbContext portalDbContext)
    {
        _repository = repository;
        _revenueSourceRepository = revenueSourceRepository;
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
