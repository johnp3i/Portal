using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models.Payroll;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Pure calculation engine for payslip computation.
/// Implements the full algorithm: resolve earnings → compute total → apply deductions with historical rates → compute net salary.
/// Registered as Singleton (no state, no I/O).
/// </summary>
public class PayslipCalculationEngine : IPayslipCalculationEngine
{
    public PayslipCalculationResult Calculate(PayslipCalculationInput input)
    {
        var result = new PayslipCalculationResult();

        // Step 1: Resolve each earning line amount
        var computedEarnings = new List<ComputedEarningLine>();

        foreach (var line in input.EarningLines)
        {
            if (line.EarningTypeCode == "Overtime")
            {
                // Validate overtime prerequisites
                if (input.Employee.HourlyRate == null || input.Employee.HourlyRate <= 0)
                {
                    result.IsValid = false;
                    result.ValidationError = "Hourly rate is required for overtime calculation.";
                    return result;
                }

                if (line.OvertimeHours == null || line.OvertimeHours <= 0)
                {
                    result.IsValid = false;
                    result.ValidationError = "Overtime hours must be specified for overtime earning lines.";
                    return result;
                }

                var multiplier = line.OvertimeMultiplier ?? 1.5m;

                if (multiplier < 1.0m || multiplier > 4.0m)
                {
                    result.IsValid = false;
                    result.ValidationError = "Overtime multiplier must be between 1.0 and 4.0.";
                    return result;
                }

                var amount = line.OvertimeHours.Value * input.Employee.HourlyRate.Value * multiplier;

                computedEarnings.Add(new ComputedEarningLine
                {
                    EarningTypeId = line.EarningTypeId,
                    Description = line.Description,
                    Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero),
                    OvertimeMultiplier = multiplier,
                    OvertimeHours = line.OvertimeHours
                });
            }
            else
            {
                // Non-overtime: use manually entered amount
                if (line.Amount == null || line.Amount <= 0)
                {
                    result.IsValid = false;
                    result.ValidationError = $"Amount must be greater than zero for earning line '{line.Description ?? line.EarningTypeCode}'.";
                    return result;
                }

                computedEarnings.Add(new ComputedEarningLine
                {
                    EarningTypeId = line.EarningTypeId,
                    Description = line.Description,
                    Amount = line.Amount.Value,
                    OvertimeMultiplier = null,
                    OvertimeHours = null
                });
            }
        }

        // Step 2: Compute TotalEarnings
        var totalEarnings = computedEarnings.Sum(e => e.Amount);

        // Step 3: For each applicable deduction, look up rate and compute
        var computedDeductions = new List<ComputedDeductionLine>();

        foreach (var deductionType in input.ApplicableDeductions)
        {
            // Find effective rate for the period date
            var rateHistory = FindEffectiveRate(deductionType.RateHistories, input.PeriodDate);

            if (rateHistory == null)
            {
                result.IsValid = false;
                result.ValidationError = $"No effective rate found for '{deductionType.Name}' on {input.PeriodDate:yyyy-MM-dd}.";
                return result;
            }

            decimal calculatedAmount;

            if (deductionType.IsPercentage)
            {
                calculatedAmount = Math.Round(totalEarnings * (rateHistory.Rate / 100m), 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                // Fixed amount deduction
                calculatedAmount = rateHistory.Rate;
            }

            computedDeductions.Add(new ComputedDeductionLine
            {
                DeductionTypeId = deductionType.Id,
                BaseAmount = totalEarnings,
                Rate = rateHistory.Rate,
                CalculatedAmount = calculatedAmount,
                DeductionCategoryTypeId = deductionType.DeductionCategoryTypeId,
                DeductionRateHistoryId = rateHistory.Id
            });
        }

        // Step 4: Compute totals by category
        // DeductionCategoryTypeId: 1 = Deduction (from employee), 2 = Contribution (by employer)
        var totalEmployeeDeductions = computedDeductions
            .Where(d => d.DeductionCategoryTypeId == 1)
            .Sum(d => d.CalculatedAmount);

        var totalEmployerContributions = computedDeductions
            .Where(d => d.DeductionCategoryTypeId == 2)
            .Sum(d => d.CalculatedAmount);

        // Step 5: Compute NetSalary
        var netSalary = totalEarnings - totalEmployeeDeductions;

        // Return complete result
        result.IsValid = true;
        result.TotalEarnings = totalEarnings;
        result.TotalEmployeeDeductions = totalEmployeeDeductions;
        result.NetSalary = netSalary;
        result.TotalEmployerContributions = totalEmployerContributions;
        result.EarningLines = computedEarnings;
        result.DeductionLines = computedDeductions;

        return result;
    }

    /// <summary>
    /// Finds the effective rate for a deduction type at the given period date.
    /// Returns the rate where EffectiveFromUtc &lt;= periodDate AND (EffectiveToUtc is null OR EffectiveToUtc &gt; periodDate).
    /// </summary>
    private static DeductionRateHistory? FindEffectiveRate(List<DeductionRateHistory> histories, DateTime periodDate)
    {
        return histories.FirstOrDefault(h =>
            h.EffectiveFromUtc <= periodDate &&
            (h.EffectiveToUtc == null || h.EffectiveToUtc > periodDate));
    }
}
