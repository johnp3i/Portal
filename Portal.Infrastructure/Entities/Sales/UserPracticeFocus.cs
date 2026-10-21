namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// A user's single "this week's practice focus" — one deliberately-chosen communication behaviour
/// to practise, shown on the private My Development dashboard (Call Reflection module).
///
/// Distinct from a per-call "next improvement" note on <see cref="ActivitySelfEvaluation"/>: this is
/// a standing focus, not tied to any one activity. One row per user per business; private to the author.
///
/// Schema: [sales].[UserPracticeFocus].
/// </summary>
public class UserPracticeFocus
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    /// <summary>The owner of this focus (ASP.NET Identity user id). Author-only.</summary>
    public string UserId { get; set; } = null!;

    public string Focus { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    // Navigation property
    public Business Business { get; set; } = null!;
}
