namespace Portal.Infrastructure.Models;

/// <summary>
/// One grouped row from the expense-trend aggregation: the spend total for a single series
/// (category or supplier) within a single time bucket. Produced by
/// PurchaseRepository.GetExpenseTrendAsync and pivoted into <see cref="ExpenseTrendResult"/> by the
/// service.
/// </summary>
public class ExpenseTrendPoint
{
    /// <summary>The bucket start date (day/month/year start, per the chosen bucket granularity).</summary>
    public DateOnly Bucket { get; set; }

    /// <summary>The series id — ExpenseCategoryId or SupplierId depending on the dimension.</summary>
    public int SeriesId { get; set; }

    /// <summary>The series display name (category or supplier name).</summary>
    public string SeriesName { get; set; } = string.Empty;

    /// <summary>Spend in this bucket for this series: SUM(TotalAmount), cancelled excluded.</summary>
    public decimal Amount { get; set; }
}

/// <summary>
/// A single line on the expense-trend chart: a category or supplier, with one spend value per bucket
/// aligned to <see cref="ExpenseTrendResult.Labels"/> (zero where there was no spend).
/// </summary>
public class ExpenseTrendSeries
{
    public string Name { get; set; } = string.Empty;
    public List<decimal> Data { get; set; } = new();
}

/// <summary>
/// The shaped expense-trend result for the chart: an ordered list of bucket labels and one series per
/// line. Every series' Data has the same length as Labels. The sum across all series over all buckets
/// reconciles with the Purchases page "Total Spend" KPI for the same filter.
/// </summary>
public class ExpenseTrendResult
{
    public List<string> Labels { get; set; } = new();
    public List<ExpenseTrendSeries> Series { get; set; } = new();
}
