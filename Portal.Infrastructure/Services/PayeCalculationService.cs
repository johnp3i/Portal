using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models.Payroll;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Pure PAYE progressive tax calculation service.
/// Registered as Singleton (no state, no I/O).
/// 
/// Algorithm:
/// 1. Project monthly income to annual (×12)
/// 2. Apply progressive bands: for each band, compute taxable amount within band and tax for band
/// 3. Sum annual tax, divide by 12, round to 2dp
/// 4. Track top marginal rate (highest band rate where income falls)
/// </summary>
public class PayeCalculationService : IPayeCalculationService
{
    public PayeCalculationResult CalculateMonthlyPaye(decimal monthlyTaxableIncome, List<PayeTaxBand> bands)
    {
        try
        {
            var result = new PayeCalculationResult();

            // Validate bands
            if (bands == null || bands.Count == 0)
            {
                result.IsValid = false;
                result.ValidationError = "No tax bands configured";
                return result;
            }

            // Zero or negative income → no tax
            if (monthlyTaxableIncome <= 0)
            {
                result.IsValid = true;
                result.MonthlyPaye = 0;
                result.AnnualProjectedIncome = 0;
                result.AnnualTax = 0;
                result.EffectiveRate = 0;
                result.TopMarginalRate = 0;
                return result;
            }

            // Step 1: Project to annual
            decimal annualProjected = monthlyTaxableIncome * 12;

            // Step 2: Apply progressive bands
            decimal annualTax = 0;
            decimal topMarginalRate = 0;
            var breakdowns = new List<PayeBandBreakdown>();

            // Bands must be ordered by LowerBound ascending
            var orderedBands = bands.OrderBy(b => b.LowerBound).ToList();

            foreach (var band in orderedBands)
            {
                decimal bandLower = band.LowerBound;
                decimal bandUpper = band.UpperBound ?? decimal.MaxValue;

                // If income doesn't reach this band, stop
                if (annualProjected <= bandLower)
                    break;

                // Compute taxable amount within this band
                decimal incomeInBand = Math.Min(annualProjected, bandUpper) - bandLower;

                if (incomeInBand <= 0)
                    continue;

                decimal taxForBand = Math.Round(incomeInBand * band.Rate, 2, MidpointRounding.AwayFromZero);
                annualTax += taxForBand;

                // Track top marginal rate (last band where income falls)
                if (incomeInBand > 0)
                    topMarginalRate = band.Rate;

                breakdowns.Add(new PayeBandBreakdown
                {
                    LowerBound = band.LowerBound,
                    UpperBound = band.UpperBound,
                    Rate = band.Rate,
                    TaxableAmountInBand = incomeInBand,
                    TaxForBand = taxForBand
                });
            }

            // Step 3: Compute monthly PAYE
            decimal monthlyPaye = Math.Round(annualTax / 12, 2, MidpointRounding.AwayFromZero);

            // Step 4: Compute effective rate
            decimal effectiveRate = annualProjected > 0 ? annualTax / annualProjected : 0;

            // Return result
            result.IsValid = true;
            result.AnnualProjectedIncome = annualProjected;
            result.AnnualTax = annualTax;
            result.MonthlyPaye = monthlyPaye;
            result.EffectiveRate = effectiveRate;
            result.TopMarginalRate = topMarginalRate;
            result.BandBreakdowns = breakdowns;

            return result;
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
