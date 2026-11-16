using System.Text;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Builds the file name for a payslip PDF so the download and the emailed attachment always match.
/// Format: Payslip_{yyyy}-{MM}_{Surname}_{First}.pdf (e.g. Payslip_2026-10_Papamichael_Giannis.pdf).
/// Employee names are stored as a single "First Last" string, so the last word is treated as the
/// surname and everything before it as the first name.
/// </summary>
public static class PayslipFileNameBuilder
{
    public static string Build(string? employeeName, int year, int month)
    {
        var parts = (employeeName ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(Sanitize)
            .Where(p => p.Length > 0)
            .ToList();

        string surname;
        string first;
        if (parts.Count == 0)
        {
            surname = "Employee";
            first = string.Empty;
        }
        else if (parts.Count == 1)
        {
            surname = parts[0];
            first = string.Empty;
        }
        else
        {
            surname = parts[^1];
            first = string.Join("_", parts.Take(parts.Count - 1));
        }

        var name = $"Payslip_{year:D4}-{month:D2}_{surname}";
        if (first.Length > 0)
            name += "_" + first;

        return name + ".pdf";
    }

    // Keep letters (including non-Latin such as Greek), digits and hyphens; drop everything else
    // (path separators, quotes, punctuation) so the result is always a safe file name.
    private static string Sanitize(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch) || ch == '-')
                sb.Append(ch);
        }
        return sb.ToString();
    }
}