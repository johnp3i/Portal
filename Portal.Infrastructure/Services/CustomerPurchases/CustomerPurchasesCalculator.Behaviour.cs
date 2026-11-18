using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services.CustomerPurchases;

/// <summary>Buying rhythm and payment behaviour calculations.</summary>
public static partial class CustomerPurchasesCalculator
{
    public const string ChipOnTrack = "onTrack";
    public const string ChipReorderAround = "reorderAround";
    public const string ChipReorderSoon = "reorderSoon";

    private const int RhythmMaxItems = 5;
    private const int ReorderSoonWindowDays = 7;

    /// <summary>
    /// Rhythm per group key from all-time product lines (Selected_Currency). Groups need at least
    /// 2 distinct purchase dates. Top 5 by all-time revenue descending, then group key ascending.
    /// </summary>
    public static List<RhythmItemDto> BuildRhythm(IEnumerable<ProductLineRow> allTimeLines, DateOnly today)
    {
        var items = new List<(string Key, decimal Revenue, RhythmItemDto Item)>();

        foreach (var group in allTimeLines.GroupBy(line => GroupKeyString(line.ProductCode, line.Description), StringComparer.Ordinal))
        {
            var groupLines = group.ToList();
            var gap = MedianGapDays(groupLines.Select(line => line.InvoiceDate));
            if (gap is null)
            {
                continue;
            }

            var latest = groupLines.Max(line => line.InvoiceDate);
            var daysSince = Math.Max(0, today.DayNumber - latest.DayNumber);
            var expected = latest.AddDays(gap.Value);

            string chip;
            if (daysSince * 2 < gap.Value)
            {
                chip = ChipOnTrack;
            }
            else if (expected > today.AddDays(ReorderSoonWindowDays))
            {
                chip = ChipReorderAround;
            }
            else
            {
                chip = ChipReorderSoon;
            }

            var name = groupLines
                .Where(line => line.InvoiceDate == latest)
                .Select(line => line.Description ?? "")
                .OrderBy(text => text, StringComparer.OrdinalIgnoreCase)
                .ThenBy(text => text, StringComparer.Ordinal)
                .First();

            items.Add((group.Key, groupLines.Sum(line => line.LineTotal), new RhythmItemDto
            {
                Name = name,
                TypicalGapDays = gap.Value,
                DaysSinceLast = daysSince,
                Chip = chip,
                ExpectedDate = expected
            }));
        }

        return items
            .OrderByDescending(entry => entry.Revenue)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Take(RhythmMaxItems)
            .Select(entry => entry.Item)
            .ToList();
    }

    /// <summary>
    /// Payment behaviour over the period invoices: settled means outstanding &lt;= 0 with a last
    /// payment date. Overdue amount and credit note figures are supplied by the caller.
    /// </summary>
    public static PaymentBehaviourDto BuildPaymentBehaviour(
        IEnumerable<InvoiceFinancialRow> periodInvoices,
        decimal overdueAmount,
        CreditNoteSummaryRow? credit)
    {
        var settled = periodInvoices
            .Where(row => Outstanding(row) <= 0m && row.LastPaymentDateUtc is not null)
            .ToList();

        var creditCount = credit?.Count ?? 0;
        var creditTotal = credit?.Total ?? 0m;

        if (settled.Count == 0)
        {
            return new PaymentBehaviourDto
            {
                HasSettled = false,
                OverdueAmount = overdueAmount,
                CreditNoteCount = creditCount,
                CreditNoteTotal = creditTotal
            };
        }

        var daysToPay = new List<int>(settled.Count);
        var terms = new List<int>(settled.Count);
        var onTime = 0;
        foreach (var row in settled)
        {
            var paid = DateOnly.FromDateTime(row.LastPaymentDateUtc!.Value);
            daysToPay.Add(paid.DayNumber - row.InvoiceDate.DayNumber);
            terms.Add(row.DueDate.DayNumber - row.InvoiceDate.DayNumber);
            if (paid <= row.DueDate)
            {
                onTime++;
            }
        }

        return new PaymentBehaviourDto
        {
            HasSettled = true,
            AvgDaysToPay = RoundMean(daysToPay),
            AvgTermsDays = RoundMean(terms),
            OnTimeCount = onTime,
            SettledCount = settled.Count,
            OnTimePercent = Math.Round((decimal)onTime / settled.Count * 100m, 0, MidpointRounding.AwayFromZero),
            OverdueAmount = overdueAmount,
            CreditNoteCount = creditCount,
            CreditNoteTotal = creditTotal
        };
    }

    private static int RoundMean(IReadOnlyList<int> values)
    {
        var mean = (decimal)values.Sum(value => (long)value) / values.Count;
        return (int)Math.Round(mean, 0, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Catalogue currency: the business primary currency when it is a valid three-letter code,
    /// otherwise null (no catalogue price comparison).
    /// </summary>
    public static string? InferCatalogueCurrency(string? businessPrimaryCurrency)
    {
        return PurchasesQuery.NormalizeCurrency(businessPrimaryCurrency?.ToUpperInvariant());
    }
}
