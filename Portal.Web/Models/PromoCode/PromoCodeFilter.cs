namespace Portal.Web.Models.PromoCode;

public class PromoCodeFilter
{
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
