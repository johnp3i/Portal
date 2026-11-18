using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services.CustomerPurchases;

public static partial class CustomerPurchasesCalculator
{
    /// <summary>
    /// Assembles the complete dashboard. <paramref name="allInvoices"/> holds every currency;
    /// <paramref name="selectedCurrencyLines"/> are all-time product lines of the selected currency.
    /// </summary>
    public static PurchasesDashboardDto BuildDashboard(
        int customerId,
        string customerName,
        IReadOnlyList<InvoiceFinancialRow> allInvoices,
        IReadOnlyList<ProductLineRow> selectedCurrencyLines,
        CreditNoteSummaryRow? credit,
        IReadOnlyList<CatalogPriceRow> catalogPrices,
        string? catalogueCurrency,
        PurchasesQuery query,
        DateOnly today)
    {
        var (start, end) = ResolvePeriod(query, today);
        var period = new PeriodDto { Key = query.PeriodKey, From = start, To = end };

        if (allInvoices.Count == 0)
        {
            return new PurchasesDashboardDto
            {
                CustomerId = customerId,
                CustomerName = customerName,
                HasAnyInvoices = false,
                Period = period,
                Trend = BuildTrend(Array.Empty<InvoiceFinancialRow>(), today)
            };
        }

        var currencies = DistinctCurrencies(allInvoices);
        var selected = SelectCurrency(allInvoices, query, today)!;
        var rows = allInvoices.Where(row => string.Equals(row.CurrencyCode, selected, StringComparison.Ordinal)).ToList();

        bool InPeriod(DateOnly date) => (start is null || date >= start.Value) && (end is null || date <= end.Value);

        var periodInvoices = rows.Where(row => InPeriod(row.InvoiceDate)).ToList();
        var periodLines = selectedCurrencyLines.Where(line => InPeriod(line.InvoiceDate)).ToList();

        var kpis = BuildKpis(rows, query, today);
        var mix = BuildProductMix(periodLines, catalogPrices, catalogueCurrency, selected);
        var (pendingItems, pendingTotal) = BuildPendingList(rows, today);

        return new PurchasesDashboardDto
        {
            CustomerId = customerId,
            CustomerName = customerName,
            HasAnyInvoices = true,
            Period = period,
            Currencies = currencies.ToList(),
            SelectedCurrency = selected,
            ShowCurrencySwitch = currencies.Count > 1,
            Kpis = kpis,
            Trend = BuildTrend(rows, today),
            ProductMix = mix,
            Donut = BuildDonut(mix.Rows),
            Behaviour = new BehaviourDto
            {
                Rhythm = BuildRhythm(selectedCurrencyLines, today),
                Payment = BuildPaymentBehaviour(periodInvoices, kpis.Overdue.Amount, credit)
            },
            PendingInvoices = pendingItems,
            PendingTotal = pendingTotal
        };
    }
}
