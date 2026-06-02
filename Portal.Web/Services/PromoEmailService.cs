using Portal.Web.Services.Email;

namespace Portal.Web.Services;

/// <summary>
/// Sends branded promotional emails containing the promo code, trial duration,
/// expiry date, and a CTA button linking to the registration page.
/// Does not modify the promo code record.
/// </summary>
public class PromoEmailService : IPromoEmailService
{
    private readonly IEmailSender _emailSender;
    private readonly ILogger<PromoEmailService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PromoEmailService(IEmailSender emailSender, ILogger<PromoEmailService> logger, IHttpContextAccessor httpContextAccessor)
    {
        _emailSender = emailSender;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<bool> SendPromoCodeEmailAsync(string recipientEmail, string code, int durationMonths, DateTime expiresAtUtc, int promoCodeId)
    {
        try
        {
            var subject = "Your Exclusive Free Trial Code — Portal by 3 Inventors";
            var htmlBody = BuildPromoCodeEmailHtml(code, durationMonths, expiresAtUtc);

            await _emailSender.SendEmailAsync(recipientEmail, subject, htmlBody, EmailDepartmentEnum.PromoCode);

            _logger.LogInformation("Promo code email sent. RecipientEmail={RecipientEmail}, PromoCodeId={PromoCodeId}, Code={Code}",
                recipientEmail, promoCodeId, code);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send promo code email. RecipientEmail={RecipientEmail}, PromoCodeId={PromoCodeId}",
                recipientEmail, promoCodeId);
            return false;
        }
    }

    private string BuildPromoCodeEmailHtml(string code, int durationMonths, DateTime expiresAtUtc)
    {
        var baseUrl = GetBaseUrl();
        var registrationUrl = $"{baseUrl}/Account/Register?promoCode={System.Net.WebUtility.UrlEncode(code)}";
        var expiryFormatted = expiresAtUtc.ToString("dd MMMM yyyy");
        var durationText = durationMonths == 1 ? "1 month" : $"{durationMonths} months";

        return $@"<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8"" /><meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" /></head>
<body style=""margin:0;padding:0;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;background-color:#F2F6FA;"">
    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#F2F6FA;"">
        <tr>
            <td align=""center"" style=""padding:40px 16px;"">
                <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""max-width:600px;width:100%;background-color:#FFFFFF;border-radius:16px;overflow:hidden;"">
                    <!-- Header -->
                    <tr>
                        <td style=""background-color:#F7FAFC;padding:32px 40px;text-align:center;border-bottom:1px solid #E2EBF3;"">
                            <img src=""https://www.3inventors.com/img/logo_blue_web_toolbar_oi.png"" alt=""3 Inventors"" width=""220"" style=""display:block;margin:0 auto;max-width:220px;height:auto;"" />
                        </td>
                    </tr>
                    <!-- Accent line -->
                    <tr><td style=""height:4px;background-color:#0D5EA6;""></td></tr>
                    <!-- Body -->
                    <tr>
                        <td style=""padding:40px;"">
                            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"">
                                <tr>
                                    <td style=""background-color:#EBF5FF;border-radius:20px;padding:6px 16px;"">
                                        <span style=""font-size:12px;font-weight:700;color:#0D5EA6;letter-spacing:0.06em;text-transform:uppercase;"">Free Trial</span>
                                    </td>
                                </tr>
                            </table>
                            <h1 style=""margin:24px 0 0 0;font-size:24px;font-weight:700;color:#0B1B28;line-height:1.3;"">You've been offered a free trial!</h1>
                            <p style=""margin:16px 0 0 0;font-size:16px;line-height:1.7;color:#3D4F5F;"">
                                You have been selected to receive a complimentary <strong>{durationText}</strong> trial of the Portal Business plan — full access, no payment required.
                            </p>
                            <!-- Promo Code Display -->
                            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:28px 0;width:100%;"">
                                <tr>
                                    <td style=""padding:20px;background-color:#F7FAFC;border-radius:12px;text-align:center;"">
                                        <div style=""font-size:11px;font-weight:700;color:#0D5EA6;letter-spacing:0.08em;text-transform:uppercase;margin-bottom:8px;"">Your Promo Code</div>
                                        <div style=""font-size:32px;font-weight:700;color:#0B1B28;font-family:'Courier New',Courier,monospace;letter-spacing:4px;"">{System.Net.WebUtility.HtmlEncode(code)}</div>
                                    </td>
                                </tr>
                            </table>
                            <!-- Details -->
                            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:0 0 24px 0;width:100%;"">
                                <tr>
                                    <td style=""padding:12px 16px;background-color:#F7FAFC;border-radius:8px;"">
                                        <div style=""font-size:11px;font-weight:700;color:#0D5EA6;letter-spacing:0.08em;text-transform:uppercase;"">Trial Duration</div>
                                        <div style=""margin-top:4px;font-size:16px;font-weight:700;color:#0B1B28;"">{durationText}</div>
                                    </td>
                                </tr>
                                <tr><td style=""height:8px;""></td></tr>
                                <tr>
                                    <td style=""padding:12px 16px;background-color:#F7FAFC;border-radius:8px;"">
                                        <div style=""font-size:11px;font-weight:700;color:#0D5EA6;letter-spacing:0.08em;text-transform:uppercase;"">Code Expires</div>
                                        <div style=""margin-top:4px;font-size:16px;font-weight:700;color:#0B1B28;"">{expiryFormatted}</div>
                                    </td>
                                </tr>
                            </table>
                            <!-- CTA Button -->
                            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:28px auto 0 auto;"">
                                <tr>
                                    <td>
                                        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""border-radius:12px;overflow:hidden;"">
                                            <tr>
                                                <td style=""background-color:#0D5EA6;padding:16px 40px;text-align:center;"">
                                                    <a href=""{registrationUrl}"" style=""color:#FFFFFF;font-size:16px;font-weight:700;text-decoration:none;display:block;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;"">Register Now</a>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>
                            <p style=""margin:24px 0 0 0;font-size:13px;color:#5E7385;line-height:1.6;"">
                                Use the code above during registration or click the button to get started. This code expires on {expiryFormatted}.
                            </p>
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td style=""background-color:#0B1B28;padding:28px 40px;text-align:center;"">
                            <p style=""margin:0 0 8px 0;font-size:14px;font-weight:700;color:#FFFFFF;"">3 Inventors Limited</p>
                            <p style=""margin:0 0 4px 0;font-size:12px;color:#8899A6;"">Nicosia, Cyprus</p>
                            <p style=""margin:0;font-size:11px;color:#5E6D7A;letter-spacing:0.08em;"">Knowledge &middot; Professionalism &middot; Innovation</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    private string GetBaseUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request == null) return "";
        return $"{request.Scheme}://{request.Host}";
    }
}
