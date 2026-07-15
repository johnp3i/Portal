using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Allocates a global payment across outstanding invoices using FIFO or manual strategies.
/// Creates child payment records and triggers financial status recalculation for each affected invoice.
/// </summary>
public class PaymentAllocationEngine : IPaymentAllocationEngine
{
    private readonly PaymentRepository _paymentRepository;
    private readonly IFinancialStatusEngine _financialStatusEngine;
    private readonly IPaymentScheduleService _paymentScheduleService;
    private readonly PortalDbContext _dbContext;

    public PaymentAllocationEngine(
        PaymentRepository paymentRepository,
        IFinancialStatusEngine financialStatusEngine,
        IPaymentScheduleService paymentScheduleService,
        PortalDbContext dbContext)
    {
        _paymentRepository = paymentRepository;
        _financialStatusEngine = financialStatusEngine;
        _paymentScheduleService = paymentScheduleService;
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<AllocationResult> AllocateFifoAsync(int parentPaymentId, int customerId, decimal amount, int businessId, string userId)
    {
        var result = new AllocationResult();
        var remaining = amount;

        // Get outstanding invoices in FIFO order
        var outstandingInvoices = await _paymentRepository.GetOutstandingInvoicesForCustomerAsync(customerId, businessId);

        // Get the parent payment to inherit date and method
        var parent = await _paymentRepository.GetByIdAndBusinessIdAsync(parentPaymentId, businessId);
        if (parent == null)
            return result;

        foreach (var invoice in outstandingInvoices)
        {
            if (remaining <= 0)
                break;

            var allocation = Math.Min(remaining, invoice.OutstandingBalance);

            var childPayment = new Payment
            {
                BusinessId = businessId,
                InvoiceId = invoice.InvoiceId,
                ParentPaymentId = parentPaymentId,
                IsAutoAllocated = true,
                CustomerId = null,
                CreditAmount = 0,
                PaymentMethodTypeId = parent.PaymentMethodTypeId,
                PaymentDateUtc = parent.PaymentDateUtc,
                Amount = allocation,
                Reference = parent.Reference,
                Notes = $"Auto-allocated from global payment",
                IsVoided = false,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = userId
            };

            var childId = await _paymentRepository.InsertAsync(childPayment);

            // Match to instalment schedule if one exists
            await _paymentScheduleService.MatchPaymentToScheduleAsync(childId, allocation, invoice.InvoiceId, businessId, userId);

            // Recalculate financial status
            await _financialStatusEngine.RecalculateStatusAsync(invoice.InvoiceId, businessId);

            // Compute outstanding after allocation
            var outstandingAfter = invoice.OutstandingBalance - allocation;

            result.Allocations.Add(new AllocationDetail
            {
                ChildPaymentId = childId,
                InvoiceId = invoice.InvoiceId,
                InvoiceNumber = invoice.InvoiceNumber,
                AllocatedAmount = allocation,
                InvoiceOutstandingAfter = outstandingAfter
            });

            remaining -= allocation;
        }

        // Any remainder is credit
        if (remaining > 0)
        {
            result.CreditAmount = remaining;
            await _paymentRepository.UpdateCreditAmountAsync(parentPaymentId, remaining);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<AllocationResult> AllocateManualAsync(int parentPaymentId, List<ManualAllocationItem> allocations, decimal totalAmount, int businessId, string userId)
    {
        var result = new AllocationResult();

        // Get the parent payment to inherit date and method
        var parent = await _paymentRepository.GetByIdAndBusinessIdAsync(parentPaymentId, businessId);
        if (parent == null)
            return result;

        decimal totalAllocated = 0;

        foreach (var item in allocations)
        {
            var childPayment = new Payment
            {
                BusinessId = businessId,
                InvoiceId = item.InvoiceId,
                ParentPaymentId = parentPaymentId,
                IsAutoAllocated = false,
                CustomerId = null,
                CreditAmount = 0,
                PaymentMethodTypeId = parent.PaymentMethodTypeId,
                PaymentDateUtc = parent.PaymentDateUtc,
                Amount = item.Amount,
                Reference = parent.Reference,
                Notes = $"Manually allocated from global payment",
                IsVoided = false,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = userId
            };

            var childId = await _paymentRepository.InsertAsync(childPayment);

            // Match to instalment schedule if one exists
            await _paymentScheduleService.MatchPaymentToScheduleAsync(childId, item.Amount, item.InvoiceId, businessId, userId);

            // Recalculate financial status
            await _financialStatusEngine.RecalculateStatusAsync(item.InvoiceId, businessId);

            // Get the invoice number and compute outstanding after allocation
            var invoice = await _dbContext.Invoices
                .Where(i => i.Id == item.InvoiceId && i.BusinessId == businessId)
                .Select(i => new { i.InvoiceNumber, i.TotalAmount })
                .FirstOrDefaultAsync();

            var totalPaidForInvoice = await _paymentRepository.GetTotalPaidAsync(item.InvoiceId, businessId);
            var outstandingAfter = (invoice?.TotalAmount ?? 0) - totalPaidForInvoice;

            result.Allocations.Add(new AllocationDetail
            {
                ChildPaymentId = childId,
                InvoiceId = item.InvoiceId,
                InvoiceNumber = invoice?.InvoiceNumber ?? "Unknown",
                AllocatedAmount = item.Amount,
                InvoiceOutstandingAfter = outstandingAfter
            });

            totalAllocated += item.Amount;
        }

        // Any remainder is credit
        var remainder = totalAmount - totalAllocated;
        if (remainder > 0)
        {
            result.CreditAmount = remainder;
            await _paymentRepository.UpdateCreditAmountAsync(parentPaymentId, remainder);
        }

        return result;
    }
}
