using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services.CustomerPurchases;

/// <summary>
/// Pure calculation core for the Customer Purchases dashboard. No data access and no clock reads;
/// "today" is always passed in by the caller (UTC date).
/// </summary>
public static partial class CustomerPurchasesCalculator
{
    /// <summary>Net revenue of one invoice: Round(TotalAmount - TotalCredited, 2).</summary>
    public static decimal NetRevenue(InvoiceFinancialRow invoice)
    {
        return Math.Round(invoice.TotalAmount - invoice.TotalCredited, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Outstanding = Round(Total - Paid - Credited, 2).</summary>
    public static decimal Outstanding(InvoiceFinancialRow invoice)
    {
        return Math.Round(invoice.TotalAmount - invoice.TotalPaid - invoice.TotalCredited, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Pending: not written off and outstanding greater than zero.</summary>
    public static bool IsPending(InvoiceFinancialRow invoice)
    {
        return !invoice.IsWrittenOff && Outstanding(invoice) > 0m;
    }

    /// <summary>Overdue: pending and due date strictly before today. Derived only from balance and due date.</summary>
    public static bool IsOverdue(InvoiceFinancialRow invoice, DateOnly today)
    {
        return IsPending(invoice) && invoice.DueDate < today;
    }

    /// <summary>Resolves the inclusive period bounds; null bounds mean unbounded.</summary>
    public static (DateOnly? Start, DateOnly? End) ResolvePeriod(PurchasesQuery query, DateOnly today)
    {
        switch (query.PeriodKey)
        {
            case PurchasesQuery.PeriodThisYear:
                return (new DateOnly(today.Year, 1, 1), today);
            case PurchasesQuery.PeriodLastYear:
                return (new DateOnly(today.Year - 1, 1, 1), new DateOnly(today.Year - 1, 12, 31));
            case PurchasesQuery.PeriodAll:
                return (null, null);
            case PurchasesQuery.PeriodCustom:
                return (query.From, query.To);
            default:
                var firstOfMonth = new DateOnly(today.Year, today.Month, 1);
                return (firstOfMonth.AddMonths(-11), today);
        }
    }

    /// <summary>Distinct currency codes in ascending ordinal order.</summary>
    public static IReadOnlyList<string> DistinctCurrencies(IEnumerable<InvoiceFinancialRow> rows)
    {
        return rows
            .Select(row => row.CurrencyCode)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Selects the currency: the requested one when the customer has it, the only one when single,
    /// otherwise the highest Net_Revenue in the period (ties by ordinal code); when no invoice is in
    /// the period, the currency of the most recent invoice (latest date, then highest Id).
    /// Returns null when there are no rows.
    /// </summary>
    public static string? SelectCurrency(IReadOnlyList<InvoiceFinancialRow> rows, PurchasesQuery query, DateOnly today)
    {
        var currencies = DistinctCurrencies(rows);
        if (currencies.Count == 0)
        {
            return null;
        }

        if (query.Currency is not null && currencies.Contains(query.Currency, StringComparer.Ordinal))
        {
            return query.Currency;
        }

        if (currencies.Count == 1)
        {
            return currencies[0];
        }

        var (start, end) = ResolvePeriod(query, today);
        var inPeriod = rows
            .Where(row => (start is null || row.InvoiceDate >= start.Value)
                          && (end is null || row.InvoiceDate <= end.Value))
            .ToList();

        if (inPeriod.Count == 0)
        {
            return rows
                .OrderByDescending(row => row.InvoiceDate)
                .ThenByDescending(row => row.Id)
                .First()
                .CurrencyCode;
        }

        return inPeriod
            .GroupBy(row => row.CurrencyCode, StringComparer.Ordinal)
            .Select(group => new { Code = group.Key, Net = group.Sum(NetRevenue) })
            .OrderByDescending(item => item.Net)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .First()
            .Code;
    }

    /// <summary>
    /// Median of day gaps between consecutive distinct sorted dates. Even count uses (a + b + 1) / 2
    /// (half up, integer). Null for fewer than 2 distinct dates.
    /// </summary>
    public static int? MedianGapDays(IEnumerable<DateOnly> dates)
    {
        var sorted = dates.Distinct().OrderBy(date => date).ToList();
        if (sorted.Count < 2)
        {
            return null;
        }

        var gaps = new List<int>(sorted.Count - 1);
        for (var i = 1; i < sorted.Count; i++)
        {
            gaps.Add(sorted[i].DayNumber - sorted[i - 1].DayNumber);
        }

        gaps.Sort();
        var count = gaps.Count;
        if (count % 2 == 1)
        {
            return gaps[count / 2];
        }

        return (gaps[count / 2 - 1] + gaps[count / 2] + 1) / 2;
    }
}
