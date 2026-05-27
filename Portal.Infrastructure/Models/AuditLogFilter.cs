namespace Portal.Infrastructure.Models;

/// <summary>
/// Filter parameters for querying the audit log. Clamping of PageNumber and PageSize
/// is handled by the service layer — no validation attributes are applied here.
/// </summary>
public class AuditLogFilter
{
    public string? TableName { get; set; }
    public string? Action { get; set; }
    public string? UserId { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
