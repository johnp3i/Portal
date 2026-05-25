namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object representing a product's usage count across invoices and quotations.
/// </summary>
public class ProductUsageDto
{
    public string Description { get; set; } = null!;
    public int UsageCount { get; set; }
}
