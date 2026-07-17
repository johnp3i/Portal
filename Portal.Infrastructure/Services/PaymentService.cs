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
    private readonly CreditNoteRepository _creditNoteRepository;
    private readonly IFinancialStatusEngine _financialStatusEngine;
    private readonly IPaymentScheduleService _paymentScheduleService;
    private readonly IPaymentAllocationEngine _allocationEngine;
    private readonly IPaymentReceiptService? _receiptService;
    private readonly PortalDbContext _portalDbContext;

    private const int InvoiceStatusIssued = 2;

    public PaymentService(
        PaymentRepository paymentRepository,
        InvoiceRepository invoiceRepository,
        CreditNoteRepository creditNoteRepository,
        IFinancialStatusEngine financialStatusEngine,
        IPaymentScheduleService paymentScheduleService,
        IPaymentAllocationEngine allocationEngine,
        PortalDbContext portalDbContext,
        IPaymentReceiptService? receiptService = null)
    {
        _paymentRepository = paymentRepository;
        _invoiceRepository = invoiceRepository;
        _creditNoteRepository = creditNoteRepository;
        _financialStatusEngine = financialStatusEngine;
        _paymentScheduleService = paymentScheduleService;
        _allocationEngine = allocationEngine;
        _portalDbContext = portalDbContext;
        _receiptService = receiptService;
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
        var totalCredited = await _creditNoteRepository.GetTotalAppliedCreditAsync(dto.InvoiceId, businessId);
        var outstandingBalance = invoice.TotalAmount - totalPaid - totalCredited;

        if (dto.Amount > outstandingBalance)
        {
            var currencySymbol = await GetCurrencySymbolAsync(businessId);
            return ServiceResult.Fail($"Amount exceeds outstanding balance of {currencySymbol}{outstandingBalance:F2}.");
        }

        var payment = new Payment
        {
            BusinessId = businessId,
            InvoiceId = dto.InvoiceId,
            ParentPaymentId = null,
            IsAutoAllocated = false,
            CustomerId = null,
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

        var paymentId = await _paymentRepository.InsertAsync(payment);

        await _paymentScheduleService.MatchPaymentToScheduleAsync(paymentId, dto.Amount, dto.InvoiceId, businessId, userId);
        await _financialStatusEngine.RecalculateStatusAsync(dto.InvoiceId, businessId);

        // Auto-generate receipt if enabled
        await TryAutoGenerateReceiptAsync(paymentId, businessId, userId);

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

        // Defensively zero CreditAmount to prevent stale credit on voided payments
        if (payment.CreditAmount > 0)
            await _paymentRepository.UpdateCreditAmountAsync(paymentId, 0);

        if (payment.InvoiceId.HasValue)
        {
            await _paymentScheduleService.RevertPaymentMatchAsync(paymentId, payment.InvoiceId.Value, businessId);
            await _financialStatusEngine.RecalculateStatusAsync(payment.InvoiceId.Value, businessId);
        }

        // Void associated receipt (cascade)
        if (_receiptService != null)
            await _receiptService.VoidByPaymentIdAsync(paymentId, businessId);

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

            using var transaction = await _portalDbContext.Database.BeginTransactionAsync();
            try
            {
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

                await transaction.CommitAsync();

                // Auto-generate receipt if enabled
                await TryAutoGenerateReceiptAsync(parentId, businessId, userId);

                return ServiceResult<AllocationResult>.Ok(allocationResult);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
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

            if (parent.InvoiceId != null && parent.CustomerId == null)
                return ServiceResult.Fail("This is a standalone payment. Use the standard void.");

            var children = await _paymentRepository.GetChildAllocationsAsync(paymentId, businessId);
            var activeChildren = children.Where(c => !c.IsVoided).ToList();
            var affectedInvoiceIds = activeChildren
                .Where(c => c.InvoiceId.HasValue)
                .Select(c => c.InvoiceId!.Value)
                .Distinct()
                .ToList();

            using var transaction = await _portalDbContext.Database.BeginTransactionAsync();
            try
            {
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

                await transaction.CommitAsync();

                // Void associated receipt (cascade)
                if (_receiptService != null)
                    await _receiptService.VoidByPaymentIdAsync(paymentId, businessId);

                return ServiceResult.Ok();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
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

    /// <inheritdoc />
    public async Task<decimal> GetCreditBalanceAsync(int customerId, int businessId)
    {
        return await _portalDbContext.Payments
            .Where(p => p.BusinessId == businessId
                && p.CustomerId == customerId
                && !p.IsVoided
                && p.CreditAmount > 0)
            .SumAsync(p => (decimal?)p.CreditAmount) ?? 0m;
    }

    /// <inheritdoc />
    public async Task<ServiceResult<AllocationResult>> ApplyCreditAsync(int customerId, int businessId, string userId)
    {
        try
        {
            using var transaction = await _portalDbContext.Database.BeginTransactionAsync();
            try
            {
                // Read credit payments INSIDE transaction with UPDLOCK to prevent double-spend
                var creditPayments = await _portalDbContext.Payments
                    .FromSqlRaw(@"
                        SELECT * FROM [revenue].[Payment] WITH (UPDLOCK)
                        WHERE [BusinessId] = {0}
                          AND [CustomerId] = {1}
                          AND [IsVoided] = 0
                          AND [CreditAmount] > 0
                        ORDER BY [CreatedAtUtc] ASC", businessId, customerId)
                    .ToListAsync();

                if (creditPayments.Count == 0 || creditPayments.Sum(p => p.CreditAmount) == 0)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult<AllocationResult>.Fail("No credit available for this customer.");
                }

                // Get outstanding invoices
                var outstandingInvoices = await _paymentRepository.GetOutstandingInvoicesForCustomerAsync(customerId, businessId);
                if (outstandingInvoices.Count == 0)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult<AllocationResult>.Fail("No outstanding invoices to apply credit to.");
                }

                var result = new AllocationResult();
                var remaining = creditPayments.Sum(p => p.CreditAmount);

                foreach (var invoice in outstandingInvoices)
                {
                    if (remaining <= 0) break;

                    var allocation = Math.Min(remaining, invoice.OutstandingBalance);
                    var runningOutstanding = invoice.OutstandingBalance;

                    // Find the credit payment to draw from (oldest first)
                    foreach (var creditPayment in creditPayments)
                    {
                        if (allocation <= 0) break;
                        if (creditPayment.CreditAmount <= 0) continue;

                        var drawAmount = Math.Min(allocation, creditPayment.CreditAmount);

                        // Create child allocation
                        var child = new Payment
                        {
                            BusinessId = businessId,
                            InvoiceId = invoice.InvoiceId,
                            ParentPaymentId = creditPayment.Id,
                            IsAutoAllocated = true,
                            CustomerId = null,
                            CreditAmount = 0,
                            PaymentMethodTypeId = creditPayment.PaymentMethodTypeId,
                            PaymentDateUtc = DateTime.UtcNow,
                            Amount = drawAmount,
                            Reference = creditPayment.Reference,
                            Notes = "Applied from credit balance",
                            IsVoided = false,
                            CreatedAtUtc = DateTime.UtcNow,
                            CreatedByUserId = userId
                        };

                        var childId = await _paymentRepository.InsertAsync(child);

                        // Match to instalment schedule for each child allocation
                        await _paymentScheduleService.MatchPaymentToScheduleAsync(childId, drawAmount, invoice.InvoiceId, businessId, userId);

                        // Reduce credit on parent
                        creditPayment.CreditAmount -= drawAmount;
                        await _portalDbContext.Database.ExecuteSqlRawAsync(
                            "UPDATE [revenue].[Payment] SET [CreditAmount] = @CreditAmount WHERE [Id] = @Id",
                            new Microsoft.Data.SqlClient.SqlParameter("@CreditAmount", creditPayment.CreditAmount),
                            new Microsoft.Data.SqlClient.SqlParameter("@Id", creditPayment.Id));

                        runningOutstanding -= drawAmount;

                        result.Allocations.Add(new AllocationDetail
                        {
                            ChildPaymentId = childId,
                            InvoiceId = invoice.InvoiceId,
                            InvoiceNumber = invoice.InvoiceNumber,
                            AllocatedAmount = drawAmount,
                            InvoiceOutstandingAfter = runningOutstanding
                        });

                        allocation -= drawAmount;
                        remaining -= drawAmount;
                    }

                    // Recalculate invoice status
                    await _financialStatusEngine.RecalculateStatusAsync(invoice.InvoiceId, businessId);
                }

                result.CreditAmount = creditPayments.Sum(p => p.CreditAmount);
                await transaction.CommitAsync();

                return ServiceResult<AllocationResult>.Ok(result);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> VoidChildAllocationAsync(int paymentId, int businessId)
    {
        try
        {
            var payment = await _paymentRepository.GetByIdAndBusinessIdAsync(paymentId, businessId);
            if (payment == null)
                return ServiceResult.Fail("Payment not found.");

            if (payment.IsVoided)
                return ServiceResult.Fail("This payment has already been voided.");

            if (payment.ParentPaymentId == null)
                return ServiceResult.Fail("This is not a child allocation. Use the standard void for standalone or parent payments.");

            using var transaction = await _portalDbContext.Database.BeginTransactionAsync();
            try
            {
                // Void the child
                await _paymentRepository.VoidAsync(paymentId);

                // Recalculate invoice status
                if (payment.InvoiceId.HasValue)
                {
                    await _paymentScheduleService.RevertPaymentMatchAsync(paymentId, payment.InvoiceId.Value, businessId);
                    await _financialStatusEngine.RecalculateStatusAsync(payment.InvoiceId.Value, businessId);
                }

                // Check if all children of the parent are now voided
                var parentId = payment.ParentPaymentId.Value;
                var parent = await _paymentRepository.GetByIdAndBusinessIdAsync(parentId, businessId);
                if (parent != null && !parent.IsVoided)
                {
                    var siblings = await _paymentRepository.GetChildAllocationsAsync(parentId, businessId);
                    var hasActiveChildren = siblings.Any(c => !c.IsVoided);

                    if (!hasActiveChildren)
                    {
                        // All children voided — auto-void the parent and zero its credit
                        await _paymentRepository.VoidAsync(parentId);
                        await _paymentRepository.UpdateCreditAmountAsync(parentId, 0);
                    }
                    else
                    {
                        // Some children still active — return amount to parent's credit
                        await _paymentRepository.UpdateCreditAmountAsync(parentId, parent.CreditAmount + payment.Amount);
                    }
                }

                await transaction.CommitAsync();
                return ServiceResult.Ok();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> VoidPaymentSmartAsync(int paymentId, int businessId)
    {
        try
        {
            var payment = await _paymentRepository.GetByIdAndBusinessIdAsync(paymentId, businessId);
            if (payment == null)
                return ServiceResult.Fail("Payment not found.");

            if (payment.IsVoided)
                return ServiceResult.Fail("This payment has already been voided.");

            // Detect type and route accordingly
            if (payment.ParentPaymentId != null)
            {
                // Child allocation — void individually and return to credit
                return await VoidChildAllocationAsync(paymentId, businessId);
            }
            else if (payment.InvoiceId == null && payment.CustomerId != null)
            {
                // Parent (global) payment — cascade void
                return await VoidGlobalPaymentAsync(paymentId, businessId);
            }
            else
            {
                // Standalone per-invoice payment
                return await VoidPaymentAsync(paymentId, businessId);
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private async Task<string> GetCurrencySymbolAsync(int businessId)
    {
        var currencySymbol = await _portalDbContext.BusinessProfiles
            .Where(bp => bp.BusinessId == businessId)
            .Select(bp => bp.CurrencySymbol)
            .FirstOrDefaultAsync();

        return currencySymbol ?? "€";
    }

    /// <summary>
    /// Checks if auto-receipt is enabled for the business and generates a receipt if so.
    /// </summary>
    private async Task TryAutoGenerateReceiptAsync(int paymentId, int businessId, string userId)
    {
        if (_receiptService == null) return;

        var business = await _portalDbContext.Businesses
            .Where(b => b.Id == businessId)
            .Select(b => new { b.IsAutoReceiptEnabled })
            .FirstOrDefaultAsync();

        if (business?.IsAutoReceiptEnabled == true)
        {
            // Use default signature if available
            int? defaultSignatureId = null;
            var defaultSig = await _portalDbContext.Signatures.IgnoreQueryFilters()
                .Where(s => s.BusinessId == businessId && s.IsDefault && s.IsActive)
                .Select(s => s.Id)
                .FirstOrDefaultAsync();
            if (defaultSig > 0)
                defaultSignatureId = defaultSig;

            await _receiptService.GenerateReceiptAsync(paymentId, businessId, userId, defaultSignatureId);
        }
    }
}
