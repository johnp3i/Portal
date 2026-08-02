namespace Portal.Infrastructure.Entities;

public class PayslipDeductionLine
{
    public int Id { get; set; }
    public int PayslipId { get; set; }
    public int DeductionTypeId { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal Rate { get; set; }
    public decimal CalculatedAmount { get; set; }
    public byte DeductionCategoryTypeId { get; set; }
    public int DeductionRateHistoryId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
