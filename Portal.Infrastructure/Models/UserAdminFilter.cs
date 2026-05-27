namespace Portal.Infrastructure.Models;

/// <summary>
/// Filter parameters for querying business users in the admin interface.
/// Clamping of PageNumber and PageSize is handled by the service layer.
/// </summary>
public class UserAdminFilter
{
    public string? SearchTerm { get; set; }
    public string? StatusFilter { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
