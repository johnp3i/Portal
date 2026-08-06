using Microsoft.Extensions.Logging;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Integrates payroll finalisation with compliance filings.
/// Non-blocking: if no matching filing is found or the update fails, log warning and return success.
/// 
/// Flow:
/// 1. Load all finalised payslips for the period
/// 2. Sum employer SI contribution lines (Code = "SI_Contribution", DeductionCategoryTypeId = 2)
/// 3. Find matching BusinessApplication with 1-month offset (July payroll → August DueDate)
/// 4. If no filing found → log warning, return success
/// 5. Update EstimatedAmount
/// 6. Create PayslipPeriodComplianceFiling record (always insert — preserves history)
/// </summary>
public class ComplianceIntegrationService : IComplianceIntegrationService
{
    private readonly PayrollRepository _payrollRepository;
    private readonly ILogger<ComplianceIntegrationService> _logger;

    public ComplianceIntegrationService(
        PayrollRepository payrollRepository,
        ILogger<ComplianceIntegrationService> logger)
    {
        _payrollRepository = payrollRepository;
        _logger = logger;
    }

    public async Task<ServiceResult> UpdateComplianceFilingFromPayrollAsync(int periodId, int businessId, string userId)
    {
        try
        {
            // Step 1-2: Load employer contributions and sum SI_Contribution lines only
            var contributions = await _payrollRepository.GetEmployerContributionsForPeriodAsync(periodId, businessId);

            var siTotal = contributions
                .Where(c => c.DeductionTypeCode == "SI_Contribution")
                .Sum(c => c.CalculatedAmount);

            if (siTotal <= 0)
            {
                _logger.LogInformation(
                    "No SI employer contributions found for period {PeriodId} in business {BusinessId}. Skipping compliance integration.",
                    periodId, businessId);
                return ServiceResult.Ok();
            }

            // Step 3: Find matching compliance filing with 1-month offset
            var period = await _payrollRepository.GetPeriodByIdAsync(periodId, businessId);
            if (period == null)
            {
                _logger.LogWarning("Period {PeriodId} not found for compliance integration.", periodId);
                return ServiceResult.Ok();
            }

            var filingId = await _payrollRepository.FindSocialInsuranceFilingAsync(businessId, period.Year, period.Month);

            // Step 4: If no filing found, log and return success (non-blocking)
            if (!filingId.HasValue)
            {
                _logger.LogWarning(
                    "No Social Insurance compliance filing found for business {BusinessId}, payroll period {Year}/{Month}. " +
                    "Expected filing with DueDate in {DueMonth}/{DueYear}.",
                    businessId, period.Year, period.Month,
                    period.Month < 12 ? period.Month + 1 : 1,
                    period.Month < 12 ? period.Year : period.Year + 1);
                return ServiceResult.Ok();
            }

            // Step 5: Update EstimatedAmount on the compliance filing
            await _payrollRepository.UpdateComplianceFilingEstimatedAmountAsync(filingId.Value, siTotal);

            // Step 6: Create cross-reference record (always insert — preserves history)
            var link = new PayslipPeriodComplianceFiling
            {
                PayslipPeriodId = periodId,
                ComplianceFilingId = filingId.Value,
                ContributionTotal = siTotal,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByUserId = userId
            };

            await _payrollRepository.InsertComplianceFilingLinkAsync(link);

            _logger.LogInformation(
                "Compliance integration complete: Period {PeriodId}, Filing {FilingId}, SI Total €{SiTotal:F2}",
                periodId, filingId.Value, siTotal);

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            // Non-blocking: log error but do not fail
            _logger.LogError(ex,
                "Compliance integration failed for period {PeriodId}, business {BusinessId}. " +
                "This does not affect payslip finalisation.",
                periodId, businessId);
            return ServiceResult.Ok();
        }
    }
}
