namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// A contact in the sales pipeline directory, representing a person or company
/// that the business interacts with for lead generation and meetings.
/// Schema: [sales].[Contact]
/// </summary>
public class SalesContact
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

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

    // Navigation properties
    public Business Business { get; set; } = null!;

    public ICollection<LeadRequest> LeadRequests { get; set; } = new List<LeadRequest>();

    public ICollection<Meeting> Meetings { get; set; } = new List<Meeting>();
}
