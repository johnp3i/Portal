namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// Lookup: defines the communication channel for responding to a lead (Email, Telephone, SMS, etc.).
/// Schema: [sales].[LeadResponseType]
/// </summary>
public class LeadResponseType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;
}
