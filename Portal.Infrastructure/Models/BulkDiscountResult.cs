namespace Portal.Infrastructure.Models;

/// <summary>
/// Result of a bulk discount operation (apply or remove), returning success status
/// and an updated totals breakdown for the document.
/// </summary>
public class BulkDiscountResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public DocumentTotalsBreakdown? Totals { get; set; }

    public static BulkDiscountResult Ok(DocumentTotalsBreakdown totals) =>
        new() { Success = true, Totals = totals };

    public static BulkDiscountResult Fail(string message) =>
        new() { Success = false, Message = message };
}
