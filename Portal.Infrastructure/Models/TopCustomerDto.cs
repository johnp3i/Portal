namespace Portal.Infrastructure.Models;

/// <summary>
/// A top customer row ranked by total invoiced amount for the home dashboard.
/// </summary>
public class TopCustomerDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public decimal TotalInvoiced { get; set; }
    public decimal TotalPaid { get; set; }
}
