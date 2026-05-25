namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for product catalog KPI summary data.
/// </summary>
public class ProductKpiDto
{
    public int TotalProducts { get; set; }
    public int ActiveProducts { get; set; }
    public decimal AverageSellingPrice { get; set; }
    public string? BestSellerDescription { get; set; }
    public int BestSellerUsageCount { get; set; }
}
