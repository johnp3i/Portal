using System.Net;

namespace Portal.Web.Services.Email;

/// <summary>
/// Builds branded HTML confirmation emails for contact form submissions.
/// Two templates: Demo Request and General Inquiry, selected by inquiryType.
/// Design spec: .kiro/docs/Portal.web/email-templates/general/confirmation-email-design-guide.md
/// </summary>
public static class ConfirmationEmailBuilder
{
    private static readonly HashSet<string> DemoInquiryTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Demo Request",
        "Request a Demo"
    };

    /// <summary>
    /// Builds a confirmation email (subject + HTML body) based on inquiry type.
    /// Demo requests get a demo-specific template; everything else gets the general inquiry template.
    /// </summary>
    public static (string Subject, string HtmlBody) Build(
        string firstName,
        string? lastName = null,
        string? email = null,
        string? companyName = null,
        string? inquiryType = null,
        string? platform = null,
        string? industry = null)
    {
        var isDemo = !string.IsNullOrWhiteSpace(inquiryType)
                     && DemoInquiryTypes.Contains(inquiryType.Trim());

        var subject = isDemo
            ? "3 Inventors Portal — Demo Request Received"
            : "3 Inventors Portal — Message Received";

        var safeFirstName = Sanitize(firstName);
        var safeLastName = Sanitize(lastName);
        var safeEmail = Sanitize(email);
        var safeCompanyName = Sanitize(companyName);
        var safePlatform = Sanitize(platform);
        var safeIndustry = Sanitize(industry);

        var greeting = string.IsNullOrWhiteSpace(safeFirstName) ? "Hi there" : $"Hi {safeFirstName}";

        var badgeText = isDemo ? "Demo Request Received" : "Message Received";
        var bodyParagraphs = isDemo
            ? BuildDemoBodyParagraphs()
            : BuildGeneralBodyParagraphs();
        var detailsCard = isDemo
            ? BuildDemoDetailsCard(safePlatform, safeIndustry, safeCompanyName)
            : BuildGeneralDetailsCard(safeFirstName, safeLastName, safeEmail, safeCompanyName);
        var nextSteps = isDemo
            ? BuildDemoNextSteps()
            : BuildGeneralNextSteps();
        var closingText = isDemo
            ? BuildDemoClosing()
            : BuildGeneralClosing();

        var htmlBody = WrapInLayout(greeting, badgeText, bodyParagraphs, detailsCard, nextSteps, closingText);

        return (subject, htmlBody);
    }

    // ── Body paragraphs ──

    private static string BuildDemoBodyParagraphs()
    {
        return @"<p style=""margin:16px 0 0 0; font-size:16px; line-height:1.7; color:#3D4F5F;"">
                    Thank you for your interest in 3 Inventors. We have received your demo request and a member of our team will reach out to you within <strong>24 hours</strong> to schedule a session.
                 </p>";
    }

    private static string BuildGeneralBodyParagraphs()
    {
        return @"<p style=""margin:16px 0 0 0; font-size:16px; line-height:1.7; color:#3D4F5F;"">
                    Thank you for reaching out to 3 Inventors. We have received your inquiry and a member of our team will get back to you within <strong>24 hours</strong>.
                 </p>
                 <p style=""margin:16px 0 0 0; font-size:16px; line-height:1.7; color:#3D4F5F;"">
                    We review every message personally and will respond with the attention your inquiry deserves.
                 </p>";
    }

    // ── Details card ──

    private static string BuildDemoDetailsCard(string? platform, string? industry, string? companyName)
    {
        var rows = "";
        if (!string.IsNullOrWhiteSpace(platform))
            rows += DetailRow("Platform", platform);
        if (!string.IsNullOrWhiteSpace(industry))
            rows += DetailRow("Industry", industry);
        if (!string.IsNullOrWhiteSpace(companyName))
            rows += DetailRow("Company", companyName);

        if (string.IsNullOrEmpty(rows))
            return "";

        return WrapDetailsCard("What you requested", rows);
    }

    private static string BuildGeneralDetailsCard(string? firstName, string? lastName, string? email, string? companyName)
    {
        var fullName = $"{firstName} {lastName}".Trim();
        var rows = "";
        if (!string.IsNullOrWhiteSpace(fullName))
            rows += DetailRow("Name", fullName);
        if (!string.IsNullOrWhiteSpace(email))
            rows += DetailRow("Email", email);
        if (!string.IsNullOrWhiteSpace(companyName))
            rows += DetailRow("Company", companyName);

        if (string.IsNullOrEmpty(rows))
            return "";

        return WrapDetailsCard("Your inquiry details", rows);
    }

    // ── Next steps ──

    private static string BuildDemoNextSteps()
    {
        return WrapNextSteps("What to expect", new[]
        {
            "We will contact you to confirm a convenient time",
            "A guided walkthrough of the platform tailored to your operations",
            "Discussion of how the system fits your specific workflow"
        });
    }

    private static string BuildGeneralNextSteps()
    {
        return WrapNextSteps("What happens next", new[]
        {
            "Your inquiry is reviewed by our team",
            "We respond within 24 hours with a personalised reply",
            "If relevant, we suggest a call or meeting to discuss further"
        });
    }

    // ── Closing ──

    private static string BuildDemoClosing()
    {
        return @"<p style=""margin:0; font-size:14px; line-height:1.7; color:#3D4F5F;"">
                    If you have any questions before the demo, reply to this email or contact us at
                    <a href=""mailto:ask@3inventors.com"" style=""color:#0D5EA6; text-decoration:none; font-weight:600;"">ask@3inventors.com</a>.
                 </p>";
    }

    private static string BuildGeneralClosing()
    {
        return @"<p style=""margin:0; font-size:14px; line-height:1.7; color:#3D4F5F;"">
                    In the meantime, feel free to explore our platforms at
                    <a href=""https://www.3inventors.com"" style=""color:#0D5EA6; text-decoration:none; font-weight:600;"">www.3inventors.com</a>
                    or reply to this email with any additional details.
                 </p>";
    }

    // ── Layout wrapper ──

    private static string WrapInLayout(
        string greeting,
        string badgeText,
        string bodyParagraphs,
        string detailsCard,
        string nextSteps,
        string closingText)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"" xmlns=""http://www.w3.org/1999/xhtml"">
