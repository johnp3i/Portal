using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for payment recording, voiding, and payment history retrieval.
/// Enforces validation rules and triggers financial status recalculation after mutations.
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly PaymentRepository _paymentRepository;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly IFinancialStatusEngine _financialStatusEngine;
    private readonly IPaymentScheduleService _paymentScheduleService;
    private readonly IPaymentAllocationEngine _allocationEngine;
    private readonly PortalDbContext _portalDbContext;

    private const int InvoiceStatusIssued = 2;

    public PaymentService(
        PaymentRepository paymentRepository,
        InvoiceRepository invoiceRepository,
        IFinancialStatusEngine financialStatusEngine,
        IPaymentScheduleService paymentScheduleService,
        IPaymentAllocationEngine allocationEngine,
        PortalDbContext portalDbContext)
    {
        _paymentRepository = paymentRepository;
        _invoiceRepository = invoiceRepository;
        _financialStatusEngine = financialStatusEngine;
        _paymentScheduleService = paymentScheduleService;
        _allocationEngine = allocationEngine;
        _portalDbContext = portalDbContext;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> RecordPaymentAsync(RecordPaymentDto dto, int businessId, string userId)
    {
        var invoice = await _invoiceRepository.GetByIdAndBusinessIdAsync(dto.InvoiceId, businessId);
        if (invoice == null)
            return ServiceResult.Fail("Invoice not found.");

        if (invoice.InvoiceStatusTypeId != InvoiceStatusIssued)
            return ServiceResult.Fail("Payments can only be recorded against issued invoices.");

        if (dto.Amount <= 0)
            return ServiceResult.Fail("Payment amount must be greater than zero.");

        var totalPaid = await _paymentRepository.GetTotalPaidAsync(dto.InvoiceId, businessId);
        var outstandingBalance = invoice.TotalAmount - totalPaid;

        if (dto.Amount > outstandingBalance)
        {
            var currencySymbol = await GetCurrencySymbolAsync(businessId);
            return ServiceResult.Fail($"Amount exceeds outstanding balance of {currencySymbol}{outstandingBalance:F2}.");
        }

        var payment = new Payment
        {
            BusinessId = businessId,
            InvoiceId = dto.InvoiceId,
            PaymentMethodTypeId = dto.PaymentMethodTypeId,
            PaymentDateUtc = dto.PaymentDateUtc,
            Amount = dto.Amount,
            Reference = dto.Reference,
            Notes = dto.Notes,
            IsVoided = false,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId
        };

        var paymentId = await _paymentRepository.InsertAsync(payment);

        await _paymentScheduleService.MatchPaymentToScheduleAsync(paymentId, dto.Amount, dto.InvoiceId, businessId, userId);
        await _financialStatusEngine.RecalculateStatusAsync(dto.InvoiceId, businessId);

        return ServiceResult.Ok(paymentId);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> VoidPaymentAsync(int paymentId, int businessId)
    {
        var payment = await _paymentRepository.GetByIdAndBusinessIdAsync(paymentId, businessId);
        if (payment == null)
            return ServiceResult.Fail("Payment not found.");

        if (payment.IsVoided)
            return ServiceResult.Fail("This payment has already been voided.");

        await _paymentRepository.VoidAsync(paymentId);

        if (payment.InvoiceId.HasValue)
        {
            await _paymentScheduleService.RevertPaymentMatchAsync(paymentId, payment.InvoiceId.Value, businessId);
            await _financialStatusEngine.RecalculateStatusAsync(payment.InvoiceId.Value, businessId);
        }

        return ServiceResult.Ok();
    }

    /// <inheritdoc />
    public async Task<List<PaymentHistoryDto>> GetPaymentHistoryAsync(int invoiceId, int businessId)
    {
        var payments = await _paymentRepository.GetAllPaymentsByInvoiceIdAsync(invoiceId, businessId);

        var paymentMethodTypes = await _portalDbContext.PaymentMethodTypes
            .Where(pmt => pmt.IsActive)
            .ToListAsync();

        var methodLookup = paymentMethodTypes.ToDictionary(pmt => pmt.Id, pmt => pmt.Name);

        return payments.Select(p => new PaymentHistoryDto
        {
            Id = p.Id,
            PaymentDateUtc = p.PaymentDateUtc,
            Amount = p.Amount,
            PaymentMethodName = methodLookup.GetValueOrDefault(p.PaymentMethodTypeId, "Unknown"),
            Reference = p.Reference,
            Notes = p.Notes,
            IsVoided = p.IsVoided,
            IsAutoAllocated = p.IsAutoAllocated,
            ParentPaymentId = p.ParentPaymentId
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<List<PaymentMethodType>> GetPaymentMethodTypesAsync()
    {
        return await _portalDbContext.PaymentMethodTypes
            .Where(pmt => pmt.IsActive)
            .OrderBy(pmt => pmt.Id)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<ServiceResult<AllocationResult>> RecordGlobalPaymentAsync(RecordGlobalPaymentDto dto, int businessId, string userId)
    {
        try
        {
            if (dto.Amount <= 0)
                return ServiceResult<AllocationResult>.Fail("Payment amount must be greater than zero.");

            if (dto.PaymentDateUtc.Date > DateTime.UtcNow.Date)
                return ServiceResult<AllocationResult>.Fail("Payment date cannot be in the future.");

            var customer = await _portalDbContext.Customers
                .Where(c => c.Id == dto.CustomerId && c.BusinessId == businessId)
                .FirstOrDefaultAsync();

            if (customer == null)
                return ServiceResult<AllocationResult>.Fail("Customer not found.");

            var outstandingInvoices = await _paymentRepository.GetOutstandingInvoicesForCustomerAsync(dto.CustomerId, businessId);

            if (dto.AllocationMode == "manual" && dto.ManualAllocations != null)
            {
                var manualSum = dto.ManualAllocations.Sum(a => a.Amount);
                if (manualSum > dto.Amount)
                    return ServiceResult<AllocationResult>.Fail("Sum of manual allocations exceeds the payment amount.");

                foreach (var alloc in dto.ManualAllocations)
                {
                    var inv = outstandingInvoices.FirstOrDefault(i => i.InvoiceId == alloc.InvoiceId);
                    if (inv == null)
                        return ServiceResult<AllocationResult>.Fail($"Invoice {alloc.InvoiceId} not found or has no outstanding balance.");
                    if (alloc.Amount > inv.OutstandingBalance)
                        return ServiceResult<AllocationResult>.Fail($"Allocation of {alloc.Amount:N2} exceeds outstanding balance of {inv.OutstandingBalance:N2} for {inv.InvoiceNumber}.");
                }
            }

            var parentPayment = new Payment
            {
                BusinessId = businessId,
                InvoiceId = null,
                ParentPaymentId = null,
                IsAutoAllocated = false,
                CustomerId = dto.CustomerId,
                CreditAmount = 0,
                PaymentMethodTypeId = dto.PaymentMethodTypeId,
                PaymentDateUtc = dto.PaymentDateUtc,
                Amount = dto.Amount,
                Reference = dto.Reference,
                Notes = dto.Notes,
                IsVoided = false,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = userId
            };

            var parentId = await _paymentRepository.InsertAsync(parentPayment);

            AllocationResult allocationResult;
            if (dto.AllocationMode == "manual" && dto.ManualAllocations != null && dto.ManualAllocations.Count > 0)
            {
                allocationResult = await _allocationEngine.AllocateManualAsync(parentId, dto.ManualAllocations, dto.Amount, businessId, userId);
            }
            else
            {
                allocationResult = await _allocationEngine.AllocateFifoAsync(parentId, dto.CustomerId, dto.Amount, businessId, userId);
            }

            return ServiceResult<AllocationResult>.Ok(allocationResult);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> VoidGlobalPaymentAsync(int paymentId, int businessId)
    {
        try
        {
            var parent = await _paymentRepository.GetByIdAndBusinessIdAsync(paymentId, businessId);
            if (parent == null)
                return ServiceResult.Fail("Payment not found.");

            if (parent.IsVoided)
                return ServiceResult.Fail("This payment has already been voided.");

            if (parent.ParentPaymentId != null)
                return ServiceResult.Fail("Cannot void a child allocation individually. Void the parent payment instead.");

            var children = await _paymentRepository.GetChildAllocationsAsync(paymentId, businessId);
            var activeChildren = children.Where(c => !c.IsVoided).ToList();
            var affectedInvoiceIds = activeChildren
                .Where(c => c.InvoiceId.HasValue)
                .Select(c => c.InvoiceId!.Value)
                .Distinct()
                .ToList();

            await _paymentRepository.VoidAsync(paymentId);
            await _paymentRepository.VoidChildrenAsync(paymentId, businessId);
            await _paymentRepository.UpdateCreditAmountAsync(paymentId, 0);

            foreach (var invoiceId in affectedInvoiceIds)
            {
                var childrenForInvoice = activeChildren.Where(c => c.InvoiceId == invoiceId).ToList();
                foreach (var child in childrenForInvoice)
                {
                    await _paymentScheduleService.RevertPaymentMatchAsync(child.Id, invoiceId, businessId);
                }
                await _financialStatusEngine.RecalculateStatusAsync(invoiceId, businessId);
            }

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<OutstandingInvoiceDto>> GetOutstandingForCustomerAsync(int customerId, int businessId)
    {
        return await _paymentRepository.GetOutstandingInvoicesForCustomerAsync(customerId, businessId);
    }

    private async Task<string> GetCurrencySymbolAsync(int businessId)
    {
        var currencySymbol = await _portalDbContext.BusinessProfiles
            .Where(bp => bp.BusinessId == businessId)
            .Select(bp => bp.CurrencySymbol)
            .FirstOrDefaultAsync();

        return currencySymbol ?? "€";
    }
}
