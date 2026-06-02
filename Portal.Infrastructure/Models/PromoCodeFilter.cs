namespace Portal.Infrastructure.Models;

/// <summary>
/// Filter parameters for querying promo codes with pagination and optional status filter.
/// </summary>
public class PromoCodeFilter
{
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
