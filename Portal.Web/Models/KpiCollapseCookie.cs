namespace Portal.Web.Models;

/// <summary>
/// Reads the <c>cp_kpi_collapsed</c> cookie that stores which KPI cards on the
/// Customer Purchases page are collapsed. Parsing is server-side so the first paint is correct.
/// </summary>
public static class KpiCollapseCookie
{
    public const string Name = "cp_kpi_collapsed";

    /// <summary>The six valid KPI identifiers, matched case-sensitively.</summary>
    public static readonly IReadOnlySet<string> ValidIds =
        new HashSet<string>(StringComparer.Ordinal) { "rev12", "lifetime", "avginv", "last", "pending", "overdue" };

    /// <summary>
    /// Parses the raw cookie value into the set of valid collapsed KPI ids.
    /// Unknown, empty and differently cased entries are dropped. Never throws.
    /// </summary>
    public static HashSet<string> Parse(string? raw)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            if (string.IsNullOrEmpty(raw))
            {
                return result;
            }

            if (raw.Contains('%'))
            {
                try
                {
                    raw = Uri.UnescapeDataString(raw);
                }
                catch (Exception ex)
                {
                    return result;
                }
            }

            foreach (var part in raw.Split(','))
            {
                if (ValidIds.Contains(part))
                {
                    result.Add(part);
                }
            }
        }
        catch (Exception ex)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return result;
    }
}
