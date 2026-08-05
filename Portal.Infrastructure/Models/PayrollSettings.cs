namespace Portal.Infrastructure.Models;

/// <summary>
/// Configuration settings for the Payroll module.
/// Bound to the "Payroll" section in appsettings.json.
/// </summary>
public class PayrollSettings
{
    /// <summary>
    /// Maximum number of payslips to email in a single batch. Default: 50.
    /// </summary>
    public int BatchEmailMaxSize { get; set; } = 50;

    /// <summary>
    /// Delay in milliseconds between sending each email in a batch. Default: 500ms.
    /// Prevents SMTP rate limiting.
    /// </summary>
    public int BatchEmailDelayBetweenSendsMs { get; set; } = 500;
}
