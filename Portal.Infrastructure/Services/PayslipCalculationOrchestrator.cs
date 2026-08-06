using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models.Payroll;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Orchestrator that wraps the existing PayslipCalculationEngine and adds PAYE income tax.
/// Registered as Scoped (needs repository access to load tax bands).
/// 
/// Flow:
/// 1. Call existing engine to get base calculation
/// 2. If !isPayeApplicable or result invalid, return as-is
/// 3. Sum PAYE-deductible employee deductions (IsPayeDeductible flag)
/// 4. Compute taxableIncome = TotalEarnings - payeDeductibleTotal
/// 5. Load tax bands for country and year
/// 6. Call PayeCalculationService
/// 7. Append PAYE ComputedDeductionLine, recalculate totals
/// </summary>
public class PayslipCalculationOrchestrator : IPayslipCalculationOrchestrator
{
    private readonly IPayslipCalculationEngine _calculationEngine;
    private readonly IPayeCalculationService _payeService;
    private readonly PayrollRepository _payrollRepository;
    private readonly IBusinessService _businessService;

    /// <summary>
    /// Maps BusinessProfile.Country (full name) to ISO 3166-1 alpha-2 code for PayeTaxBand lookup.
    /// </summary>
    private static readonly Dictionary<string, string> CountryCodeMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Cyprus", "CY" },
        { "Malta", "MT" },
        { "United Kingdom", "GB" },
        { "Greece", "GR" },
        { "Ireland", "IE" }
    };

    public PayslipCalculationOrchestrator(
        IPayslipCalculationEngine calculationEngine,
        IPayeCalculationService payeService,
        PayrollRepository payrollRepository,
        IBusinessService businessService)
    {
        _calculationEngine = calculationEngine;
        _payeService = payeService;
        _payrollRepository = payrollRepository;
        _businessService = businessService;
    }

    public async Task<PayslipCalculationResult> CalculateWithPayeAsync(PayslipCalculationInput input, bool isPayeApplicable)
    {
        try
        {
            // Step 1: Run existing engine (unchanged)
            var result = _calculationEngine.Calculate(input);

            // Step 2: If not applicable or result invalid, return as-is
            if (!isPayeApplicable || !result.IsValid)
                return result;

            // Step 3: Sum PAYE-deductible employee deductions
            // Identify which deductions reduce the PAYE base via IsPayeDeductible flag
            var payeDeductibleTypeIds = input.ApplicableDeductions
                .Where(d => d.IsPayeDeductible && d.DeductionCategoryTypeId == 1)
                .Select(d => d.Id)
                .ToHashSet();

            decimal payeDeductibleTotal = result.DeductionLines
                .Where(dl => payeDeductibleTypeIds.Contains(dl.DeductionTypeId))
                .Sum(dl => dl.CalculatedAmount);

            // Step 4: Compute taxable income
            decimal taxableIncome = result.TotalEarnings - payeDeductibleTotal;

            // Step 5: Get business profile → map country to ISO code → load tax bands
            var profile = await _businessService.GetBusinessProfileAsync(input.Employee.BusinessId);
            var countryName = profile?.Country ?? "Cyprus";

            if (!CountryCodeMapping.TryGetValue(countryName, out var countryCode))
                countryCode = "CY"; // Default to Cyprus if unmapped

            int periodYear = input.PeriodDate.Year;
            var bands = await _payrollRepository.GetTaxBandsAsync(countryCode, periodYear);

            // Step 6: Call PAYE calculation service
            var payeResult = _payeService.CalculateMonthlyPaye(taxableIncome, bands);

            if (!payeResult.IsValid)
            {
                result.IsValid = false;
                result.ValidationError = payeResult.ValidationError;
                return result;
            }

            // Step 7: Append PAYE deduction line
            // Get PAYE DeductionType Id for this business
            var payeTypeId = await _payrollRepository.GetPayeDeductionTypeIdForBusinessAsync(input.Employee.BusinessId);

            if (payeTypeId.HasValue)
            {
                var payeLine = new ComputedDeductionLine
                {
                    DeductionTypeId = payeTypeId.Value,
                    BaseAmount = taxableIncome,
                    Rate = payeResult.TopMarginalRate * 100, // Convert decimal rate to percentage for display (0.35 → 35.00)
                    CalculatedAmount = payeResult.MonthlyPaye,
                    DeductionCategoryTypeId = 1, // Employee deduction
                    DeductionRateHistoryId = null // PAYE uses progressive bands, not rate history
                };

                result.DeductionLines.Add(payeLine);

                // Recalculate totals
                result.TotalEmployeeDeductions = result.DeductionLines
                    .Where(d => d.DeductionCategoryTypeId == 1)
                    .Sum(d => d.CalculatedAmount);

                result.NetSalary = result.TotalEarnings - result.TotalEmployeeDeductions;
            }

            return result;
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
