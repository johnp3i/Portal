namespace Portal.Infrastructure.Models.Payroll;

public class PayslipAuditLogDto
{
    public int Id { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public byte ActionTypeId { get; set; }
    public string? FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    // Used for period-level grouping (not serialized to UI for payslip-level queries)
    public int PayslipId { get; set; }
    public string? EmployeeName { get; set; }
}
