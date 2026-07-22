namespace Portal.Infrastructure.Models.Sales;

public class TeamMemberDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string? LastName { get; set; }
    public string DisplayName { get; set; } = null!;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Role { get; set; }
    public string? UserId { get; set; }
    public bool IsLinkedToPortalUser { get; set; }
    public bool IsActive { get; set; }
    public int ActiveLeadCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class CreateTeamMemberRequest
{
    public string FirstName { get; set; } = null!;
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Role { get; set; }
    public string? UserId { get; set; }
}

public class UpdateTeamMemberRequest
{
    public int Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Role { get; set; }
    public string? UserId { get; set; }
}
