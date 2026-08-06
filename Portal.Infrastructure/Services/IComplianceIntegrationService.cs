using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Integrates payroll finalisation with compliance filings.
/// Updates Social Insurance filing estimated amounts from payroll data.
/// NON-BLOCKING: failures log a warning but do not fail finalisation.
/// </summary>
public interface IComplianceIntegrationService
{
    /// <summary>
    /// After period finalisation, sums employer SI contributions and updates the matching compliance filing.
    /// Creates a PayslipPeriodComplianceFiling cross-reference record for audit history.
    /// </summary>
    /// <param name="periodId">The payslip period that was finalised.</param>
    /// <param name="businessId">The tenant business Id.</param>
    /// <param name="userId">The user who triggered finalisation.</param>
    /// <returns>ServiceResult (always success unless a critical error occurs).</returns>
    Task<ServiceResult> UpdateComplianceFilingFromPayrollAsync(int periodId, int businessId, string userId);
}
