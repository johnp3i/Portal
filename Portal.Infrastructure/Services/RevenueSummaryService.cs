using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for Z-Report manual entry and management.
/// Handles creation with multi-line VAT breakdown, totals computation, duplicate detection, and soft-delete.
/// </summary>
public class RevenueSummaryService : IRevenueSummaryService
{
    private readonly RevenueSummaryRepository _revenueSummaryRepository;
    private readonly RevenueSourceRepository _revenueSourceRepository;
    private readonly VatSubmissionPeriodRepository _vatPeriodRepository;
    private readonly AuditLogRepository _auditLogRepository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly PortalDbContext _portalDbContext;

    public RevenueSummaryService(
        RevenueSummaryRepository revenueSummaryRepository,
        RevenueSourceRepository revenueSourceRepository,
        VatSubmissionPeriodRepository vatPeriodRepository,
        AuditLogRepository auditLogRepository,
        ICurrentTenantService currentTenantService,
        PortalDbContext portalDbContext)
    {
        _revenueSummaryRepository = revenueSummaryRepository;
        _revenueSourceRepository = revenueSourceRepository;
        _vatPeriodRepository = vatPeriodRepository;
        _auditLogRepository = auditLogRepository;
        _currentTenantService = currentTenantService;
        _portalDbContext = portalDbContext;
    }

    public async Task<PagedResult<RevenueSummary>> GetPagedAsync(
        int? revenueSourceId = null,
        DateOnly? dateFrom = null,
        DateOnly? dateTo = null,
        string? zReportNumber = null,
        int page = 1,
        int pageSize = 15,
        string dateMode = "period")
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 15;

        int offset = (page - 1) * pageSize;

        var (items, totalCount) = await _revenueSummaryRepository.GetPagedAsync(
            _currentTenantService.CurrentBusinessId,
            revenueSourceId,
            dateFrom,
            dateTo,
            zReportNumber?.Trim(),
            offset,
            pageSize,
            dateMode,
            includeInactive: true);

        var result = new PagedResult<RevenueSummary>
        {
            Items = items,
            CurrentPage = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        // Clamp to page 1 if requested page exceeds total pages
        if (page > result.TotalPages && result.TotalCount > 0)
        {
            var (clampedItems, _) = await _revenueSummaryRepository.GetPagedAsync(
                _currentTenantService.CurrentBusinessId,
                revenueSourceId,
                dateFrom,
                dateTo,
                zReportNumber?.Trim(),
                0,
                pageSize,
                dateMode,
                includeInactive: true);

            result.Items = clampedItems;
            result.CurrentPage = 1;
        }

        return result;
    }

    public async Task<RevenueSummary?> GetByIdAsync(int id)
    {
        return await _revenueSummaryRepository.GetByIdAndBusinessIdAsync(id, _currentTenantService.CurrentBusinessId);
    }

    public async Task<List<RevenueSummaryLine>> GetLinesAsync(int revenueSummaryId)
    {
        return await _revenueSummaryRepository.GetLinesBySummaryIdAsync(revenueSummaryId);
    }

