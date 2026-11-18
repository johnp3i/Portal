using System.Globalization;
using Portal.Infrastructure.Models;

namespace Portal.Web.Models;

/// <summary>
/// Server-side text for the six KPI cards, so they are filled on first paint instead of waiting for the
/// page script. Mirrors renderKpis in wwwroot/js/customer-purchases.js; keep the two in step.
/// </summary>
public static class PurchasesKpiText
{
    public sealed record Part(string Text, string? CssClass);
    public sealed record Card(string Value, IReadOnlyList<Part> Detail);

    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    // Same symbols the browser's Intl.NumberFormat('en-US') uses; any other code is shown as "XXX 1,234".
    private static readonly Dictionary<string, string> Symbols = new(StringComparer.Ordinal)
    {
        ["EUR"] = "\u20AC", ["USD"] = "$", ["GBP"] = "\u00A3", ["JPY"] = "\u00A5", ["INR"] = "\u20B9"
    };

    public static string Money(decimal value, string? code)
    {
        var rounded = Math.Round(value, 0, MidpointRounding.AwayFromZero);
        var amount = Math.Abs(rounded).ToString("N0", En);
        var sign = rounded < 0 ? "-" : "";
        if (string.IsNullOrEmpty(code))
            return sign + amount;
        return Symbols.TryGetValue(code, out var symbol) ? sign + symbol + amount : sign + code + "\u00A0" + amount;
    }

    private static string Plural(int n, string one, string many) => n + " " + (n == 1 ? one : many);

    public static IReadOnlyDictionary<string, Card> Build(KpiDto k, string? cur)
    {
        var cards = new Dictionary<string, Card>(StringComparer.Ordinal);

        if (k.Rev12.HasPrior && k.Rev12.ChangePercent.HasValue)
        {
            var dir = k.Rev12.Direction;
            var arrow = dir == "up" ? "\u2191 " : dir == "down" ? "\u2193 " : "\u2192 ";
            var pct = Math.Abs(k.Rev12.ChangePercent.Value).ToString("0.0", En) + "%";
            cards["rev12"] = new Card(Money(k.Rev12.Value, cur), new[]
            {
                new Part(arrow + pct, dir == "up" ? "up" : dir == "down" ? "down" : null),
                new Part(" vs previous 12 months (" + Money(k.Rev12.Comparison, cur) + ")", null)
            });
        }
        else
        {
            cards["rev12"] = new Card(Money(k.Rev12.Value, cur), new[] { new Part("No prior period", null) });
        }

        var lifetime = Plural(k.Lifetime.InvoiceCount, "invoice", "invoices");
        if (k.Lifetime.FirstInvoiceDate.HasValue)
            lifetime += " since " + k.Lifetime.FirstInvoiceDate.Value.ToString("MMM yyyy", En);
        cards["lifetime"] = new Card(Money(k.Lifetime.Value, cur), new[] { new Part(lifetime + ", after credit notes", null) });

        cards["avginv"] = new Card(
            k.AvgInvoice.Value.HasValue ? Money(k.AvgInvoice.Value.Value, cur) : "No invoices",
            new[] { new Part(Plural(k.AvgInvoice.InvoiceCount, "invoice", "invoices") + " in the period", null) });

        if (k.Last.Date.HasValue && k.Last.DaysAgo.HasValue)
        {
            var days = k.Last.DaysAgo.Value;
            var detail = k.Last.Date.Value.ToString("dd MMM yyyy", En);
            if (k.Last.FrequencyDays.HasValue)
                detail += " \u00B7 buys about every " + Plural(k.Last.FrequencyDays.Value, "day", "days");
            cards["last"] = new Card(days == 0 ? "Today" : Plural(days, "day", "days") + " ago", new[] { new Part(detail, null) });
        }
        else
        {
            cards["last"] = new Card("No purchases yet", Array.Empty<Part>());
        }

        cards["pending"] = new Card(
            k.Pending.Count + " \u00B7 " + Money(k.Pending.Amount, cur),
            new[] { new Part("Outstanding after payments and credit notes", null) });

        if (k.Overdue.Count > 0)
        {
            var text = Plural(k.Overdue.Count, "invoice", "invoices");
            if (k.Overdue.DaysLate.HasValue)
                text += ", " + Plural(k.Overdue.DaysLate.Value, "day", "days") + " late";
            cards["overdue"] = new Card(Money(k.Overdue.Amount, cur), new[] { new Part(text, "down") });
        }
        else
        {
            cards["overdue"] = new Card(Money(k.Overdue.Amount, cur), new[] { new Part("Nothing overdue", null) });
        }

        return cards;
    }
}