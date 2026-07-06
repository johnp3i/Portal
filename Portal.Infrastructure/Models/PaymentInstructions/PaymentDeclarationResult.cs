namespace Portal.Infrastructure.Models;

/// <summary>
/// Result of a customer's payment declaration via the shared invoice page.
/// </summary>
public class PaymentDeclarationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
    public DateTime? DeclaredAtUtc { get; set; }
}
