using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Entities.Import;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Import;
using Portal.Infrastructure.Repositories.Import;

namespace Portal.Infrastructure.Services.Import;

/// <summary>
/// Orchestrates file parsing, validation, duplicate detection, session management,
/// and bulk import confirmation with transactional guarantees.
/// </summary>
public class ImportEngineService : IImportEngineService
{
    private const int MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    private const int MaxDataRows = 500;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csv", ".xlsx", ".xls"
    };

    private readonly IFileParsingService _fileParsingService;
    private readonly IImportValidationService _validationService;
    private readonly IDuplicateDetectionService _duplicateDetectionService;
    private readonly IParserTemplateService _templateService;
    private readonly ImportSessionRepository _sessionRepository;
    private readonly PortalDbContext _dbContext;

    public ImportEngineService(
        IFileParsingService fileParsingService,
        IImportValidationService validationService,
        IDuplicateDetectionService duplicateDetectionService,
        IParserTemplateService templateService,
        ImportSessionRepository sessionRepository,
        PortalDbContext dbContext)
    {
        _fileParsingService = fileParsingService;
        _validationService = validationService;
        _duplicateDetectionService = duplicateDetectionService;
        _templateService = templateService;
        _sessionRepository = sessionRepository;
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<ImportSessionResult>> ParseFileAsync(
        Stream fileStream, string fileName, int supplierId, int? templateId, int businessId)
    {
        try
        {
            // Validate file extension
            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            {
                return ServiceResult<ImportSessionResult>.Fail("Only CSV, XLSX, and XLS files are accepted.");
            }

            // Validate file size
            if (fileStream.Length > MaxFileSizeBytes)
            {
                return ServiceResult<ImportSessionResult>.Fail("File size exceeds the 5 MB limit.");
            }

            // Parse file
            List<ParsedRow> parsedRows;

            if (templateId.HasValue)
            {
                var template = await _templateService.GetTemplateByIdAsync(templateId.Value, businessId);
                if (template == null)
                    return ServiceResult<ImportSessionResult>.Fail("Parser template not found.");

                parsedRows = extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)
                    ? _fileParsingService.ParseCsv(fileStream, template)
                    : _fileParsingService.ParseExcel(fileStream, template);
            }
            else
            {
                // Try auto-detection
                parsedRows = _fileParsingService.AutoDetectAndParse(fileStream, extension);
                if (parsedRows.Count == 0)
                {
                    return ServiceResult<ImportSessionResult>.Fail(
                        "Could not auto-detect column mappings. Please select or create a parser template.");
                }
            }

            // Validate row count
            if (parsedRows.Count > MaxDataRows)
            {
                return ServiceResult<ImportSessionResult>.Fail($"File contains more than {MaxDataRows} data rows.");
            }

            if (parsedRows.Count == 0)
            {
                return ServiceResult<ImportSessionResult>.Fail("No data rows found in the file.");
            }

            // Validate rows
            var validatedRows = await _validationService.ValidateRowsAsync(parsedRows, supplierId, businessId);

            // Check duplicates
            var duplicateResults = await _duplicateDetectionService.CheckDuplicatesAsync(validatedRows, supplierId, businessId);
            foreach (var dup in duplicateResults.Where(d => d.IsDuplicate))
            {
                validatedRows[dup.RowIndex].IsDuplicate = true;
                if (validatedRows[dup.RowIndex].Status != RowValidationStatus.Invalid)
                {
                    validatedRows[dup.RowIndex].Status = RowValidationStatus.Warning;
                }
                validatedRows[dup.RowIndex].Warnings.Add("Potential duplicate — a matching purchase already exists.");
            }

            // Compute summary
            var validCount = validatedRows.Count(r => r.Status != RowValidationStatus.Invalid);
            var invalidCount = validatedRows.Count(r => r.Status == RowValidationStatus.Invalid);
            var warningCount = validatedRows.Count(r => r.Status == RowValidationStatus.Warning);
            var batchTotal = validatedRows
                .Where(r => r.Status != RowValidationStatus.Invalid && r.Data.TotalAmount.HasValue)
                .Sum(r => r.Data.TotalAmount!.Value);

            // Persist session
            var session = new ImportSession
            {
                BusinessId = businessId,
                SupplierId = supplierId,
                ParserTemplateId = templateId,
                FileName = fileName,
                TotalRows = validatedRows.Count,
                ValidRows = validCount,
                InvalidRows = invalidCount,
                RowDataJson = JsonSerializer.Serialize(validatedRows),
                IsConfirmed = false,
                CreatedAtUtc = DateTime.UtcNow
            };

            var sessionId = await _sessionRepository.CreateSessionAsync(session);

            return ServiceResult<ImportSessionResult>.Ok(new ImportSessionResult
            {
                SessionId = sessionId,
                TotalRows = validatedRows.Count,
                ValidRows = validCount,
                InvalidRows = invalidCount,
                WarningRows = warningCount,
                BatchTotal = batchTotal,
                Rows = validatedRows
            });
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult<ImportSessionResult>> RevalidateRowAsync(
        int sessionId, int rowIndex, string field, string value, int businessId, int supplierId)
    {
        try
        {
            var session = await _sessionRepository.GetByIdAsync(sessionId, businessId);
            if (session == null)
                return ServiceResult<ImportSessionResult>.Fail("Import session not found.");

            var rows = JsonSerializer.Deserialize<List<ValidatedRow>>(session.RowDataJson) ?? new();
            if (rowIndex < 0 || rowIndex >= rows.Count)
                return ServiceResult<ImportSessionResult>.Fail("Invalid row index.");

            // Update the field value on the parsed row
            var parsedRow = rows[rowIndex].Data;
            UpdateFieldValue(parsedRow, field, value);

            // Re-validate the single row
            var revalidated = await _validationService.ValidateRowAsync(parsedRow, supplierId, businessId);
            revalidated.IsDuplicate = rows[rowIndex].IsDuplicate;
            revalidated.IsRemoved = rows[rowIndex].IsRemoved;
            rows[rowIndex] = revalidated;

            // Recalculate counts
            var activeRows = rows.Where(r => !r.IsRemoved).ToList();
            var validCount = activeRows.Count(r => r.Status != RowValidationStatus.Invalid);
            var invalidCount = activeRows.Count(r => r.Status == RowValidationStatus.Invalid);
            var batchTotal = activeRows
                .Where(r => r.Status != RowValidationStatus.Invalid && r.Data.TotalAmount.HasValue)
                .Sum(r => r.Data.TotalAmount!.Value);

            // Persist
            await _sessionRepository.UpdateRowDataAsync(sessionId, businessId,
                JsonSerializer.Serialize(rows), validCount, invalidCount, activeRows.Count);

            return ServiceResult<ImportSessionResult>.Ok(new ImportSessionResult
            {
                SessionId = sessionId,
                TotalRows = activeRows.Count,
                ValidRows = validCount,
                InvalidRows = invalidCount,
                WarningRows = activeRows.Count(r => r.Status == RowValidationStatus.Warning),
                BatchTotal = batchTotal,
                Rows = rows
            });
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> RemoveRowAsync(int sessionId, int rowIndex, int businessId)
    {
        try
        {
            var session = await _sessionRepository.GetByIdAsync(sessionId, businessId);
            if (session == null)
                return ServiceResult.Fail("Import session not found.");

            var rows = JsonSerializer.Deserialize<List<ValidatedRow>>(session.RowDataJson) ?? new();
            if (rowIndex < 0 || rowIndex >= rows.Count)
                return ServiceResult.Fail("Invalid row index.");

            rows[rowIndex].IsRemoved = true;

            // Recalculate counts
            var activeRows = rows.Where(r => !r.IsRemoved).ToList();
            var validCount = activeRows.Count(r => r.Status != RowValidationStatus.Invalid);
            var invalidCount = activeRows.Count(r => r.Status == RowValidationStatus.Invalid);

            await _sessionRepository.UpdateRowDataAsync(sessionId, businessId,
                JsonSerializer.Serialize(rows), validCount, invalidCount, activeRows.Count);

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult<ImportConfirmationResult>> ConfirmImportAsync(
        int sessionId, int businessId, string userId)
    {
        try
        {
            var session = await _sessionRepository.GetByIdAsync(sessionId, businessId);
            if (session == null)
                return ServiceResult<ImportConfirmationResult>.Fail("Import session not found.");

            var rows = JsonSerializer.Deserialize<List<ValidatedRow>>(session.RowDataJson) ?? new();
            var importableRows = rows
                .Where(r => !r.IsRemoved && r.Status != RowValidationStatus.Invalid && r.Data.ExpenseCategoryId.HasValue)
                .ToList();

            if (importableRows.Count == 0)
                return ServiceResult<ImportConfirmationResult>.Fail("No valid rows to import.");

            var now = DateTime.UtcNow;
            var totalAmount = 0m;

            // Execute in a transaction
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                foreach (var row in importableRows)
                {
                    var data = row.Data;

                    const string insertQuery = @"
                        INSERT INTO [purchase].[Purchase]
                            ([BusinessId], [SupplierId], [ExpenseCategoryId], [PurchaseOriginTypeId], [PurchaseTypeId],
                             [InvoiceNumber], [InvoiceDate], [Description],
                             [AmountExcludingVat], [VatAmount], [TotalAmount],
                             [Country], [Notes], [VatSubmissionPeriodId], [CreatedAtUtc], [UpdatedAtUtc])
                        VALUES
                            (@BusinessId, @SupplierId, @ExpenseCategoryId, @PurchaseOriginTypeId, @PurchaseTypeId,
                             @InvoiceNumber, @InvoiceDate, @Description,
                             @AmountExcludingVat, @VatAmount, @TotalAmount,
                             @Country, @Notes, @VatSubmissionPeriodId, @CreatedAtUtc, @UpdatedAtUtc)";

                    await _dbContext.Database.ExecuteSqlRawAsync(insertQuery,
                        new SqlParameter("@BusinessId", businessId),
                        new SqlParameter("@SupplierId", session.SupplierId),
                        new SqlParameter("@ExpenseCategoryId", System.Data.SqlDbType.Int) { Value = data.ExpenseCategoryId.HasValue ? data.ExpenseCategoryId.Value : DBNull.Value },
                        new SqlParameter("@PurchaseOriginTypeId", data.PurchaseOriginTypeId ?? 1),
                        new SqlParameter("@PurchaseTypeId", 3), // Expense by default
                        new SqlParameter("@InvoiceNumber", System.Data.SqlDbType.NVarChar, 100) { Value = (object?)data.InvoiceNumber ?? DBNull.Value },
                        new SqlParameter("@InvoiceDate", data.InvoiceDate!.Value.ToDateTime(TimeOnly.MinValue)),
                        new SqlParameter("@Description", System.Data.SqlDbType.NVarChar, 500) { Value = (object?)data.Description ?? DBNull.Value },
                        new SqlParameter("@AmountExcludingVat", data.AmountExcludingVat ?? 0m),
                        new SqlParameter("@VatAmount", data.VatAmount ?? 0m),
                        new SqlParameter("@TotalAmount", data.TotalAmount ?? 0m),
                        new SqlParameter("@Country", System.Data.SqlDbType.NVarChar, 100) { Value = (object?)data.Country ?? DBNull.Value },
                        new SqlParameter("@Notes", System.Data.SqlDbType.NVarChar, 1000) { Value = (object?)data.Notes ?? DBNull.Value },
                        new SqlParameter("@VatSubmissionPeriodId", System.Data.SqlDbType.Int) { Value = data.VatSubmissionPeriodId.HasValue ? data.VatSubmissionPeriodId.Value : DBNull.Value },
                        new SqlParameter("@CreatedAtUtc", now),
                        new SqlParameter("@UpdatedAtUtc", now));

                    totalAmount += data.TotalAmount ?? 0m;
                }

                // Audit log
                const string auditQuery = @"
                    INSERT INTO [audit].[AuditLog]
                        ([BusinessId], [UserId], [Action], [TableName], [RecordId], [NewValues], [Timestamp])
                    VALUES
                        (@BusinessId, @UserId, @Action, @TableName, @RecordId, @NewValues, @Timestamp)";

                await _dbContext.Database.ExecuteSqlRawAsync(auditQuery,
                    new SqlParameter("@BusinessId", businessId),
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@Action", "PurchaseImport"),
                    new SqlParameter("@TableName", "Purchase"),
                    new SqlParameter("@RecordId", $"Batch:{importableRows.Count}"),
                    new SqlParameter("@NewValues", $"Imported {importableRows.Count} purchases from '{session.FileName}'. Total: {totalAmount:N2}"),
                    new SqlParameter("@Timestamp", now));

                await transaction.CommitAsync();

                // Delete the session after successful import
                await _sessionRepository.DeleteAsync(sessionId, businessId);

                return ServiceResult<ImportConfirmationResult>.Ok(new ImportConfirmationResult
                {
                    ImportedCount = importableRows.Count,
                    TotalAmount = totalAmount
                });
            }
            catch
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

    private static void UpdateFieldValue(ParsedRow row, string field, string value)
    {
        switch (field)
        {
            case ImportTargetFields.InvoiceDate:
                if (DateOnly.TryParse(value, out var date))
                    row.InvoiceDate = date;
                break;
            case ImportTargetFields.InvoiceNumber:
                row.InvoiceNumber = value;
                break;
            case ImportTargetFields.Description:
                row.Description = value;
                break;
            case ImportTargetFields.AmountExcludingVat:
                if (decimal.TryParse(value, out var excl))
                    row.AmountExcludingVat = excl;
                break;
            case ImportTargetFields.VatAmount:
                if (decimal.TryParse(value, out var vat))
                    row.VatAmount = vat;
                break;
            case ImportTargetFields.TotalAmount:
                if (decimal.TryParse(value, out var total))
                    row.TotalAmount = total;
                break;
            case ImportTargetFields.Country:
                row.Country = value;
                break;
            case ImportTargetFields.Notes:
                row.Notes = value;
                break;
            case ImportTargetFields.PurchaseOriginType:
                row.PurchaseOriginTypeName = value;
                row.PurchaseOriginTypeId = null; // Will be re-resolved during validation
                break;
            case "ExpenseCategory":
                row.ExpenseCategoryName = value;
                row.ExpenseCategoryId = null; // Will be re-resolved during validation
                break;
        }
    }
}
