namespace Portal.Infrastructure.Entities;

public class PayslipPeriod
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public byte PayslipStatusTypeId { get; set; } = 1;
    public DateTime? ProcessedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
