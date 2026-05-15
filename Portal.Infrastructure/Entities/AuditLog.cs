namespace Portal.Infrastructure.Entities;

/// <summary>
/// A record tracking data changes across the platform. Append-only — no UPDATE or DELETE permitted.
/// Schema: [audit].AuditLog
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    public int? BusinessId { get; set; }

    public string? UserId { get; set; }

    public string Action { get; set; } = null!;

    public string TableName { get; set; } = null!;

    public string RecordId { get; set; } = null!;

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public DateTime Timestamp { get; set; }

    // Navigation properties
    public Business? Business { get; set; }
}
