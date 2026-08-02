namespace Portal.Infrastructure.Models.Payroll;

public class DeductionTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsPercentage { get; set; }
    public byte DeductionCategoryTypeId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string Country { get; set; } = string.Empty;
    public decimal? CurrentRate { get; set; }
}

public class CreateDeductionTypeRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsPercentage { get; set; } = true;
    public byte DeductionCategoryTypeId { get; set; }
    public string Country { get; set; } = "CY";
    public decimal InitialRate { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
}

public class DeductionRateHistoryDto
{
    public int Id { get; set; }
    public decimal Rate { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public bool IsCurrent => EffectiveToUtc == null;
}

public class AddRateHistoryRequest
{
    public int DeductionTypeId { get; set; }
    public decimal Rate { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
}
