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
}
