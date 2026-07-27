namespace Portal.Infrastructure.Models;

/// <summary>
/// Represents a single business row in the Business Insights admin view,
/// with aggregated activity counts and subscription metadata.
/// </summary>
public class BusinessInsightDto
{
    public int BusinessId { get; set; }
    public string BusinessName { get; set; } = null!;
    public string OwnerFullName { get; set; } = null!;
    public string OwnerEmail { get; set; } = null!;
    public bool IsEmailConfirmed { get; set; }
    public string PlanName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public int QuotationCount { get; set; }
    public int InvoiceCount { get; set; }
    public int PurchaseCount { get; set; }
    public decimal RevenueTotal { get; set; }
    public DateTime? LastActivityUtc { get; set; }
}
