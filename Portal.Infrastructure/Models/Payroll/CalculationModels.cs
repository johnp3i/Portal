using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Models.Payroll;

public class PayslipCalculationInput
{
    public Employee Employee { get; set; } = null!;
    public List<EarningLineInput> EarningLines { get; set; } = new();
    public List<DeductionTypeWithHistory> ApplicableDeductions { get; set; } = new();
    public DateTime PeriodDate { get; set; }
}

public class EarningLineInput
{
    public int EarningTypeId { get; set; }
    public string EarningTypeCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? Amount { get; set; }
    public decimal? OvertimeMultiplier { get; set; }
    public decimal? OvertimeHours { get; set; }
}

public class DeductionTypeWithHistory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsPercentage { get; set; }
    public byte DeductionCategoryTypeId { get; set; }
    public bool IsPayeDeductible { get; set; }
    public List<DeductionRateHistory> RateHistories { get; set; } = new();
}

public class PayslipCalculationResult
{
    public bool IsValid { get; set; } = true;
    public string? ValidationError { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal TotalEmployeeDeductions { get; set; }
    public decimal NetSalary { get; set; }
    public decimal TotalEmployerContributions { get; set; }
    public List<ComputedEarningLine> EarningLines { get; set; } = new();
    public List<ComputedDeductionLine> DeductionLines { get; set; } = new();
}

public class ComputedEarningLine
{
    public int EarningTypeId { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public decimal? OvertimeMultiplier { get; set; }
    public decimal? OvertimeHours { get; set; }
}

public class ComputedDeductionLine
{
    public int DeductionTypeId { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal Rate { get; set; }
    public decimal CalculatedAmount { get; set; }
    public byte DeductionCategoryTypeId { get; set; }
    public int? DeductionRateHistoryId { get; set; }
}
