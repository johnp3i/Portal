namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for quotation list display.
/// </summary>
public class QuotationListDto
{
    public int Id { get; set; }
    public string Reference { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public string StatusName { get; set; } = null!;
    public int QuotationStatusTypeId { get; set; }
    public decimal TotalAmount { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool IsExpired { get; set; }
}
