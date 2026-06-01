using System.Net;

namespace Portal.Web.Services.Email;

/// <summary>
/// Builds internal notification emails sent to ask@3inventors.com when a visitor
/// submits the contact form. The email contains a simple HTML table with all
/// submitted fields, HTML-encoded to prevent XSS.
/// </summary>
public static class NotificationEmailBuilder
{
    private const string SubjectPrefix = "3 Inventors Portal — ";
    private const string DefaultInquiryType = "Contact Request";

    /// <summary>
    /// Builds the notification email subject and HTML body for the internal team.
    /// </summary>
    /// <returns>A tuple containing the email subject and the HTML body.</returns>
    public static (string Subject, string HtmlBody) Build(
        string? inquiryType,
        string? companyName,
        string? firstName,
        string? lastName,
        string? email,
        string? telephone,
        string? industry)
    {
        var effectiveInquiryType = string.IsNullOrWhiteSpace(inquiryType)
            ? DefaultInquiryType
            : inquiryType.Trim();

        var subject = $"{SubjectPrefix}{effectiveInquiryType}";

        var htmlBody = BuildHtmlBody(
            effectiveInquiryType,
            companyName,
            firstName,
            lastName,
            email,
            telephone,
            industry);

        return (subject, htmlBody);
    }

    private static string BuildHtmlBody(
        string inquiryType,
        string? companyName,
        string? firstName,
        string? lastName,
        string? email,
        string? telephone,
        string? industry)
    {
        var rows = TableRow("Inquiry Type", inquiryType)
                 + TableRow("Company Name", companyName)
                 + TableRow("First Name", firstName)
                 + TableRow("Last Name", lastName)
                 + TableRow("Email", email)
                 + TableRow("Telephone", telephone)
                 + TableRow("Industry", industry);

        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"" />
    <title>Contact Form Submission</title>
</head>
<body style=""margin:0; padding:0; font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif; background-color:#F2F6FA;"">
    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#F2F6FA;"">
        <tr>
            <td align=""center"" style=""padding:32px 16px;"">
                <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""max-width:600px; width:100%; background-color:#FFFFFF; border-radius:12px; overflow:hidden;"">
                    <tr>
                        <td style=""background-color:#0B1B28; padding:24px 32px;"">
                            <h1 style=""margin:0; font-size:18px; font-weight:700; color:#FFFFFF;"">New Contact Form Submission</h1>
                            <p style=""margin:8px 0 0 0; font-size:13px; color:#8899A6;"">3 Inventors Portal</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding:28px 32px;"">
                            <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""border:1px solid #E2EBF3; border-radius:8px; overflow:hidden;"">
                                <tr>
                                    <td style=""background-color:#F7FAFC; padding:12px 16px; border-bottom:1px solid #E2EBF3;"">
                                        <strong style=""font-size:13px; color:#0D5EA6; letter-spacing:0.05em; text-transform:uppercase;"">Submission Details</strong>
                                    </td>
                                </tr>
                                <tr>
                                    <td style=""padding:16px;"">
                                        <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                                            {rows}
                                        </table>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding:0 32px 24px 32px;"">
                            <p style=""margin:0; font-size:12px; color:#5E6D7A;"">This is an automated notification from the 3 Inventors Portal contact form.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    private static string TableRow(string label, string? value)
    {
        var encodedLabel = WebUtility.HtmlEncode(label);
        var encodedValue = string.IsNullOrWhiteSpace(value)
            ? "<span style=\"color:#8899A6;\">—</span>"
            : WebUtility.HtmlEncode(value.Trim());

        return $@"<tr>
                                                <td style=""padding:8px 0; font-size:14px; color:#5E7385; width:120px; vertical-align:top; border-bottom:1px solid #F0F4F8;"">{encodedLabel}</td>
                                                <td style=""padding:8px 0; font-size:14px; color:#0B1B28; font-weight:600; border-bottom:1px solid #F0F4F8;"">{encodedValue}</td>
                                            </tr>";
    }
}
