namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for an upcoming supplier payment row displayed on the dashboard.
/// The effective due date is TargetPaymentDate ?? SupplierDueDate.
/// </summary>
public class UpcomingSupplierPaymentDto
{
    public int PurchaseId { get; set; }
    public string SupplierName { get; set; } = null!;
    public string? Description { get; set; }
    public decimal TotalAmount { get; set; }
    public DateOnly EffectiveDueDate { get; set; }
    public DateOnly? SupplierDueDate { get; set; }
    public DateOnly? TargetPaymentDate { get; set; }

    /// <summary>
    /// One of: "overdue", "today", "due_soon", "upcoming"
    /// </summary>
    public string Status { get; set; } = null!;
}
