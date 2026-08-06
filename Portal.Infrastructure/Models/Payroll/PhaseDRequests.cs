namespace Portal.Infrastructure.Models.Payroll;

/// <summary>
/// Request to create a new country deduction template (SuperAdmin only).
/// </summary>
public class CreateCountryTemplateRequest
{
    public string CountryCode { get; set; } = string.Empty;
    public string DeductionName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsPercentage { get; set; } = true;
    public byte DeductionCategoryTypeId { get; set; }
    public decimal DefaultRate { get; set; }
    public bool IsPayeDeductible { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// Request to update an existing country deduction template (SuperAdmin only).
/// </summary>
public class UpdateCountryTemplateRequest
{
    public int Id { get; set; }
    public string DeductionName { get; set; } = string.Empty;
    public decimal DefaultRate { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// Request to create a new PAYE tax band (SuperAdmin only).
/// </summary>
public class CreateTaxBandRequest
{
    public string CountryCode { get; set; } = string.Empty;
    public decimal LowerBound { get; set; }
    public decimal? UpperBound { get; set; }
    public decimal Rate { get; set; }
    public int EffectiveFromYear { get; set; }
    public int? EffectiveToYear { get; set; }
}

/// <summary>
/// Request to update an existing PAYE tax band (SuperAdmin only).
/// </summary>
public class UpdateTaxBandRequest
{
    public int Id { get; set; }
    public decimal LowerBound { get; set; }
    public decimal? UpperBound { get; set; }
    public decimal Rate { get; set; }
    public int EffectiveFromYear { get; set; }
    public int? EffectiveToYear { get; set; }
}


/// <summary>
/// Request body for toggling PAYE applicability on an employee.
/// </summary>
public class TogglePayeRequest
{
    public int EmployeeId { get; set; }
    public bool IsPayeApplicable { get; set; }
}
