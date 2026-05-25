namespace Portal.Infrastructure.Services;

/// <summary>
/// Static helper class for revenue-related pure computations.
/// Encapsulates view model calculations that can be tested independently.
/// </summary>
public static class RevenueCalculations
{
    /// <summary>
    /// Computes the payment progress bar percentage for an invoice.
    /// Returns (TotalPaid / TotalAmount) × 100, clamped between 0 and 100, rounded to 1 decimal place.
    /// Returns 0 when TotalAmount is zero or negative.
    /// </summary>
    /// <param name="totalAmount">The total invoice amount.</param>
    /// <param name="totalPaid">The total amount paid (sum of valid payments).</param>
    /// <returns>A percentage value between 0 and 100 (inclusive), rounded to 1 decimal place.</returns>
    public static decimal ComputeProgressPercentage(decimal totalAmount, decimal totalPaid)
    {
        if (totalAmount <= 0)
            return 0;

        var percentage = Math.Round(totalPaid / totalAmount * 100, 1);
        return Math.Min(100, Math.Max(0, percentage));
    }
}
