using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services.CustomerPurchases;

/// <summary>
/// Product mix, donut and drill-down calculations. Never uses product type or price tier.
/// Callers pass lines already restricted to the Selected_Currency (and, for the mix, the period).
/// </summary>
public static partial class CustomerPurchasesCalculator
{
    private const int DonutTopCount = 5;
    private const int DrillDownMaxLines = 200;

    /// <summary>Group kind ("C" for code, "D" for description) and the upper-cased trimmed key.</summary>
    public static (string Kind, string Key) GroupKey(string? code, string? description)
    {
        var trimmedCode = code?.Trim();
        if (!string.IsNullOrEmpty(trimmedCode))
        {
            return ("C", trimmedCode.ToUpperInvariant());
        }

        return ("D", (description ?? "").Trim().ToUpperInvariant());
    }

    /// <summary>Opaque group key string: <c>C:&lt;UPPER code&gt;</c> or <c>D:&lt;UPPER description&gt;</c>.</summary>
    public static string GroupKeyString(string? code, string? description)
    {
        var (kind, key) = GroupKey(code, description);
        return kind + ":" + key;
    }

    private static decimal Share(decimal part, decimal total)
    {
        if (total == 0m)
        {
            return 0.0m;
        }

        return Math.Round(part / total * 100m, 1, MidpointRounding.AwayFromZero);
    }

    /// <summary>Builds the product mix from lines in the Selected_Currency within the period.</summary>
    public static ProductMixDto BuildProductMix(
        IEnumerable<ProductLineRow> lines,
        IEnumerable<CatalogPriceRow> catalogPrices,
        string? catalogueCurrency,
        string selectedCurrency)
    {
        var lineList = lines.ToList();
        var totalRevenue = lineList.Sum(line => line.LineTotal);

        var useCatalogue = catalogueCurrency is not null
                           && string.Equals(catalogueCurrency, selectedCurrency, StringComparison.Ordinal);
        var priceByCode = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (useCatalogue)
        {
            foreach (var price in catalogPrices)
            {
                var code = price.ProductCode?.Trim();
                if (!string.IsNullOrEmpty(code) && price.DefaultSellingPrice > 0m)
                {
                    priceByCode[code] = price.DefaultSellingPrice;
                }
            }
        }

        var rows = new List<ProductRowDto>();
        foreach (var group in lineList.GroupBy(line => GroupKeyString(line.ProductCode, line.Description), StringComparer.Ordinal))
        {
            var groupLines = group.ToList();
            var isCode = group.Key.StartsWith("C:", StringComparison.Ordinal);
            var latestDate = groupLines.Max(line => line.InvoiceDate);
            var displayName = groupLines
                .Where(line => line.InvoiceDate == latestDate)
                .Select(line => line.Description ?? "")
                .OrderBy(text => text, StringComparer.OrdinalIgnoreCase)
                .ThenBy(text => text, StringComparer.Ordinal)
                .First();

            var quantity = groupLines.Sum(line => line.Quantity);
            var revenue = groupLines.Sum(line => line.LineTotal);

            decimal? averagePrice = quantity > 0m ? revenue / quantity : null;

            decimal? listPrice = null;
            decimal? priceDifference = null;
            var codeKey = isCode ? group.Key.Substring(2) : null;
            if (codeKey is not null && priceByCode.TryGetValue(codeKey, out var catalogue))
            {
                listPrice = catalogue;
                if (averagePrice is not null)
                {
                    priceDifference = Math.Round((averagePrice.Value - catalogue) / catalogue * 100m, 1, MidpointRounding.AwayFromZero);
                }
            }

            var costed = groupLines.Where(line => line.CostPrice is not null).ToList();
            var costedTotal = costed.Sum(line => line.LineTotal);
            decimal? margin = null;
            if (costed.Count > 0 && costedTotal != 0m)
            {
                var cost = costed.Sum(line => line.CostPrice!.Value * line.Quantity);
                margin = Math.Round((costedTotal - cost) / costedTotal * 100m, 1, MidpointRounding.AwayFromZero);
            }

            var isPartial = false;
            if (revenue != 0m)
            {
                var coverage = costedTotal / revenue;
                isPartial = coverage > 0m && coverage < 1m;
            }

            rows.Add(new ProductRowDto
            {
                GroupKey = group.Key,
                DisplayName = displayName,
                Code = isCode ? codeKey : null,
                IsCustomItem = !isCode,
                Quantity = quantity,
                Revenue = revenue,
                SharePercent = Share(revenue, totalRevenue),
                AveragePricePaid = averagePrice,
                ListPrice = listPrice,
                PriceDifferencePercent = priceDifference,
                LastBought = latestDate,
                MarginPercent = margin,
                IsMarginPartial = isPartial
            });
        }

        var ordered = rows
            .OrderByDescending(row => row.Revenue)
            .ThenBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.GroupKey, StringComparer.Ordinal)
            .ToList();

