
namespace Portal.Web.Services.Email
{
    public static class EmailFormatHelper
    {
        public static string FormatMessage(string email, string name, string surname, string telephone, string companyName, string? inquiryType = null, string? industry = null)
        {
            string fmessage = "<html>";
            fmessage += "<head>";
            fmessage += "<title></title>";
            fmessage += "</head>";
            fmessage += "<body>";
            fmessage += "<table border=0 width=95% cellpadding=0 cellspacing=0>";

            if (!string.IsNullOrWhiteSpace(inquiryType))
            {
                fmessage += "<tr>";
                fmessage += "<td class='text-right'>Inquiry Type: </td>";
                fmessage += "<td class='text-left'><b>" + inquiryType + "</b></td>";
                fmessage += "</tr>";
            }

            fmessage += "<tr>";
            fmessage += "<td class='text-right'>Email from: </td>";
            fmessage += "<td class='text-left'>" + email + "</td>";
            fmessage += "</tr>";

            fmessage += "<tr>";
            fmessage += "<td class='text-right'>Name: </td>";
            fmessage += "<td class='text-left'>" + name + "</td>";
            fmessage += "</tr>";

            fmessage += "<tr>";
            fmessage += "<td class='text-right'>Surname: </td>";
            fmessage += "<td class='text-left'>" + surname ?? "---" + "</td>";
            fmessage += "</tr>";

            fmessage += "<tr>";
            fmessage += "<td class='text-right'>Telephone: </td>";
            fmessage += "<td class='text-left'>" + telephone ?? "---" + "</td>";
            fmessage += "</tr>";

            fmessage += "<tr>";
            fmessage += "<td class='text-right'>Company Name: </td>";
            fmessage += "<td class='text-left'>" + companyName ?? "---" + "</td>";
            fmessage += "</tr>";

            if (!string.IsNullOrWhiteSpace(industry))
            {
                fmessage += "<tr>";
                fmessage += "<td class='text-right'>Industry: </td>";
                fmessage += "<td class='text-left'>" + industry + "</td>";
                fmessage += "</tr>";
            }

            fmessage += "</table>";
            fmessage += "</body>";
            fmessage += "</html>";
            return fmessage;
        }

        public static string BuildConfirmationEmailHtml(string name, string productName)
        {

            var greeting = string.IsNullOrWhiteSpace(name) ? "Hello" : $"Hello {name}";
            return $@"
                <html>
                <head><title></title></head>
                <body style=""margin:0;padding:0;font-family:Inter,Arial,Helvetica,sans-serif;background-color:#F7FBFE;"">
                    <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#F7FBFE;padding:40px 20px;"">
                    <tr>
                        <td align=""center"">
                        <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#ffffff;border-radius:16px;border:1px solid #D9EAF4;overflow:hidden;"">
                            <tr>
                            <td style=""background:linear-gradient(180deg,#0079C9 0%,#005597 100%);padding:20px 40px;text-align:center;"">
                                <h1 style=""margin:0;color:#ffffff;font-size:22px;font-weight:700;"">{productName}</h1>
                            </td>
                            </tr>
                            <tr>
                            <td style=""padding:28px 40px 40px;"">
                                <p style=""margin:0 0 20px;font-size:16px;color:#1e293b;font-weight:600;"">{greeting},</p>
                                <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#475569;"">Thank you for reaching out to us. We have received your message and our team will review it shortly.</p>
                                <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#475569;"">We will get back to you within the next 24 hours.</p>
                                <p style=""margin:0;font-size:15px;line-height:1.7;color:#475569;"">If you have any urgent questions in the meantime, feel free to reply to this email.</p>
                            </td>
                            </tr>
                            <tr>
                            <td style=""padding:0 40px;"">
                                <hr style=""border:none;border-top:1px solid #D9EAF4;margin:0;"" />
                            </td>
                            </tr>
                            <tr>
                            <td style=""padding:24px 40px 32px;text-align:center;"">
                                <p style=""margin:0 0 4px;font-size:14px;font-weight:600;color:#005597;"">{productName} Team</p>
                                <p style=""margin:0;font-size:12px;color:#94a3b8;"">Part of the 3 Inventors Operational Intelligence Platform</p>
                            </td>
                            </tr>
                        </table>
                        </td>
                    </tr>
                    </table>
                </body>
                </html>";
        }
    }
}
