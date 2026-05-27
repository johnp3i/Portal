namespace Portal.Infrastructure.Models;

/// <summary>
/// Filter criteria for querying credit notes in the paginated list.
/// </summary>
public class CreditNoteFilterDto
{
    public int? StatusId { get; set; }
    public int? CustomerId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public string? SearchTerm { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
