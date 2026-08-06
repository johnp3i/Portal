using Portal.Infrastructure.Models.Payroll;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Orchestrator that wraps the existing PayslipCalculationEngine and adds PAYE income tax.
/// When IsPayeApplicable = false, returns the engine result unchanged (zero regression risk).
/// When IsPayeApplicable = true, computes PAYE and appends a PAYE deduction line.
/// Registered as Scoped (needs repository access to load tax bands).
/// </summary>
public interface IPayslipCalculationOrchestrator
{
    /// <summary>
    /// Calculates a payslip with optional PAYE income tax.
    /// </summary>
    /// <param name="input">Standard payslip calculation input (employee, earnings, deductions, period date).</param>
    /// <param name="isPayeApplicable">Whether PAYE should be calculated for this employee.</param>
    /// <returns>PayslipCalculationResult with PAYE line appended if applicable.</returns>
    Task<PayslipCalculationResult> CalculateWithPayeAsync(PayslipCalculationInput input, bool isPayeApplicable);
}
