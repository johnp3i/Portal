using System.Net;

namespace Portal.Infrastructure.Services.Notifications;

/// <summary>
/// Builds self-contained HTML bodies for Digital Assistant emails. Pure static helpers with
/// no Web/DI dependency, so the Infrastructure-side producer can render bodies at write time.
/// </summary>
public static class AssistantEmailBuilder
{
    public static string ThankYouSubject(string invoiceNumber)
        => $"Thank you for your payment — {invoiceNumber}";

    /// <summary>
    /// Renders the Thank-You email. When <paramref name="includeFooter"/> is true, appends a
    /// subtle "Powered by 3 Inventors Business Portal" footer linking to <paramref name="footerUrl"/>.
    /// </summary>
    public static string BuildThankYouHtml(
        string customerName,
        decimal amount,
        string invoiceNumber,
        string businessName,
        string currencySymbol,
        bool includeFooter,
        string footerUrl)
    {
        var name = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(customerName) ? "there" : customerName);
        var biz = WebUtility.HtmlEncode(businessName);
        var inv = WebUtility.HtmlEncode(invoiceNumber);
        var amt = $"{WebUtility.HtmlEncode(currencySymbol)}{amount:N2}";

        var footer = includeFooter
            ? $@"
                    <tr>
                        <td style=""padding:0 40px 32px;"">
                            <div style=""border-top:1px solid #E2EBF3;padding-top:16px;text-align:center;"">
                                <p style=""margin:0;font-size:12px;color:#8a9bac;line-height:1.6;"">
                                    Sent via 3 Inventors Business Portal &mdash;
                                    <a href=""{WebUtility.HtmlEncode(footerUrl)}"" style=""color:#0D5EA6;font-weight:700;text-decoration:none;"">like this? Discover it &rarr;</a>
                                </p>
                            </div>
                        </td>
                    </tr>"
            : string.Empty;

        return $@"<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8"" /><meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" /></head>
<body style=""margin:0;padding:0;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;background-color:#F2F6FA;"">
    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#F2F6FA;"">
        <tr>
            <td align=""center"" style=""padding:40px 16px;"">
                <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""max-width:600px;width:100%;background-color:#FFFFFF;border-radius:16px;overflow:hidden;"">
                    <tr><td style=""height:4px;background-color:#129867;""></td></tr>
                    <tr>
                        <td style=""padding:40px 40px 8px;"">
                            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                                <tr>
                                    <td style=""background-color:#E7F6EF;border-radius:20px;padding:6px 16px;"">
                                        <span style=""font-size:12px;font-weight:700;color:#129867;letter-spacing:0.06em;text-transform:uppercase;"">Payment received</span>
                                    </td>
                                </tr>
                            </table>
                            <h1 style=""margin:24px 0 0 0;font-size:24px;font-weight:700;color:#0B1B28;line-height:1.3;"">Thank you, {name}</h1>
                            <p style=""margin:16px 0 0 0;font-size:16px;line-height:1.7;color:#3D4F5F;"">
                                We're writing to confirm that we've received your payment of
                                <strong>{amt}</strong> for invoice <strong>{inv}</strong>.
                            </p>
                            <p style=""margin:16px 0 0 0;font-size:16px;line-height:1.7;color:#3D4F5F;"">
                                Thank you for your business.
                            </p>
                            <p style=""margin:24px 0 0 0;font-size:16px;line-height:1.7;color:#3D4F5F;"">
                                Kind regards,<br /><strong>{biz}</strong>
                            </p>
                        </td>
                    </tr>
                    <tr><td style=""height:24px;""></td></tr>
                    {footer}
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }
}
