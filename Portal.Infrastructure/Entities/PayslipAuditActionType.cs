namespace Portal.Infrastructure.Entities;

/// <summary>
/// Lookup table for audit action types: Unlocked (1), Edited (2), Re-finalised (3).
/// Schema: [payroll].PayslipAuditActionType
/// </summary>
public class PayslipAuditActionType
{
    public byte Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
