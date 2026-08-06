namespace Portal.Infrastructure.Entities;

/// <summary>
/// Cross-reference between a payslip period and a compliance filing (BusinessApplication).
/// Each finalisation creates a new record (preserves history).
/// Schema: [payroll].PayslipPeriodComplianceFiling
/// </summary>
public class PayslipPeriodComplianceFiling
{
    public int Id { get; set; }
    public int PayslipPeriodId { get; set; }
    public int ComplianceFilingId { get; set; }
    public decimal ContributionTotal { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string UpdatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
