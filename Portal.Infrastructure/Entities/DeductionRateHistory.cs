namespace Portal.Infrastructure.Entities;

public class DeductionRateHistory
{
    public int Id { get; set; }
    public int DeductionTypeId { get; set; }
    public decimal Rate { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
