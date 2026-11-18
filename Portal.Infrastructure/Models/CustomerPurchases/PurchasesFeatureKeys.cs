namespace Portal.Infrastructure.Models;

/// <summary>
/// Allow-list of feature keys a user can register interest in from the purchases dashboard.
/// </summary>
public static class PurchasesFeatureKeys
{
    public const string Discounts = "discounts";
    public const string PriceDrift = "price-drift";

    public static readonly IReadOnlyList<string> All = new[] { Discounts, PriceDrift };

    /// <summary>
    /// True when the key is exactly one of the allowed keys (ordinal, case-sensitive).
    /// </summary>
    public static bool IsValid(string? key) =>
        key is not null && (string.Equals(key, Discounts, StringComparison.Ordinal)
                            || string.Equals(key, PriceDrift, StringComparison.Ordinal));
}