<head>
    <meta charset=""UTF-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"" />
    <title>{WebUtility.HtmlEncode(badgeText)}</title>
</head>
<body style=""margin:0; padding:0; background-color:#F2F6FA; font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif; -webkit-font-smoothing:antialiased;"">

    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#F2F6FA;"">
        <tr>
            <td align=""center"" style=""padding:40px 16px;"">

                <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""max-width:600px; width:100%; background-color:#FFFFFF; border-radius:16px; overflow:hidden;"">

                    <!-- Header -->
                    <tr>
                        <td style=""background-color:#F7FAFC; padding:32px 40px; text-align:center; border-bottom:1px solid #E2EBF3;"">
                            <img src=""https://www.3inventors.com/img/logo_blue_web_toolbar_oi.png"" alt=""3 Inventors"" width=""220"" style=""display:block; margin:0 auto; max-width:220px; height:auto; color:#0D5EA6; font-size:22px; font-weight:700;"" />
                            <p style=""margin:12px 0 0 0; font-size:12px; letter-spacing:0.18em; text-transform:uppercase; color:#0D5EA6; font-weight:600;"">
                                Business Management Platform
                            </p>
                        </td>
                    </tr>

                    <!-- Accent line -->
                    <tr>
                        <td style=""height:4px; background-color:#0D5EA6;""></td>
                    </tr>

                    <!-- Body -->
                    <tr>
                        <td style=""padding:40px 40px 20px 40px;"">
                            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                                <tr>
                                    <td style=""background-color:#EBF5FF; border-radius:20px; padding:6px 16px;"">
                                        <span style=""font-size:12px; font-weight:700; color:#0D5EA6; letter-spacing:0.06em; text-transform:uppercase;"">{WebUtility.HtmlEncode(badgeText)}</span>
                                    </td>
                                </tr>
                            </table>

                            <h1 style=""margin:24px 0 0 0; font-size:24px; font-weight:700; color:#0B1B28; line-height:1.3;"">
                                {greeting},
                            </h1>

                            {bodyParagraphs}
                        </td>
                    </tr>

                    <!-- Details card -->
                    {detailsCard}

                    <!-- Next steps -->
                    {nextSteps}

                    <!-- Divider -->
                    <tr>
                        <td style=""padding:0 40px;"">
                            <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                                <tr>
                                    <td style=""height:1px; background-color:#E2EBF3;""></td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Closing -->
                    <tr>
                        <td style=""padding:24px 40px 32px 40px;"">
                            {closingText}
                            <p style=""margin:20px 0 0 0; font-size:14px; color:#3D4F5F;"">
                                Kind regards,<br />
                                <strong style=""color:#0B1B28;"">The 3 Inventors Team</strong>
                            </p>
                        </td>
                    </tr>

                    <!-- Footer -->
                    <tr>
                        <td style=""background-color:#0B1B28; padding:28px 40px; text-align:center;"">
                            <p style=""margin:0 0 8px 0; font-size:14px; font-weight:700; color:#FFFFFF;"">
                                3 Inventors Limited
                            </p>
                            <p style=""margin:0 0 4px 0; font-size:12px; color:#8899A6;"">
                                Nicosia, Cyprus &nbsp;&middot;&nbsp; 700 75 700
                            </p>
                            <p style=""margin:0 0 16px 0; font-size:12px; color:#8899A6;"">
                                <a href=""https://www.3inventors.com"" style=""color:#8EDFFF; text-decoration:none;"">www.3inventors.com</a>
                            </p>
                            <p style=""margin:0; font-size:11px; color:#5E6D7A; letter-spacing:0.08em;"">
                                Knowledge &middot; Professionalism &middot; Innovation
                            </p>
                        </td>
                    </tr>

                </table>

            </td>
        </tr>
    </table>

