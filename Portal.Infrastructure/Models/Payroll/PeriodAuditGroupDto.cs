namespace Portal.Infrastructure.Models.Payroll;

public class PeriodAuditGroupDto
{
    public int PayslipId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public List<PayslipAuditLogDto> Entries { get; set; } = new();
}
