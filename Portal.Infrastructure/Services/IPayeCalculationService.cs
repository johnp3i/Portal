using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models.Payroll;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Pure PAYE progressive tax calculation service.
/// No I/O, no state — suitable for Singleton registration.
/// Computes monthly PAYE from monthly taxable income using progressive annual tax bands.
/// </summary>
public interface IPayeCalculationService
{
    /// <summary>
    /// Calculates monthly PAYE income tax from monthly taxable income using progressive bands.
    /// Projects income to annual, applies bands, then divides by 12.
    /// </summary>
    /// <param name="monthlyTaxableIncome">Monthly income after PAYE-deductible deductions.</param>
    /// <param name="bands">Progressive tax bands ordered by LowerBound ascending.</param>
    /// <returns>PayeCalculationResult with monthly PAYE, effective rate, and per-band breakdown.</returns>
    PayeCalculationResult CalculateMonthlyPaye(decimal monthlyTaxableIncome, List<PayeTaxBand> bands);
}
