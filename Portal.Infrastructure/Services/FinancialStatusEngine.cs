using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Pure computation engine for invoice financial status determination.
/// ComputeOutstandingBalance and DetermineFinancialStatus are pure functions with no side effects.
/// RecalculateStatusAsync orchestrates the full recalculation workflow.
/// </summary>
public class FinancialStatusEngine : IFinancialStatusEngine
{
    private readonly PaymentRepository _paymentRepository;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly CreditNoteRepository _creditNoteRepository;

    // Financial Status Type IDs
    private const int StatusUnpaid = 1;
    private const int StatusPartiallyPaid = 2;
    private const int StatusPaid = 3;
    private const int StatusOverdue = 4;
    private const int StatusWrittenOff = 5;

    public FinancialStatusEngine(
        PaymentRepository paymentRepository,
        InvoiceRepository invoiceRepository,
        CreditNoteRepository creditNoteRepository)
    {
        _paymentRepository = paymentRepository;
        _invoiceRepository = invoiceRepository;
        _creditNoteRepository = creditNoteRepository;
    }

    /// <inheritdoc />
    public decimal ComputeOutstandingBalance(decimal totalAmount, IEnumerable<Payment> payments)
    {
        return ComputeOutstandingBalance(totalAmount, payments, 0m);
    }

    /// <inheritdoc />
    public decimal ComputeOutstandingBalance(decimal totalAmount, IEnumerable<Payment> payments, decimal appliedCreditTotal)
    {
        var validPaymentSum = payments
            .Where(p => !p.IsVoided)
            .Sum(p => p.Amount);

        return totalAmount - validPaymentSum - appliedCreditTotal;
    }

    /// <inheritdoc />
    public int DetermineFinancialStatus(decimal totalAmount, decimal outstandingBalance,
        bool hasValidPayments, DateOnly dueDate, int currentStatusId)
    {
        // WrittenOff is always preserved — never overridden by automatic recalculation
        if (currentStatusId == StatusWrittenOff)
            return StatusWrittenOff;

        // Fully paid: no outstanding balance and at least one valid payment or credit exists
        if (outstandingBalance == 0 && hasValidPayments)
            return StatusPaid;

        // Overdue: has outstanding balance and past due date
        // DESIGN DECISION: Overdue takes priority over PartiallyPaid. An invoice that is both
        // partially paid AND past due is classified as Overdue — the urgency of the overdue state
        // is more actionable for the business than the "partially paid" information.
        if (outstandingBalance > 0 && dueDate < DateOnly.FromDateTime(DateTime.UtcNow))
            return StatusOverdue;

        // Partially paid: has outstanding balance, has valid payments, and not yet overdue
        if (outstandingBalance > 0 && hasValidPayments && dueDate >= DateOnly.FromDateTime(DateTime.UtcNow))
            return StatusPartiallyPaid;

        // Unpaid: full amount outstanding and not yet overdue
        // NOTE: An invoice with partial credit notes applied (outstanding < totalAmount) but no payments
        // and not yet overdue will fall through to the default "Unpaid" status. This is semantically
        // correct — the invoice hasn't been paid, even though credits reduced the balance.
        if (outstandingBalance == totalAmount && dueDate >= DateOnly.FromDateTime(DateTime.UtcNow))
            return StatusUnpaid;

        // Default: Unpaid (covers edge cases like partial credits with no payments, not overdue)
        return StatusUnpaid;
    }

    /// <inheritdoc />
    public async Task RecalculateStatusAsync(int invoiceId, int businessId)
    {
        // Fetch the invoice to get TotalAmount, DueDate, and current status
        var invoice = await _invoiceRepository.GetByIdAndBusinessIdAsync(invoiceId, businessId);
        if (invoice == null)
            return;

        // Fetch all valid (non-voided) payments for this invoice
        var validPayments = await _paymentRepository.GetValidPaymentsByInvoiceIdAsync(invoiceId, businessId);

        // Fetch total applied credit notes for this invoice
        var appliedCreditTotal = await _creditNoteRepository.GetTotalAppliedCreditAsync(invoiceId, businessId);

        // Compute outstanding balance including applied credits
        var outstandingBalance = ComputeOutstandingBalance(invoice.TotalAmount, validPayments, appliedCreditTotal);

        // Determine the correct financial status
        var hasValidPayments = validPayments.Count > 0 || appliedCreditTotal > 0;
        var newStatusId = DetermineFinancialStatus(
            invoice.TotalAmount,
            outstandingBalance,
            hasValidPayments,
            invoice.DueDate,
            invoice.InvoiceFinancialStatusTypeId);

        // Update the invoice's financial status if it changed
        if (newStatusId != invoice.InvoiceFinancialStatusTypeId)
        {
            await _invoiceRepository.UpdateFinancialStatusAsync(invoiceId, newStatusId);
        }
    }
}