</body>
</html>";
    }

    // ── Helpers ──

    private static string WrapDetailsCard(string title, string rows)
    {
        return $@"<tr>
        <td style=""padding:0 40px 20px 40px;"">
            <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#F7FAFC; border:1px solid #E2EBF3; border-radius:12px;"">
                <tr>
                    <td style=""padding:24px;"">
                        <p style=""margin:0 0 16px 0; font-size:13px; font-weight:700; color:#0D5EA6; letter-spacing:0.12em; text-transform:uppercase;"">
                            {WebUtility.HtmlEncode(title)}
                        </p>
                        <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                            {rows}
                        </table>
                    </td>
                </tr>
            </table>
        </td>
    </tr>";
    }

    private static string DetailRow(string label, string value)
    {
        return $@"<tr>
            <td style=""padding:6px 0; font-size:14px; color:#5E7385; width:110px; vertical-align:top;"">{WebUtility.HtmlEncode(label)}</td>
            <td style=""padding:6px 0; font-size:14px; color:#0B1B28; font-weight:600;"">{WebUtility.HtmlEncode(value)}</td>
        </tr>";
    }

    private static string WrapNextSteps(string heading, string[] steps)
    {
        var rows = "";
        for (var i = 0; i < steps.Length; i++)
        {
            rows += $@"<tr>
                <td style=""padding:8px 0; font-size:14px; line-height:1.6; color:#3D4F5F;"">
                    {i + 1}. {WebUtility.HtmlEncode(steps[i])}
                </td>
            </tr>";
        }

        return $@"<tr>
        <td style=""padding:0 40px 20px 40px;"">
            <h2 style=""margin:0 0 12px 0; font-size:16px; font-weight:700; color:#0B1B28;"">{WebUtility.HtmlEncode(heading)}</h2>
            <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                {rows}
            </table>
        </td>
    </tr>";
    }

    /// <summary>
    /// HTML-encodes user input to prevent XSS. Returns empty string for null/whitespace.
    /// </summary>
    private static string Sanitize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "";
        return WebUtility.HtmlEncode(input.Trim());
    }
}
