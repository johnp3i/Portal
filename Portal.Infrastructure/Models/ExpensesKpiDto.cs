namespace Portal.Infrastructure.Models;

/// <summary>
/// Expenses KPI card data for the current month displayed on the home dashboard.
/// </summary>
public class ExpensesKpiDto
{
    public decimal TotalExpenses { get; set; }
    public int PurchaseCount { get; set; }
}
