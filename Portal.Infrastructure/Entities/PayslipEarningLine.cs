namespace Portal.Infrastructure.Entities;

public class PayslipEarningLine
{
    public int Id { get; set; }
    public int PayslipId { get; set; }
    public int EarningTypeId { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public decimal? OvertimeMultiplier { get; set; }
    public decimal? OvertimeHours { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
