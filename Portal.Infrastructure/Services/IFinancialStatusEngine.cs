using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Pure computation engine for invoice financial status determination.
/// ComputeOutstandingBalance and DetermineFinancialStatus are pure functions (no side effects, no async).
/// RecalculateStatusAsync orchestrates fetching data, computing, and persisting the result.
/// </summary>
public interface IFinancialStatusEngine
{
    /// <summary>
    /// Computes the outstanding balance for an invoice: TotalAmount - sum(valid payments).
    /// Only non-voided payments are included in the sum.
    /// </summary>
    decimal ComputeOutstandingBalance(decimal totalAmount, IEnumerable<Payment> payments);

    /// <summary>
    /// Computes the outstanding balance for an invoice: TotalAmount - sum(valid payments) - appliedCreditTotal.
    /// Only non-voided payments are included in the sum.
    /// </summary>
    decimal ComputeOutstandingBalance(decimal totalAmount, IEnumerable<Payment> payments, decimal appliedCreditTotal);

    /// <summary>
    /// Determines the correct InvoiceFinancialStatusTypeId based on outstanding balance,
    /// payment existence, due date, and current status.
    /// Decision tree: WrittenOff preserved → Paid → Overdue → PartiallyPaid → Unpaid.
    /// </summary>
    int DetermineFinancialStatus(decimal totalAmount, decimal outstandingBalance,
        bool hasValidPayments, DateOnly dueDate, int currentStatusId);

    /// <summary>
    /// Recalculates and persists the financial status for an invoice.
    /// Fetches payments and applied credit totals, computes balance, determines status, updates invoice.
    /// </summary>
    Task RecalculateStatusAsync(int invoiceId, int businessId);
}
