namespace Portal.Infrastructure.Entities;

/// <summary>
/// Country-specific deduction template for multi-country expansion.
/// SuperAdmin manages these; businesses import copies via CountryTemplateService.
/// Schema: [payroll].CountryDeductionTemplate
/// </summary>
public class CountryDeductionTemplate
{
    public int Id { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string DeductionName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsPercentage { get; set; } = true;
    public byte DeductionCategoryTypeId { get; set; }
    public decimal DefaultRate { get; set; }
    public bool IsPayeDeductible { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}
