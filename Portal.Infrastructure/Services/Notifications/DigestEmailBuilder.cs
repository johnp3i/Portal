using System.Net;
using System.Text;

namespace Portal.Infrastructure.Services.Notifications;

/// <summary>
/// Builds self-contained HTML bodies for the owner-facing scheduled digests (Group 3).
/// Pure static helpers with no Web/DI dependency, mirroring <see cref="AssistantEmailBuilder"/>.
/// Owner-facing: NO customer branded footer. Always renders (empty weeks get an "all clear"
/// variant plus a short "this week at a glance" line).
/// </summary>
public static class DigestEmailBuilder
{
    public static string OutstandingBalanceSubject(string businessName)
        => $"Your weekly outstanding balance — {businessName}";

    public static string FinancialSnapshotSubject(string businessName)
        => $"Your weekly financial snapshot — {businessName}";

    // ---- Outstanding Balance Digest -------------------------------------------------------

    public static string BuildOutstandingBalanceHtml(OutstandingBalanceDigestModel m)
    {
        var cur = WebUtility.HtmlEncode(m.CurrencySymbol);
        var biz = WebUtility.HtmlEncode(m.BusinessName);
        var allClear = m.OutstandingTotal == 0m && m.UpcomingPayables.Count == 0;

        var body = new StringBuilder();

        if (allClear)
        {
            body.Append(Paragraph(
                "Nothing outstanding this week — you're on top of it. No customer balances are " +
                "waiting and no supplier payments are coming due in the period."));
        }
        else
        {
            // Receivables summary
            body.Append(SectionHeading("What you're owed"));
            body.Append(StatRow("Total outstanding", $"{cur}{m.OutstandingTotal:N2}", $"{m.OutstandingInvoiceCount} invoice(s)"));
            if (m.OverdueTotal > 0m)
                body.Append(StatRow("Overdue", $"{cur}{m.OverdueTotal:N2}", $"{m.OverdueInvoiceCount} invoice(s)", accent: "#C24A4A"));

            if (m.TopOutstanding.Count > 0)
            {
                var rows = new StringBuilder();
                foreach (var r in m.TopOutstanding)
                {
                    rows.Append($@"<tr>
                        <td style=""padding:8px 10px;border-bottom:1px solid #eef2f6;font-size:13px;color:#0B1B28;"">{WebUtility.HtmlEncode(r.CustomerName)}</td>
                        <td style=""padding:8px 10px;border-bottom:1px solid #eef2f6;font-size:13px;color:#5a6a7a;"">{WebUtility.HtmlEncode(r.InvoiceNumber)}</td>
                        <td style=""padding:8px 10px;border-bottom:1px solid #eef2f6;font-size:13px;color:#5a6a7a;"">{r.DueDate:dd MMM yyyy}</td>
                        <td style=""padding:8px 10px;border-bottom:1px solid #eef2f6;font-size:13px;color:#0B1B28;text-align:right;font-weight:700;"">{cur}{r.OutstandingBalance:N2}</td>
                    </tr>");
                }
                body.Append(Table(new[] { "Customer", "Invoice", "Due", "Outstanding" }, rows.ToString()));
            }

            // Payables summary
            body.Append(SectionHeading("Supplier payments coming due"));
            if (m.UpcomingPayables.Count == 0)
            {
                body.Append(Paragraph("No supplier payments are coming due in the period."));
            }
            else
            {
                var rows = new StringBuilder();
                foreach (var p in m.UpcomingPayables)
                {
                    var badge = StatusBadge(p.Status);
                    rows.Append($@"<tr>
                        <td style=""padding:8px 10px;border-bottom:1px solid #eef2f6;font-size:13px;color:#0B1B28;"">{WebUtility.HtmlEncode(p.SupplierName)}</td>
                        <td style=""padding:8px 10px;border-bottom:1px solid #eef2f6;font-size:13px;color:#5a6a7a;"">{p.EffectiveDueDate:dd MMM yyyy} {badge}</td>
                        <td style=""padding:8px 10px;border-bottom:1px solid #eef2f6;font-size:13px;color:#0B1B28;text-align:right;font-weight:700;"">{cur}{p.TotalAmount:N2}</td>
                    </tr>");
                }
                body.Append(Table(new[] { "Supplier", "Due", "Amount" }, rows.ToString()));
                body.Append(SmallNote(
                    "\"Coming due\" is based on each purchase's target payment date. Payment status " +
                    "is not tracked on purchases, so paid items may still appear if not cancelled."));
            }
        }

        // This week at a glance — always present
        body.Append(GlanceLine(cur, m.InvoicesIssuedThisWeek, m.IssuedAmountThisWeek, m.PaymentsReceivedThisWeek, m.CollectedThisWeek));

        return Shell("Weekly outstanding balance", "#0D5EA6", "Receivables & payables", biz, body.ToString());
    }

    // ---- Financial Snapshot ---------------------------------------------------------------

    public static string BuildFinancialSnapshotHtml(FinancialSnapshotDigestModel m)
    {
        var cur = WebUtility.HtmlEncode(m.CurrencySymbol);
        var biz = WebUtility.HtmlEncode(m.BusinessName);
        var body = new StringBuilder();

        if (!m.HasActivity)
        {
            body.Append(Paragraph(
                "A quiet week — no notable financial movement to report. Everything looks steady."));
        }

        body.Append(SectionHeading("This week"));
        foreach (var fig in m.Figures)
            body.Append(StatRow(fig.Label, $"{cur}{fig.Amount:N2}", fig.Detail, accent: fig.Accent));

        body.Append(GlanceLine(cur, m.InvoicesIssuedThisWeek, m.IssuedAmountThisWeek, m.PaymentsReceivedThisWeek, m.CollectedThisWeek));

        return Shell("Weekly financial snapshot", "#0D5EA6", "Financial position", biz, body.ToString());
    }

    // ---- Shared building blocks -----------------------------------------------------------

    private static string SectionHeading(string text) => $@"
        <h2 style=""margin:28px 0 12px 0;font-size:16px;font-weight:700;color:#0B1B28;"">{WebUtility.HtmlEncode(text)}</h2>";

    private static string Paragraph(string text) => $@"
        <p style=""margin:16px 0 0 0;font-size:15px;line-height:1.7;color:#3D4F5F;"">{WebUtility.HtmlEncode(text)}</p>";

    private static string StatRow(string label, string value, string? detail, string accent = "#0B1B28") => $@"
        <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:6px 0;"">
            <tr>
                <td style=""font-size:14px;color:#5a6a7a;"">{WebUtility.HtmlEncode(label)}{(string.IsNullOrEmpty(detail) ? "" : $@" <span style=""color:#98a8b6;"">· {WebUtility.HtmlEncode(detail)}</span>")}</td>
                <td style=""font-size:16px;font-weight:700;color:{accent};text-align:right;"">{value}</td>
            </tr>
        </table>";

    private static string Table(string[] headers, string rowsHtml)
    {
        var ths = new StringBuilder();
        foreach (var h in headers)
        {
            var align = h is "Outstanding" or "Amount" ? "right" : "left";
            ths.Append($@"<th style=""text-align:{align};font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:.04em;color:#98a8b6;padding:8px 10px;border-bottom:2px solid #e2e8f0;"">{WebUtility.HtmlEncode(h)}</th>");
        }
        return $@"
        <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin-top:8px;border-collapse:collapse;"">
            <thead><tr>{ths}</tr></thead>
            <tbody>{rowsHtml}</tbody>
        </table>";
    }

    private static string StatusBadge(string status)
    {
        var (bg, fg, label) = status switch
        {
            "overdue" => ("rgba(194,74,74,.12)", "#C24A4A", "Overdue"),
            "today" => ("rgba(200,145,46,.14)", "#C8912E", "Due today"),
            "due_soon" => ("rgba(200,145,46,.12)", "#C8912E", "Due soon"),
            _ => ("rgba(13,94,166,.10)", "#0D5EA6", "Upcoming"),
        };
        return $@"<span style=""display:inline-block;padding:2px 8px;border-radius:12px;font-size:10px;font-weight:700;background:{bg};color:{fg};"">{label}</span>";
    }

    private static string SmallNote(string text) => $@"
        <p style=""margin:10px 0 0 0;font-size:12px;line-height:1.5;color:#98a8b6;"">{WebUtility.HtmlEncode(text)}</p>";

    private static string GlanceLine(string cur, int invoicesIssued, decimal issuedAmount, int paymentsReceived, decimal collected) => $@"
        <div style=""margin-top:28px;padding-top:16px;border-top:1px solid #E2EBF3;"">
            <p style=""margin:0;font-size:13px;color:#5a6a7a;line-height:1.6;"">
                <strong style=""color:#0B1B28;"">This week at a glance:</strong>
                {invoicesIssued} invoice(s) issued ({cur}{issuedAmount:N2}) ·
                {paymentsReceived} payment(s) received ({cur}{collected:N2}).
            </p>
        </div>";

    private static string Shell(string title, string accentColor, string eyebrow, string businessName, string bodyHtml) => $@"<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8"" /><meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" /></head>
<body style=""margin:0;padding:0;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;background-color:#F2F6FA;"">
    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#F2F6FA;"">
        <tr>
            <td align=""center"" style=""padding:40px 16px;"">
                <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""max-width:600px;width:100%;background-color:#FFFFFF;border-radius:16px;overflow:hidden;"">
                    <tr><td style=""height:4px;background-color:{accentColor};""></td></tr>
                    <tr>
                        <td style=""padding:40px 40px 32px;"">
                            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                                <tr>
                                    <td style=""background-color:#EBF5FF;border-radius:20px;padding:6px 16px;"">
                                        <span style=""font-size:12px;font-weight:700;color:{accentColor};letter-spacing:0.06em;text-transform:uppercase;"">{WebUtility.HtmlEncode(eyebrow)}</span>
                                    </td>
                                </tr>
                            </table>
                            <h1 style=""margin:24px 0 0 0;font-size:24px;font-weight:700;color:#0B1B28;line-height:1.3;"">{WebUtility.HtmlEncode(title)}</h1>
                            <p style=""margin:8px 0 0 0;font-size:14px;color:#8a9bac;"">{WebUtility.HtmlEncode(businessName)}</p>
                            {bodyHtml}
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
}

// ---- Composer input models ----------------------------------------------------------------

/// <summary>Data for the Weekly Outstanding Balance Digest email.</summary>
public class OutstandingBalanceDigestModel
{
    public string BusinessName { get; set; } = "Your business";
    public string CurrencySymbol { get; set; } = "€";

    public decimal OutstandingTotal { get; set; }
    public int OutstandingInvoiceCount { get; set; }
    public decimal OverdueTotal { get; set; }
    public int OverdueInvoiceCount { get; set; }
    public List<OutstandingInvoiceLine> TopOutstanding { get; set; } = new();
    public List<UpcomingPayableLine> UpcomingPayables { get; set; } = new();

    // This week at a glance
    public int InvoicesIssuedThisWeek { get; set; }
    public decimal IssuedAmountThisWeek { get; set; }
    public int PaymentsReceivedThisWeek { get; set; }
    public decimal CollectedThisWeek { get; set; }
}

public class OutstandingInvoiceLine
{
    public string CustomerName { get; set; } = null!;
    public string InvoiceNumber { get; set; } = null!;
    public DateOnly DueDate { get; set; }
    public decimal OutstandingBalance { get; set; }
}

public class UpcomingPayableLine
{
    public string SupplierName { get; set; } = null!;
    public DateOnly EffectiveDueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "upcoming";
}

/// <summary>Data for the Weekly Financial Snapshot email.</summary>
public class FinancialSnapshotDigestModel
{
    public string BusinessName { get; set; } = "Your business";
    public string CurrencySymbol { get; set; } = "€";
    public bool HasActivity { get; set; }
    public List<SnapshotFigure> Figures { get; set; } = new();

    public int InvoicesIssuedThisWeek { get; set; }
    public decimal IssuedAmountThisWeek { get; set; }
    public int PaymentsReceivedThisWeek { get; set; }
    public decimal CollectedThisWeek { get; set; }
}

public class SnapshotFigure
{
    public string Key { get; set; } = null!;
    public string Label { get; set; } = null!;
    public decimal Amount { get; set; }
    public string? Detail { get; set; }
    public string Accent { get; set; } = "#0B1B28";
}
