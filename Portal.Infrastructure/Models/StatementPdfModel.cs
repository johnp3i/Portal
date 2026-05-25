namespace Portal.Infrastructure.Models;

/// <summary>
/// View model used for rendering the statement PDF, containing customer details, business info, and the statement data.
/// </summary>
public class StatementPdfModel
{
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerAddress { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string? BusinessLogoUrl { get; set; }
    public string CurrencySymbol { get; set; } = "€";
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public StatementResultDto Statement { get; set; } = new();
}
