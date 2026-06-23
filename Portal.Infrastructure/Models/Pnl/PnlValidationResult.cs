namespace Portal.Infrastructure.Models;

/// <summary>
/// Result of validating a custom P&amp;L date range.
/// </summary>
public class PnlValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}
