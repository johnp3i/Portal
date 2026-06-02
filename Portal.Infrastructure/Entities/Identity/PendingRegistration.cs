namespace Portal.Infrastructure.Entities.Identity;

/// <summary>
/// Tracks a user's selected plan between registration and email confirmation.
/// Once the user confirms their email and completes Stripe checkout, this record
/// is marked as completed.
/// </summary>
public class PendingRegistration
{
    public int Id { get; set; }

    /// <summary>
    /// FK to AspNetUsers.Id
    /// </summary>
    public string UserId { get; set; } = null!;

    /// <summary>
    /// FK to Portal.Plan.Id (cross-database reference stored as int)
    /// </summary>
    public int PlanId { get; set; }

    /// <summary>
    /// FK to Portal.dbo.PromoCode.Id (cross-database logical reference, no physical FK).
    /// Populated when the user registers with a valid promo code.
    /// </summary>
    public int? PromoCodeId { get; set; }

    /// <summary>
    /// Whether the user has completed email confirmation and Stripe checkout.
    /// </summary>
    public bool IsCompleted { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    // Navigation
    public ApplicationUser User { get; set; } = null!;
}
