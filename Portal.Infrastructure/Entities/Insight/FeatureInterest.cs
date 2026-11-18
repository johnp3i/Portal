namespace Portal.Infrastructure.Entities.Insight;

/// <summary>
/// A user's registered interest in a "coming later" feature (for example the Discounts and Price drift
/// teasers on the Customer Purchases page). One row per business, user and feature key; registering
/// the same combination again leaves the existing row unchanged.
///
/// Schema: [insight].[FeatureInterest].
/// </summary>
public class FeatureInterest
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    /// <summary>The user who registered interest (ASP.NET Identity user id).</summary>
    public string UserId { get; set; } = null!;

    /// <summary>The feature key, 1 to 50 characters (for example <c>discounts</c> or <c>price-drift</c>).</summary>
    public string FeatureKey { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    // Navigation property
    public Business Business { get; set; } = null!;
}