        decimal? marginCoverage = null;
        if (totalRevenue != 0m)
        {
            var costedAll = lineList.Where(line => line.CostPrice is not null).Sum(line => line.LineTotal);
            marginCoverage = Math.Round(costedAll / totalRevenue * 100m, 1, MidpointRounding.AwayFromZero);
        }

        return new ProductMixDto
        {
            Rows = ordered,
            TotalRevenue = totalRevenue,
            MarginCoveragePercent = marginCoverage
        };
    }

    /// <summary>Top 5 rows plus one "Other" slice when there are more than 5 rows. Revenues sum to the total.</summary>
    public static List<DonutSliceDto> BuildDonut(IReadOnlyList<ProductRowDto> rows)
    {
        var total = rows.Sum(row => row.Revenue);
        var slices = rows
            .Take(DonutTopCount)
            .Select(row => new DonutSliceDto
            {
                Label = row.DisplayName,
                Revenue = row.Revenue,
                SharePercent = Share(row.Revenue, total),
                IsOther = false
            })
            .ToList();

        if (rows.Count > DonutTopCount)
        {
            var otherRevenue = rows.Skip(DonutTopCount).Sum(row => row.Revenue);
            slices.Add(new DonutSliceDto
            {
                Label = "Other",
                Revenue = otherRevenue,
                SharePercent = Share(otherRevenue, total),
                IsOther = true
            });
        }

        return slices;
    }

    /// <summary>
    /// Drill-down lines of one product group within the period, newest first, capped at 200.
    /// Lines whose invoice is not in <paramref name="invoices"/> are skipped.
    /// </summary>
    public static ProductPurchaseLinesDto BuildDrillDown(
        IEnumerable<ProductLineRow> lines,
        IEnumerable<InvoiceFinancialRow> invoices,
        string productKey,
        DateOnly? periodStart,
        DateOnly? periodEnd,
        DateOnly today)
    {
        var invoiceById = new Dictionary<int, InvoiceFinancialRow>();
        foreach (var invoice in invoices)
        {
            invoiceById[invoice.Id] = invoice;
        }

        var matching = lines
            .Where(line => (periodStart is null || line.InvoiceDate >= periodStart.Value)
                           && (periodEnd is null || line.InvoiceDate <= periodEnd.Value)
                           && string.Equals(GroupKeyString(line.ProductCode, line.Description), productKey, StringComparison.Ordinal)
                           && invoiceById.ContainsKey(line.InvoiceId))
            .OrderByDescending(line => line.InvoiceDate)
            .ThenByDescending(line => line.InvoiceId)
            .ThenByDescending(line => line.LineId)
            .ToList();

        var result = matching
            .Take(DrillDownMaxLines)
            .Select(line =>
            {
                var invoice = invoiceById[line.InvoiceId];
                return new ProductPurchaseLineDto
                {
                    Date = line.InvoiceDate,
                    InvoiceId = line.InvoiceId,
                    InvoiceNumber = invoice.InvoiceNumber,
                    Quantity = line.Quantity,
                    LineTotal = line.LineTotal,
                    Status = LineStatus(invoice, today)
                };
            })
            .ToList();

        return new ProductPurchaseLinesDto
        {
            TotalLineCount = matching.Count,
            Lines = result
        };
    }
}
