namespace Portal.Infrastructure.Entities;

/// <summary>
/// Tracks each payslip email send attempt.
/// Schema: [payroll].PayslipEmailLog
/// </summary>
public class PayslipEmailLog
{
    public int Id { get; set; }
    public int PayslipId { get; set; }
    public string SentByUserId { get; set; } = string.Empty;
    public string SentToEmail { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; }
    public bool IsSuccess { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
