namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// A reusable response template used by businesses to reply to leads.
/// Schema: [sales].[LeadResponseTemplate]
/// </summary>
public class LeadResponseTemplate
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int? ProductId { get; set; }

    public int LeadResponseTypeId { get; set; }

    public string Name { get; set; } = null!;

    public string? Subject { get; set; }

    public string BodyTemplate { get; set; } = null!;

    public int ResponseTimeInHours { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public SalesProduct? Product { get; set; }

    public LeadResponseType LeadResponseType { get; set; } = null!;
}
