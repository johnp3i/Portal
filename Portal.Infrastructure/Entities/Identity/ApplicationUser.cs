using Microsoft.AspNetCore.Identity;

namespace Portal.Infrastructure.Entities.Identity;

/// <summary>
/// Extends ASP.NET Core IdentityUser with portal-specific properties.
/// Stored in the Membership database.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// FK to Portal.Business — nullable for SuperAdmin accounts.
    /// </summary>
    public int? BusinessId { get; set; }

    /// <summary>
    /// User's first name.
    /// </summary>
    public string FirstName { get; set; } = null!;

    /// <summary>
    /// User's last name.
    /// </summary>
    public string LastName { get; set; } = null!;

    /// <summary>
    /// Whether the user account is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Account creation timestamp.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp of the user's most recent successful login.
    /// </summary>
    public DateTime? LastLoginUtc { get; set; }
}
