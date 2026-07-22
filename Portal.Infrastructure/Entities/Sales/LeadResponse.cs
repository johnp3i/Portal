namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// A recorded response sent to a lead. Tracks channel, template used, and sender.
/// Schema: [sales].[LeadResponse]
/// </summary>
public class LeadResponse
{
    public int Id { get; set; }

    public int LeadRequestId { get; set; }

    public int LeadResponseTypeId { get; set; }

    public int? LeadResponseTemplateId { get; set; }

    public string? RespondedByUserId { get; set; }

    public string? ResponseText { get; set; }

    public bool IsAutomated { get; set; }

    public DateTime SentAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public LeadRequest LeadRequest { get; set; } = null!;

    public LeadResponseType LeadResponseType { get; set; } = null!;

    public LeadResponseTemplate? LeadResponseTemplate { get; set; }
}
