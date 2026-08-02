namespace Portal.Infrastructure.Entities;

public class DeductionType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsPercentage { get; set; } = true;
    public byte DeductionCategoryTypeId { get; set; }
    public int? BusinessId { get; set; }
    public bool IsActive { get; set; } = true;
    public string Country { get; set; } = "CY";
    public bool IsTemplate { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
