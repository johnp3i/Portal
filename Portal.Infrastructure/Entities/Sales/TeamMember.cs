namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// A person who can be assigned to leads within the Opportunities module.
/// May optionally be linked to a portal user via UserId.
/// Schema: [sales].TeamMember
/// </summary>
public class TeamMember
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string FirstName { get; set; } = null!;
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Role { get; set; }
    public string? UserId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    // Navigation
    public Business Business { get; set; } = null!;
}
