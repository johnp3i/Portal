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

        // Compute the figures in-memory (no persistence) via the shared helper.
        var figures = await ComputeSubmissionFiguresAsync(businessId, period);
        var totalOutputVat = figures.TotalOutputVat;
        var totalInputVat = figures.TotalInputVat;
        var netVatPayable = figures.NetVatPayable;

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

    private const string StatusPass = "pass";
    private const string StatusWarning = "warning";
    private const string StatusInfo = "info";

    // Threshold at or above which a purchase-count drop vs the prior period is flagged.
    private const decimal PurchaseDropWarningThreshold = 0.33m;

    // Minimum prior-period purchase count before the drop check is meaningful.
    // Below this, a single missing purchase produces a large % swing and false alarms,
    // so low-volume periods are reported as Pass rather than Warning.
    private const int PurchaseTrendMinBaseline = 5;

    /// <inheritdoc />
    public async Task<ServiceResult<VatPreSubmissionChecklistDto>> GetPreSubmissionChecklistAsync(int vatSubmissionPeriodId)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;

            var period = await _vatSubmissionPeriodRepository.GetByIdAndBusinessIdAsync(vatSubmissionPeriodId, businessId);
            if (period == null)
            {
                return ServiceResult<VatPreSubmissionChecklistDto>.Fail("The specified period does not belong to your business.");
            }

            // Figures source. Prefer the persisted submission (the values the Detail page shows).
            // If no submission row exists yet, compute the figures IN-MEMORY via the shared helper
            // rather than calling CreateOrRecalculateAsync — this endpoint is a read-only AxGet and
            // must NOT create a VatSubmission row or write an audit-log entry.
            var persisted = await _vatSubmissionRepository.GetByPeriodIdAndBusinessIdAsync(vatSubmissionPeriodId, businessId);
            decimal totalOutputVat, totalInputVat, netVatPayable;
            bool isSubmitted;
            if (persisted != null)
            {
                totalOutputVat = persisted.TotalOutputVat;
                totalInputVat = persisted.TotalInputVat;
                netVatPayable = persisted.NetVatPayable;
                isSubmitted = persisted.IsSubmitted;
            }
            else
            {
                var figures = await ComputeSubmissionFiguresAsync(businessId, period);
                totalOutputVat = figures.TotalOutputVat;
                totalInputVat = figures.TotalInputVat;
                netVatPayable = figures.NetVatPayable;
                isSubmitted = false;
            }

            var profile = await _portalDbContext.BusinessProfiles
                .FirstOrDefaultAsync(bp => bp.BusinessId == businessId);
            var currencySymbol = profile?.CurrencySymbol ?? "€";

            var start = period.PeriodStartDate;
            var end = period.PeriodEndDate;

            var items = new List<VatChecklistItemDto>();

            // ── Actionable checks ────────────────────────────────────────────────

            // Unassigned purchases in the period date range (same predicate as VatController.Index)
            var unassignedPurchases = await _portalDbContext.Purchases
                .CountAsync(p => p.BusinessId == businessId
                    && p.VatSubmissionPeriodId == null
                    && !p.IsCancelled
                    && p.InvoiceDate >= start
                    && p.InvoiceDate <= end);

            items.Add(new VatChecklistItemDto
            {
                Key = "unassigned_purchases",
                Status = unassignedPurchases > 0 ? StatusWarning : StatusPass,
                Title = "Unassigned purchases",
                Detail = unassignedPurchases > 0
                    ? $"{unassignedPurchases} purchase(s) in this period's date range are not assigned to a VAT period. They are still included via the date range, but assigning them keeps your records explicit."
                    : "All purchases in this period are assigned. Nothing to review."
            });

            // Unassigned issued invoices dated in the period
            var unassignedInvoices = await _portalDbContext.Invoices
                .CountAsync(i => i.BusinessId == businessId
                    && i.InvoiceStatusTypeId == 2
                    && !i.IsDeleted
                    && i.VatSubmissionPeriodId == null
                    && i.InvoiceDate >= start
                    && i.InvoiceDate <= end);

            items.Add(new VatChecklistItemDto
            {
                Key = "unassigned_invoices",
                Status = unassignedInvoices > 0 ? StatusWarning : StatusPass,
                Title = "Unassigned issued invoices",
                Detail = unassignedInvoices > 0
                    ? $"{unassignedInvoices} issued invoice(s) dated in this period have no explicit VAT period. They are included via the date-range fallback."
                    : "Every issued invoice in this period is explicitly assigned."
            });

            // Zero-VAT issued invoices belonging to this period (explicit or date-range)
            var zeroVatInvoices = await _portalDbContext.Invoices
                .CountAsync(i => i.BusinessId == businessId
                    && i.InvoiceStatusTypeId == 2
                    && !i.IsDeleted
                    && i.TaxAmount == 0m
                    && i.Subtotal > 0m
                    && (i.VatSubmissionPeriodId == vatSubmissionPeriodId
                        || (i.VatSubmissionPeriodId == null && i.InvoiceDate >= start && i.InvoiceDate <= end)));

            items.Add(new VatChecklistItemDto
            {
                Key = "zero_vat_invoices",
                Status = zeroVatInvoices > 0 ? StatusInfo : StatusPass,
                Title = "Zero-VAT invoices",
                Detail = zeroVatInvoices > 0
                    ? $"{zeroVatInvoices} issued invoice(s) have a subtotal but {currencySymbol}0.00 VAT. Review whether VAT should apply, or confirm the zero-rating is intentional."
                    : "No zero-VAT issued invoices with a positive subtotal."
            });

            // Purchase count vs the immediately preceding period
            var currentPurchaseCount = await CountPurchasesForPeriodAsync(businessId, vatSubmissionPeriodId, start, end);
            var priorPeriod = await _vatSubmissionPeriodRepository.GetImmediatelyPrecedingPeriodAsync(businessId, start);

            if (priorPeriod == null)
            {
                items.Add(new VatChecklistItemDto
                {
                    Key = "purchase_count_trend",
                    Status = StatusInfo,
                    Title = "Purchase count vs previous period",
                    Detail = $"This period: {currentPurchaseCount} purchase(s). No prior period is available for comparison."
                });
            }
            else
            {
                var priorPurchaseCount = await CountPurchasesForPeriodAsync(businessId, priorPeriod.Id, priorPeriod.PeriodStartDate, priorPeriod.PeriodEndDate);
                // Only flag a drop when the prior period had a meaningful baseline —
                // otherwise a single missing purchase produces a noisy false alarm.
                var isSignificantDrop = priorPurchaseCount >= PurchaseTrendMinBaseline
                    && (decimal)(priorPurchaseCount - currentPurchaseCount) / priorPurchaseCount >= PurchaseDropWarningThreshold;

                if (isSignificantDrop)
                {
                    var dropPct = (int)Math.Round((decimal)(priorPurchaseCount - currentPurchaseCount) / priorPurchaseCount * 100);
                    items.Add(new VatChecklistItemDto
                    {
                        Key = "purchase_count_trend",
                        Status = StatusWarning,
                        Title = "Purchase count vs previous period",
                        Detail = $"This period: {currentPurchaseCount} purchase(s). Previous period: {priorPurchaseCount} purchase(s). {dropPct}% fewer — is this expected, or are some expenses not yet recorded?"
                    });
                }
                else
                {
                    items.Add(new VatChecklistItemDto
                    {
                        Key = "purchase_count_trend",
                        Status = StatusPass,
                        Title = "Purchase count vs previous period",
                        Detail = $"This period: {currentPurchaseCount} purchase(s). Previous period: {priorPurchaseCount} purchase(s). Consistent."
                    });
                }
            }

            // Input VAT discrepancy (by invoice date vs reported/assignment)
            var inputVatByDate = await ComputeInputVatByDateAsync(businessId, start, end);
            var latePurchasesIncluded = await CountLatePurchasesAsync(businessId, vatSubmissionPeriodId, start, end);
            var purchasesReportedLater = await CountPurchasesReportedLaterAsync(businessId, vatSubmissionPeriodId, start, end);

            var hasDiscrepancy = totalInputVat != inputVatByDate;
            items.Add(new VatChecklistItemDto
            {
                Key = "input_vat_discrepancy",
                Status = hasDiscrepancy ? StatusWarning : StatusPass,
                Title = "Input VAT discrepancy",
                Detail = hasDiscrepancy
                    ? $"Input VAT by invoice date ({currencySymbol}{inputVatByDate:N2}) differs from the reported figure ({currencySymbol}{totalInputVat:N2}). {latePurchasesIncluded} late purchase(s) from a previous period included here; {purchasesReportedLater} purchase(s) from this period reported later."
                    : "Input VAT by date matches the reported figure. No discrepancy."
            });

            // ── Computed figures (always Pass) ───────────────────────────────────
            items.Add(new VatChecklistItemDto
            {
                Key = "output_vat",
                Status = StatusPass,
                Title = "Output VAT (Sales)",
                Detail = $"{currencySymbol}{totalOutputVat:N2}"
            });
            items.Add(new VatChecklistItemDto
            {
                Key = "input_vat",
                Status = StatusPass,
                Title = "Input VAT (Purchases)",
                Detail = $"{currencySymbol}{totalInputVat:N2}"
            });

            var netLabel = netVatPayable > 0 ? "Tax owed"
                : netVatPayable < 0 ? "Refund due"
                : "No payment due";
            items.Add(new VatChecklistItemDto
            {
                Key = "net_vat",
                Status = StatusPass,
                Title = "Net VAT",
                Detail = $"{netLabel} — {currencySymbol}{Math.Abs(netVatPayable):N2}"
            });

            // On a filed period the checklist is a read-only review: the actionable checks
            // (unassigned, trend, discrepancy) can no longer be acted on before submission, and
            // any discrepancy now reflects post-filing data drift rather than a pre-submission
            // issue. Downgrade Warning → Info so the panel informs without nagging, and the
            // summary reflects the filed state (WarningCount = 0). Info/Pass are left as-is.
            if (isSubmitted)
            {
                foreach (var item in items)
                {
                    if (item.Status == StatusWarning)
                    {
                        item.Status = StatusInfo;
                    }
                }
            }

            var dto = new VatPreSubmissionChecklistDto
            {
                IsSubmitted = isSubmitted,
                WarningCount = items.Count(i => i.Status == StatusWarning),
                CurrencySymbol = currencySymbol,
                Items = items
            };

            return ServiceResult<VatPreSubmissionChecklistDto>.Ok(dto);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>The three computed VAT figures for a period, with no persistence side effects.</summary>
    private readonly record struct VatFigures(decimal TotalOutputVat, decimal TotalInputVat, decimal NetVatPayable);

    /// <summary>
    /// Computes Output/Input/Net VAT for a period purely in-memory (read-only, no persistence,
    /// no audit log). Shared by CreateOrRecalculateAsync (which then persists) and the
    /// pre-submission checklist (which must stay read-only).
    /// </summary>
    private async Task<VatFigures> ComputeSubmissionFiguresAsync(int businessId, VatSubmissionPeriod period)
    {
        var periodId = period.Id;
        var start = period.PeriodStartDate;
        var end = period.PeriodEndDate;

        // Output VAT — Part 1: invoices explicitly assigned to this period
        var explicitOutputVat = await _portalDbContext.Invoices
            .Where(i => i.BusinessId == businessId
                && i.InvoiceStatusTypeId == 2
                && !i.IsDeleted
                && i.VatSubmissionPeriodId == periodId)
            .SumAsync(i => (decimal?)i.TaxAmount) ?? 0m;

        // Output VAT — Part 2: invoices with NULL assignment falling in date range (backward compat)
        var dateRangeOutputVat = await _portalDbContext.Invoices
            .Where(i => i.BusinessId == businessId
                && i.InvoiceStatusTypeId == 2
                && !i.IsDeleted
                && i.VatSubmissionPeriodId == null
                && i.InvoiceDate >= start
                && i.InvoiceDate <= end)
            .SumAsync(i => (decimal?)i.TaxAmount) ?? 0m;

        // Subtract credit note TaxAmount from Output VAT (only Issued or Applied credit notes)
        var creditNoteTaxReduction = await _portalDbContext.CreditNotes
            .Where(cn => cn.BusinessId == businessId
                && cn.VatSubmissionPeriodId == periodId
                && (cn.CreditNoteStatusTypeId == 2 || cn.CreditNoteStatusTypeId == 3))
            .SumAsync(cn => (decimal?)cn.TaxAmount) ?? 0m;

        // Z-Report Revenue: sum of RevenueSummary.TotalVat assigned to this period (if feature enabled)
        var zReportOutputVat = 0m;
        var businessProfile = await _portalDbContext.BusinessProfiles
            .FirstOrDefaultAsync(bp => bp.BusinessId == businessId);
        if (businessProfile?.IsZReportEnabled == true)
        {
            zReportOutputVat = await _portalDbContext.RevenueSummaries
                .Where(rs => rs.BusinessId == businessId
                    && rs.IsActive
                    && rs.VatSubmissionPeriodId == periodId)
                .SumAsync(rs => (decimal?)rs.TotalVat) ?? 0m;
        }

        // External sales records (imported POS transactions AND external platform sales):
        // sum ExternalSalesRecord.VatAmount for active records assigned to this period.
        // These are line-level recorded sales VAT and contribute to Output VAT the same way
        // Z-Report summaries do. Assigned records only (VatSubmissionPeriodId set at import time).
        var externalSalesOutputVat = await _portalDbContext.ExternalSalesRecords
            .Where(esr => esr.BusinessId == businessId
                && esr.IsActive
                && esr.VatSubmissionPeriodId == periodId)
            .SumAsync(esr => (decimal?)esr.VatAmount) ?? 0m;

        var totalOutputVat = explicitOutputVat + dateRangeOutputVat + zReportOutputVat + externalSalesOutputVat - creditNoteTaxReduction;

        // Input VAT — Part 1: purchases explicitly assigned (origin type != 2 = exclude EU reverse charge, VAT is 0)
        var explicitInputVat = await _portalDbContext.Purchases
            .Where(p => p.BusinessId == businessId
                && p.PurchaseOriginTypeId != 2
                && !p.IsCancelled
                && p.VatSubmissionPeriodId == periodId)
            .SumAsync(p => (decimal?)p.VatAmount) ?? 0m;

        // Input VAT — Part 2: purchases with NULL assignment falling in date range (backward compat)
        var dateRangeInputVat = await _portalDbContext.Purchases
            .Where(p => p.BusinessId == businessId
                && p.PurchaseOriginTypeId != 2
                && !p.IsCancelled
                && p.VatSubmissionPeriodId == null
                && p.InvoiceDate >= start
                && p.InvoiceDate <= end)
            .SumAsync(p => (decimal?)p.VatAmount) ?? 0m;

        var totalInputVat = explicitInputVat + dateRangeInputVat;

        return new VatFigures(totalOutputVat, totalInputVat, totalOutputVat - totalInputVat);
    }

    /// <summary>Counts non-cancelled purchases belonging to a period (explicit assignment or date-range fallback).</summary>
    /// <remarks>
    /// Deliberately includes ALL origin types (including EU reverse charge, type 2). This is a
    /// record-completeness / trend metric, not a VAT-amount metric, so it must match the
    /// unassigned-purchase count used on VatController.Index. The VAT-amount helpers below
    /// exclude origin type 2 because reverse-charge purchases carry zero VAT — do not "align"
    /// these two; the asymmetry is intentional.
    /// </remarks>
    private async Task<int> CountPurchasesForPeriodAsync(int businessId, int periodId, DateOnly start, DateOnly end)
    {
        return await _portalDbContext.Purchases
            .CountAsync(p => p.BusinessId == businessId
                && !p.IsCancelled
                && (p.VatSubmissionPeriodId == periodId
                    || (p.VatSubmissionPeriodId == null && p.InvoiceDate >= start && p.InvoiceDate <= end)));
    }

    /// <summary>Input VAT computed by invoice date regardless of assignment (mirrors VatController.Detail).</summary>
    private async Task<decimal> ComputeInputVatByDateAsync(int businessId, DateOnly start, DateOnly end)
    {
        return await _portalDbContext.Purchases
            .Where(p => p.BusinessId == businessId
                && p.PurchaseOriginTypeId != 2
                && !p.IsCancelled
                && p.InvoiceDate >= start
                && p.InvoiceDate <= end)
            .SumAsync(p => (decimal?)p.VatAmount) ?? 0m;
    }

    /// <summary>Purchases assigned to this period whose InvoiceDate falls outside it (late inclusions).</summary>
    private async Task<int> CountLatePurchasesAsync(int businessId, int periodId, DateOnly start, DateOnly end)
    {
        return await _portalDbContext.Purchases
            .CountAsync(p => p.BusinessId == businessId
                && p.PurchaseOriginTypeId != 2
                && !p.IsCancelled
                && p.VatSubmissionPeriodId == periodId
                && (p.InvoiceDate < start || p.InvoiceDate > end));
    }

    /// <summary>Purchases dated in this period but assigned to a different (later) period.</summary>
    private async Task<int> CountPurchasesReportedLaterAsync(int businessId, int periodId, DateOnly start, DateOnly end)
    {
        return await _portalDbContext.Purchases
            .CountAsync(p => p.BusinessId == businessId
                && p.PurchaseOriginTypeId != 2
                && !p.IsCancelled
                && p.InvoiceDate >= start
                && p.InvoiceDate <= end
                && p.VatSubmissionPeriodId != null
                && p.VatSubmissionPeriodId != periodId);
    }
}
