using Portal.Infrastructure.Models.Payroll;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Pure calculation engine for payslip computation. No I/O, no dependencies — suitable for Singleton registration.
/// Computes earning totals, deduction amounts (using historical rates), net salary, and employer contributions.
/// </summary>
public interface IPayslipCalculationEngine
{
    /// <summary>
    /// Calculates a complete payslip from the given input (employee, earning lines, applicable deductions, period date).
    /// Returns a result containing all computed values or a validation error.
    /// </summary>
    PayslipCalculationResult Calculate(PayslipCalculationInput input);
}
