using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services.CustomerPurchases;

public static partial class CustomerPurchasesCalculator
{
    private static decimal Round2(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Builds the KPI block from the counted invoices of the Selected_Currency. Pending and Overdue
    /// come from the same pending list used for the pending table.
    /// </summary>
    public static KpiDto BuildKpis(IReadOnlyList<InvoiceFinancialRow> rows, PurchasesQuery query, DateOnly today)
    {
        return new KpiDto
        {
            Rev12 = BuildRev12(rows, today),
            Lifetime = BuildLifetime(rows),
            AvgInvoice = BuildAvgInvoice(rows, query, today),
            Last = BuildLastPurchase(rows, today),
            Pending = BuildPendingKpi(rows),
            Overdue = BuildOverdueKpi(rows, today)
        };
    }

    private static Rev12Dto BuildRev12(IReadOnlyList<InvoiceFinancialRow> rows, DateOnly today)
    {
        var windowStart = new DateOnly(today.Year, today.Month, 1).AddMonths(-11);
        var windowEnd = new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(-1);
        var priorStart = windowStart.AddMonths(-12);
        var priorEnd = windowStart.AddDays(-1);

        var rev12 = Round2(rows
            .Where(row => row.InvoiceDate >= windowStart && row.InvoiceDate <= windowEnd)
            .Sum(NetRevenue));
        var comparison = Round2(rows
            .Where(row => row.InvoiceDate >= priorStart && row.InvoiceDate <= priorEnd)
            .Sum(NetRevenue));

        if (comparison <= 0m)
        {
            return new Rev12Dto { Value = rev12, Comparison = comparison, HasPrior = false, ChangePercent = null, Direction = "none" };
        }

        var change = Math.Round((rev12 - comparison) / comparison * 100m, 1, MidpointRounding.AwayFromZero);
        var direction = change > 0m ? "up" : change < 0m ? "down" : "flat";
        return new Rev12Dto { Value = rev12, Comparison = comparison, HasPrior = true, ChangePercent = change, Direction = direction };
    }

    private static LifetimeDto BuildLifetime(IReadOnlyList<InvoiceFinancialRow> rows)
    {
        return new LifetimeDto
        {
            Value = Round2(rows.Sum(NetRevenue)),
            InvoiceCount = rows.Count,
            FirstInvoiceDate = rows.Count == 0 ? null : rows.Min(row => row.InvoiceDate)
        };
    }

    private static AvgInvoiceDto BuildAvgInvoice(IReadOnlyList<InvoiceFinancialRow> rows, PurchasesQuery query, DateOnly today)
    {
        var (start, end) = ResolvePeriod(query, today);
        var inPeriod = rows
            .Where(row => (start is null || row.InvoiceDate >= start.Value)
                          && (end is null || row.InvoiceDate <= end.Value))
            .ToList();

        if (inPeriod.Count == 0)
        {
            return new AvgInvoiceDto { Value = null, InvoiceCount = 0 };
        }

        return new AvgInvoiceDto
        {
            Value = Round2(inPeriod.Sum(NetRevenue) / inPeriod.Count),
            InvoiceCount = inPeriod.Count
        };
    }

    private static LastPurchaseDto BuildLastPurchase(IReadOnlyList<InvoiceFinancialRow> rows, DateOnly today)
    {
        if (rows.Count == 0)
        {
            return new LastPurchaseDto();
        }

        var last = rows.Max(row => row.InvoiceDate);
        var daysAgo = Math.Max(0, today.DayNumber - last.DayNumber);

        int? frequency = null;
        if (rows.Count >= 3)
        {
            var median = MedianGapDays(rows.Select(row => row.InvoiceDate));
            if (median is not null)
            {
                frequency = Math.Max(1, median.Value);
            }
        }

        return new LastPurchaseDto { Date = last, DaysAgo = daysAgo, FrequencyDays = frequency };
    }

    private static PendingKpiDto BuildPendingKpi(IReadOnlyList<InvoiceFinancialRow> rows)
    {
        var pending = rows.Where(IsPending).ToList();
        return new PendingKpiDto
        {
            Count = pending.Count,
            Amount = Round2(pending.Sum(Outstanding))
        };
    }

    private static OverdueKpiDto BuildOverdueKpi(IReadOnlyList<InvoiceFinancialRow> rows, DateOnly today)
    {
        var overdue = rows.Where(row => IsOverdue(row, today)).ToList();
        if (overdue.Count == 0)
        {
            return new OverdueKpiDto { Count = 0, Amount = 0m, DaysLate = null };
        }

        var earliestDue = overdue.Min(row => row.DueDate);
        return new OverdueKpiDto
        {
            Count = overdue.Count,
            Amount = Round2(overdue.Sum(Outstanding)),
            DaysLate = Math.Max(1, today.DayNumber - earliestDue.DayNumber)
        };
    }

    /// <summary>
    /// Twelve trend buckets, oldest first, ending with the current month. Ignores the selected period.
    /// </summary>
    public static List<TrendMonthDto> BuildTrend(IReadOnlyList<InvoiceFinancialRow> rows, DateOnly today)
    {
        var currentMonth = new DateOnly(today.Year, today.Month, 1);
        var buckets = new List<TrendMonthDto>(12);

        for (var offset = 11; offset >= 0; offset--)
        {
            var month = currentMonth.AddMonths(-offset);
            var previous = month.AddMonths(-12);

            var current = rows.Where(row => row.InvoiceDate.Year == month.Year && row.InvoiceDate.Month == month.Month).ToList();
            var previousRevenue = rows
                .Where(row => row.InvoiceDate.Year == previous.Year && row.InvoiceDate.Month == previous.Month)
                .Sum(NetRevenue);

            buckets.Add(new TrendMonthDto
            {
                Year = month.Year,
                Month = month.Month,
                IsCurrent = offset == 0,
                Revenue = Round2(current.Sum(NetRevenue)),
                InvoiceCount = current.Count,
                PreviousYearRevenue = Round2(previousRevenue)
            });
        }

        return buckets;
    }
}