    public async Task<ServiceResult> CreateAsync(RevenueSummary summary, List<RevenueSummaryLine> lines)
    {
        // Validation
        var validationResult = ValidateSummary(summary, lines);
        if (!validationResult.Success)
            return validationResult;

        // Validate revenue source exists and belongs to business
        var source = await _revenueSourceRepository.GetByIdAndBusinessIdAsync(
            summary.RevenueSourceId, _currentTenantService.CurrentBusinessId);
        if (source == null)
            return ServiceResult.Fail("Revenue source not found.");
        if (!source.IsActive)
            return ServiceResult.Fail("Revenue source is inactive. Please select an active source.");

        // Duplicate detection (advisory warning — only if Z-Report number provided)
        if (!string.IsNullOrWhiteSpace(summary.ZReportNumber))
        {
            var duplicateId = await _revenueSummaryRepository.FindDuplicateAsync(
                _currentTenantService.CurrentBusinessId,
                summary.RevenueSourceId,
                summary.ZReportNumber.Trim());

            if (duplicateId.HasValue)
            {
                return ServiceResult.Fail($"A Z-Report with number '{summary.ZReportNumber.Trim()}' already exists for this revenue source (ID: {duplicateId.Value}).");
            }
        }

        // Compute totals from lines
        ComputeTotals(summary, lines);

        summary.BusinessId = _currentTenantService.CurrentBusinessId;
        summary.IsActive = true;
        summary.CreatedAtUtc = DateTime.UtcNow;

        // VAT Period: date-range fallback if no explicit period selected
        if (!summary.VatSubmissionPeriodId.HasValue)
        {
            var matchedPeriod = await _vatPeriodRepository.GetByDateAndBusinessIdAsync(
                summary.SummaryDate, _currentTenantService.CurrentBusinessId);
            if (matchedPeriod != null)
            {
                // Only auto-assign if the period is not submitted
                var isSubmitted = await _portalDbContext.VatSubmissions
                    .AnyAsync(s => s.VatSubmissionPeriodId == matchedPeriod.Id
                        && s.BusinessId == _currentTenantService.CurrentBusinessId
                        && s.IsSubmitted);
                if (!isSubmitted)
                {
                    summary.VatSubmissionPeriodId = matchedPeriod.Id;
                }
            }
        }
        else
        {
            // Validate the explicitly selected period is not submitted
            var isSubmitted = await _portalDbContext.VatSubmissions
                .AnyAsync(s => s.VatSubmissionPeriodId == summary.VatSubmissionPeriodId.Value
                    && s.BusinessId == _currentTenantService.CurrentBusinessId
                    && s.IsSubmitted);
            if (isSubmitted)
                return ServiceResult.Fail("Cannot assign to a submitted VAT period.");
        }

        // Insert within a transaction
        using var transaction = await _portalDbContext.Database.BeginTransactionAsync();

        try
        {
            var summaryId = await _revenueSummaryRepository.InsertAsync(summary);

            foreach (var line in lines)
            {
                line.RevenueSummaryId = summaryId;
                line.CreatedAtUtc = DateTime.UtcNow;
                await _revenueSummaryRepository.InsertLineAsync(line);
            }

            await _auditLogRepository.InsertAsync(new AuditLog
            {
                BusinessId = _currentTenantService.CurrentBusinessId,
                Action = "Create",
                TableName = "revenue.RevenueSummary",
                RecordId = summaryId.ToString(),
                Timestamp = DateTime.UtcNow
            });

            await transaction.CommitAsync();

            return ServiceResult.Ok(summaryId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ServiceResult> UpdateAsync(RevenueSummary summary, List<RevenueSummaryLine> lines)
    {
        // Validation
        var validationResult = ValidateSummary(summary, lines);
        if (!validationResult.Success)
            return validationResult;

        var existing = await _revenueSummaryRepository.GetByIdAndBusinessIdAsync(
            summary.Id, _currentTenantService.CurrentBusinessId);
        if (existing == null)
            return ServiceResult.Fail("Z-Report not found.");

        // Validate revenue source exists and belongs to business
        var source = await _revenueSourceRepository.GetByIdAndBusinessIdAsync(
            summary.RevenueSourceId, _currentTenantService.CurrentBusinessId);
        if (source == null)
            return ServiceResult.Fail("Revenue source not found.");

        // Duplicate detection (if Z-Report number changed)
        if (!string.IsNullOrWhiteSpace(summary.ZReportNumber))
        {
            var duplicateId = await _revenueSummaryRepository.FindDuplicateAsync(
                _currentTenantService.CurrentBusinessId,
                summary.RevenueSourceId,
                summary.ZReportNumber.Trim());

            if (duplicateId.HasValue && duplicateId.Value != summary.Id)
            {
                return ServiceResult.Fail($"A Z-Report with number '{summary.ZReportNumber.Trim()}' already exists for this revenue source.");
            }
        }

        // Compute totals from lines
        ComputeTotals(summary, lines);

        summary.BusinessId = _currentTenantService.CurrentBusinessId;

        // Check if currently assigned to a submitted period (locked — cannot edit)
        if (existing.VatSubmissionPeriodId.HasValue)
        {
            var isExistingPeriodSubmitted = await _portalDbContext.VatSubmissions
                .AnyAsync(s => s.VatSubmissionPeriodId == existing.VatSubmissionPeriodId.Value
                    && s.BusinessId == _currentTenantService.CurrentBusinessId
                    && s.IsSubmitted);
            if (isExistingPeriodSubmitted)
                return ServiceResult.Fail("Locked — assigned to a submitted VAT period. Cannot edit.");
        }

        // VAT Period: validate new period selection if changed
        if (summary.VatSubmissionPeriodId.HasValue && summary.VatSubmissionPeriodId != existing.VatSubmissionPeriodId)
        {
            var isNewPeriodSubmitted = await _portalDbContext.VatSubmissions
                .AnyAsync(s => s.VatSubmissionPeriodId == summary.VatSubmissionPeriodId.Value
                    && s.BusinessId == _currentTenantService.CurrentBusinessId
                    && s.IsSubmitted);
            if (isNewPeriodSubmitted)
                return ServiceResult.Fail("Cannot assign to a submitted VAT period.");
        }

        // Update within a transaction (delete old lines, re-insert new ones)
        using var transaction = await _portalDbContext.Database.BeginTransactionAsync();

        try
        {
            await _revenueSummaryRepository.UpdateAsync(summary);
            await _revenueSummaryRepository.DeleteLinesBySummaryIdAsync(summary.Id);

            foreach (var line in lines)
            {
                line.RevenueSummaryId = summary.Id;
                line.CreatedAtUtc = DateTime.UtcNow;
                await _revenueSummaryRepository.InsertLineAsync(line);
            }

            await _auditLogRepository.InsertAsync(new AuditLog
            {
                BusinessId = _currentTenantService.CurrentBusinessId,
                Action = "Update",
                TableName = "revenue.RevenueSummary",
                RecordId = summary.Id.ToString(),
                Timestamp = DateTime.UtcNow
            });

            await transaction.CommitAsync();

            return ServiceResult.Ok(summary.Id);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var existing = await _revenueSummaryRepository.GetByIdAndBusinessIdAsync(
            id, _currentTenantService.CurrentBusinessId);
        if (existing == null)
            return ServiceResult.Fail("Z-Report not found.");

        await _revenueSummaryRepository.SoftDeleteAsync(id, _currentTenantService.CurrentBusinessId);

        await _auditLogRepository.InsertAsync(new AuditLog
        {
            BusinessId = _currentTenantService.CurrentBusinessId,
            Action = "Delete",
            TableName = "revenue.RevenueSummary",
            RecordId = id.ToString(),
            Timestamp = DateTime.UtcNow
        });

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RestoreAsync(int id)
    {
        var existing = await _revenueSummaryRepository.GetByIdAndBusinessIdAsync(
            id, _currentTenantService.CurrentBusinessId);
        if (existing == null)
            return ServiceResult.Fail("Z-Report not found.");

        if (existing.IsActive)
            return ServiceResult.Fail("Z-Report is already active.");

        await _revenueSummaryRepository.RestoreAsync(id, _currentTenantService.CurrentBusinessId);

        await _auditLogRepository.InsertAsync(new AuditLog
        {
            BusinessId = _currentTenantService.CurrentBusinessId,
            Action = "Restore",
            TableName = "revenue.RevenueSummary",
            RecordId = id.ToString(),
            Timestamp = DateTime.UtcNow
        });

        return ServiceResult.Ok();
    }

    public async Task<bool> IsLockedAsync(int id)
    {
        var existing = await _revenueSummaryRepository.GetByIdAndBusinessIdAsync(
            id, _currentTenantService.CurrentBusinessId);
        if (existing == null || !existing.VatSubmissionPeriodId.HasValue)
            return false;

        return await _portalDbContext.VatSubmissions
            .AnyAsync(s => s.VatSubmissionPeriodId == existing.VatSubmissionPeriodId.Value
                && s.BusinessId == _currentTenantService.CurrentBusinessId
                && s.IsSubmitted);
    }

    /// <summary>
    /// Validates the summary and its lines before persist.
    /// </summary>
    private ServiceResult ValidateSummary(RevenueSummary summary, List<RevenueSummaryLine> lines)
    {
        if (summary.RevenueSourceId <= 0)
            return ServiceResult.Fail("Revenue source is required.");

        if (summary.SummaryDate == default)
            return ServiceResult.Fail("Period start date is required.");

        if (summary.PeriodEndDate.HasValue && summary.PeriodEndDate.Value < summary.SummaryDate)
            return ServiceResult.Fail("Period end date cannot be before start date.");

        if (lines == null || lines.Count == 0)
            return ServiceResult.Fail("At least one VAT line is required.");

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.VatRate < 0 || line.VatRate > 100)
                return ServiceResult.Fail($"VAT line {i + 1}: VAT rate must be between 0 and 100.");

            if (line.NetAmount < 0)
                return ServiceResult.Fail($"VAT line {i + 1}: Net amount cannot be negative.");

            if (line.VatAmount < 0)
                return ServiceResult.Fail($"VAT line {i + 1}: VAT amount cannot be negative.");
        }

        // Check for duplicate VAT rates
        var duplicateRates = lines
            .GroupBy(l => l.VatRate)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateRates.Any())
            return ServiceResult.Fail($"Duplicate VAT rate(s) found: {string.Join(", ", duplicateRates.Select(r => $"{r}%"))}. Each VAT rate can only appear once per Z-Report.");

        return ServiceResult.Ok();
    }

    /// <summary>
    /// Computes header totals from the individual VAT lines.
    /// </summary>
    private void ComputeTotals(RevenueSummary summary, List<RevenueSummaryLine> lines)
    {
        foreach (var line in lines)
        {
            line.TotalAmount = line.NetAmount + line.VatAmount;
        }

        summary.TotalNet = lines.Sum(l => l.NetAmount);
        summary.TotalVat = lines.Sum(l => l.VatAmount);
        summary.TotalGross = lines.Sum(l => l.TotalAmount);
        summary.TotalDiscount = lines.Where(l => l.DiscountAmount.HasValue).Sum(l => l.DiscountAmount!.Value);

        if (summary.TotalDiscount == 0)
            summary.TotalDiscount = null;
    }
}
