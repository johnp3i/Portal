namespace Portal.Infrastructure.Models.Sales;

public class UpdateRequestDetailsRequest
{
    public int Id { get; set; }
    public string? RequestText { get; set; }
    public int? ProductId { get; set; }
    public int LeadSourceTypeId { get; set; }
    public int? LeadSourceReferenceTypeId { get; set; }
    public string? SourceUrl { get; set; }
}
