namespace Portal.Infrastructure.Models.Sales;

/// <summary>
/// DTO for displaying a contact in the contacts list view.
/// </summary>
public class ContactListDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? CompanyName { get; set; }
    public bool IsActive { get; set; }
    public int LeadCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public string FullName => string.IsNullOrWhiteSpace(LastName) ? FirstName : $"{FirstName} {LastName}";
}

/// <summary>
/// Request model for creating a new sales contact.
/// </summary>
public class CreateContactRequest
{
    public string FirstName { get; set; } = null!;
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? CompanyName { get; set; }
    public string? JobTitle { get; set; }
    public string? Country { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Request model for updating an existing sales contact.
/// </summary>
public class UpdateContactRequest
{
    public int Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? CompanyName { get; set; }
    public string? JobTitle { get; set; }
    public string? Country { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Detailed contact view with interest history.
/// </summary>
public class ContactDetailDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? CompanyName { get; set; }
    public string? JobTitle { get; set; }
    public string? Country { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<ContactInterestDto> InterestHistory { get; set; } = new();

    public string FullName => string.IsNullOrWhiteSpace(LastName) ? FirstName : $"{FirstName} {LastName}";
}

/// <summary>
/// A lead request entry in the contact's interest history.
/// </summary>
public class ContactInterestDto
{
    public int LeadRequestId { get; set; }
    public string? ProductName { get; set; }
    public string StageName { get; set; } = null!;
    public string? StageColour { get; set; }
    public string? RequestText { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
