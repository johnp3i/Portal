namespace Portal.Infrastructure.Models;

/// <summary>
/// Result of enabling/disabling the payment instructions toggle.
/// </summary>
public class ToggleResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
}
