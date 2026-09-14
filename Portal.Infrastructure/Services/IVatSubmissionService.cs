using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for VAT submission management including computation of output/input VAT,
/// submission creation/recalculation, and filing status tracking.
/// </summary>
public interface IVatSubmissionService
{
    /// <summary>
    /// Creates a new submission or recalculates an existing one for the specified period.
    /// Computes TotalOutputVat, TotalInputVat, and NetVatPayable from invoices and purchases.
    /// Returns ServiceResult with the VatSubmission on success.
    /// </summary>
    Task<ServiceResult<VatSubmission>> CreateOrRecalculateAsync(int vatSubmissionPeriodId);

    /// <summary>
    /// Marks an existing submission as filed with the tax authority.
    /// Sets IsSubmitted = true and SubmittedAtUtc = DateTime.UtcNow.
    /// </summary>
    Task<ServiceResult> MarkAsSubmittedAsync(int vatSubmissionId);

    /// <summary>
    /// Retrieves a submission by its period ID for the current tenant.
    /// Returns null if no submission exists for the period.
    /// </summary>
    Task<VatSubmission?> GetByPeriodIdAsync(int vatSubmissionPeriodId);

    /// <summary>
    /// Builds the advisory, non-blocking pre-submission checklist for a period:
    /// automated checks (unassigned purchases/invoices, zero-VAT invoices, purchase-count
    /// trend, input VAT discrepancy) plus the computed Output/Input/Net VAT figures.
    /// </summary>
    Task<ServiceResult<VatPreSubmissionChecklistDto>> GetPreSubmissionChecklistAsync(int vatSubmissionPeriodId);

    /// <summary>
    /// Read-only approximate net VAT payable for a period the caller already holds — WITHOUT tenant
    /// context or persistence. Prefers a persisted (unsubmitted) VatSubmission.NetVatPayable; else
    /// computes it in-memory. Safe for tenant-less background scans (e.g. the VAT Period Due Reminder
    /// assistant), which pass an explicit businessId + the period they scanned. Positive = tax owed,
    /// negative = refund due, zero = no payment expected.
    /// </summary>
    Task<decimal> GetApproxNetVatPayableAsync(int businessId, VatSubmissionPeriod period);
}
