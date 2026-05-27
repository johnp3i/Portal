using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for VAT submission management including computation of output/input VAT,
/// submission creation/recalculation, and filing status tracking.
/// </summary>
public class VatSubmissionService : IVatSubmissionService
{
    private readonly ICurrentTenantService _currentTenantService;
    private readonly VatSubmissionRepository _vatSubmissionRepository;
    private readonly VatSubmissionPeriodRepository _vatSubmissionPeriodRepository;
    private readonly PortalDbContext _portalDbContext;
    private readonly AuditLogRepository _auditLogRepository;

    public VatSubmissionService(
        ICurrentTenantService currentTenantService,
        VatSubmissionRepository vatSubmissionRepository,
        VatSubmissionPeriodRepository vatSubmissionPeriodRepository,
        PortalDbContext portalDbContext,
        AuditLogRepository auditLogRepository)
    {
        _currentTenantService = currentTenantService;
        _vatSubmissionRepository = vatSubmissionRepository;
        _vatSubmissionPeriodRepository = vatSubmissionPeriodRepository;
        _portalDbContext = portalDbContext;
        _auditLogRepository = auditLogRepository;
    }

    /// <inheritdoc />
    public async Task<ServiceResult<VatSubmission>> CreateOrRecalculateAsync(int vatSubmissionPeriodId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        // Validate period belongs to current tenant
        var period = await _vatSubmissionPeriodRepository.GetByIdAndBusinessIdAsync(vatSubmissionPeriodId, businessId);
        if (period == null)
        {
            return ServiceResult<VatSubmission>.Fail("The specified period does not belong to your business.");
        }

        // Check if submission already exists for this period
        var existingSubmission = await _vatSubmissionRepository.GetByPeriodIdAndBusinessIdAsync(vatSubmissionPeriodId, businessId);

        // If submission exists and is already submitted, return it as-is (immutable once submitted)
        if (existingSubmission != null && existingSubmission.IsSubmitted)
        {
            return ServiceResult<VatSubmission>.Ok(existingSubmission);
        }

        // Compute TotalOutputVat using two-part approach:
        // Part 1: Invoices explicitly assigned to this period
        var explicitOutputVat = await _portalDbContext.Invoices
            .Where(i => i.BusinessId == businessId
                && i.InvoiceStatusTypeId == 2
                && !i.IsDeleted
                && i.VatSubmissionPeriodId == vatSubmissionPeriodId)
            .SumAsync(i => (decimal?)i.TaxAmount) ?? 0m;

        // Part 2: Invoices with NULL assignment falling in date range (backward compat)
        var dateRangeOutputVat = await _portalDbContext.Invoices
            .Where(i => i.BusinessId == businessId
                && i.InvoiceStatusTypeId == 2
                && !i.IsDeleted
                && i.VatSubmissionPeriodId == null
                && i.InvoiceDate >= period.PeriodStartDate
                && i.InvoiceDate <= period.PeriodEndDate)
            .SumAsync(i => (decimal?)i.TaxAmount) ?? 0m;

        // Subtract credit note TaxAmount from Output VAT (only Issued or Applied credit notes)
        var creditNoteTaxReduction = await _portalDbContext.CreditNotes
            .Where(cn => cn.BusinessId == businessId
                && cn.VatSubmissionPeriodId == vatSubmissionPeriodId
                && (cn.CreditNoteStatusTypeId == 2 || cn.CreditNoteStatusTypeId == 3)) // Issued or Applied
            .SumAsync(cn => (decimal?)cn.TaxAmount) ?? 0m;

        var totalOutputVat = explicitOutputVat + dateRangeOutputVat - creditNoteTaxReduction;

        // Compute TotalInputVat: SUM(VatAmount) from purchases assigned to this period (VatSubmissionPeriodId)
        // This is the "actual" figure — what was reported for this period
        var totalInputVat = await _portalDbContext.Purchases
            .Where(p => p.BusinessId == businessId
                && p.PurchaseOriginTypeId != 2
                && !p.IsCancelled
                && p.VatSubmissionPeriodId == vatSubmissionPeriodId)
            .SumAsync(p => (decimal?)p.VatAmount) ?? 0m;

        // Also compute by InvoiceDate for discrepancy detection
        // This is the "expected" figure — what should have been reported based on invoice dates
        var inputVatByDate = await _portalDbContext.Purchases
            .Where(p => p.BusinessId == businessId
                && p.PurchaseOriginTypeId != 2
                && !p.IsCancelled
                && p.InvoiceDate >= period.PeriodStartDate
                && p.InvoiceDate <= period.PeriodEndDate)
            .SumAsync(p => (decimal?)p.VatAmount) ?? 0m;

        // Compute NetVatPayable = TotalOutputVat - TotalInputVat
        var netVatPayable = totalOutputVat - totalInputVat;

        if (existingSubmission != null)
        {
            // Update existing submission values
            existingSubmission.TotalOutputVat = totalOutputVat;
            existingSubmission.TotalInputVat = totalInputVat;
            existingSubmission.NetVatPayable = netVatPayable;

            await _vatSubmissionRepository.UpdateValuesAsync(existingSubmission);

            // Write audit log entry (Recalculated)
            await _auditLogRepository.InsertAsync(new AuditLog
            {
                BusinessId = businessId,
                Action = "Recalculated",
                TableName = "VatSubmission",
                RecordId = existingSubmission.Id.ToString(),
                NewValues = $"TotalOutputVat={totalOutputVat}, TotalInputVat={totalInputVat}, NetVatPayable={netVatPayable}",
                Timestamp = DateTime.UtcNow
            });

            return ServiceResult<VatSubmission>.Ok(existingSubmission);
        }
        else
        {
            // Insert new submission
            var submission = new VatSubmission
            {
                BusinessId = businessId,
                VatSubmissionPeriodId = vatSubmissionPeriodId,
                TotalOutputVat = totalOutputVat,
                TotalInputVat = totalInputVat,
                NetVatPayable = netVatPayable,
                IsSubmitted = false,
                SubmittedAtUtc = null,
                Notes = null,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _vatSubmissionRepository.InsertAsync(submission);

            // Write audit log entry (Created)
            await _auditLogRepository.InsertAsync(new AuditLog
            {
                BusinessId = businessId,
                Action = "Created",
                TableName = "VatSubmission",
                RecordId = submission.Id.ToString(),
                NewValues = $"TotalOutputVat={totalOutputVat}, TotalInputVat={totalInputVat}, NetVatPayable={netVatPayable}",
                Timestamp = DateTime.UtcNow
            });

            return ServiceResult<VatSubmission>.Ok(submission);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> MarkAsSubmittedAsync(int vatSubmissionId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        // Validate submission exists and belongs to tenant
        var submission = await _vatSubmissionRepository.GetByIdAndBusinessIdAsync(vatSubmissionId, businessId);
        if (submission == null)
        {
            return ServiceResult.Fail("Submission not found.");
        }

        // Reject if already submitted
        if (submission.IsSubmitted)
        {
            return ServiceResult.Fail("This submission has already been marked as submitted.");
        }

        // Set IsSubmitted=true, SubmittedAtUtc=DateTime.UtcNow
        await _vatSubmissionRepository.MarkAsSubmittedAsync(vatSubmissionId, businessId);

        // Write audit log entry (MarkedAsSubmitted)
        await _auditLogRepository.InsertAsync(new AuditLog
        {
            BusinessId = businessId,
            Action = "MarkedAsSubmitted",
            TableName = "VatSubmission",
            RecordId = vatSubmissionId.ToString(),
            NewValues = $"IsSubmitted=true, SubmittedAtUtc={DateTime.UtcNow:O}",
            Timestamp = DateTime.UtcNow
        });

        return ServiceResult.Ok();
    }

    /// <inheritdoc />
    public async Task<VatSubmission?> GetByPeriodIdAsync(int vatSubmissionPeriodId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;
        return await _vatSubmissionRepository.GetByPeriodIdAndBusinessIdAsync(vatSubmissionPeriodId, businessId);
    }
}
