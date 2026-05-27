namespace Portal.Infrastructure.Models;

/// <summary>
/// Filter parameters for querying system logs. Clamping of PageNumber and PageSize
/// is handled by the service layer.
/// </summary>
public class SystemLogFilter
{
    public string? Level { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? UserId { get; set; }
    public string? CorrelationId { get; set; }
    public string? SourceContext { get; set; }
    public string? RequestPath { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
