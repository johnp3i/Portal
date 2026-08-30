namespace Portal.Infrastructure.Models;

/// <summary>
/// Brief contact details for the "Import from Contacts" picker on the demo invitation Create page.
/// </summary>
public class SalesContactBriefItem
{
    public int Id { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? CompanyName { get; set; }
}
