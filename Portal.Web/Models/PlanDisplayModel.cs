namespace Portal.Web.Models;

public class PlanDisplayModel
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public decimal MonthlyPriceEur { get; set; }
    public string? Description { get; set; }
}
