namespace Portal.Infrastructure.Entities;

/// <summary>
/// An immutable audit log entry tracking changes to payslips.
/// Schema: [payroll].PayslipAuditLog
/// </summary>
public class PayslipAuditLog
{
    public int Id { get; set; }

    public int PayslipId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public byte PayslipAuditActionTypeId { get; set; }

    public string? FieldName { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
