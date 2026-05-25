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
    private readonly PortalDbContext _portalDbContext;

    // Invoice status constants
    private const int InvoiceStatusIssued = 2;

    public PaymentService(
        PaymentRepository paymentRepository,
        InvoiceRepository invoiceRepository,
        IFinancialStatusEngine financialStatusEngine,
        PortalDbContext portalDbContext)
    {
        _paymentRepository = paymentRepository;
        _invoiceRepository = invoiceRepository;
        _financialStatusEngine = financialStatusEngine;
        _portalDbContext = portalDbContext;
    }

    /// <inheritdoc />
    public async Task<ServiceResult> RecordPaymentAsync(RecordPaymentDto dto, int businessId, string userId)
    {
        // 1. Validate invoice exists and belongs to businessId
        var invoice = await _invoiceRepository.GetByIdAndBusinessIdAsync(dto.InvoiceId, businessId);
        if (invoice == null)
            return ServiceResult.Fail("Invoice not found.");

        // 2. Validate invoice is in Issued status
        if (invoice.InvoiceStatusTypeId != InvoiceStatusIssued)
            return ServiceResult.Fail("Payments can only be recorded against issued invoices.");

        // 3. Validate amount > 0
        if (dto.Amount <= 0)
            return ServiceResult.Fail("Payment amount must be greater than zero.");

        // 4. Compute outstanding balance and validate amount ≤ outstanding
        var totalPaid = await _paymentRepository.GetTotalPaidAsync(dto.InvoiceId, businessId);
        var outstandingBalance = invoice.TotalAmount - totalPaid;

        if (dto.Amount > outstandingBalance)
        {
            var currencySymbol = await GetCurrencySymbolAsync(businessId);
            return ServiceResult.Fail($"Amount exceeds outstanding balance of {currencySymbol}{outstandingBalance:F2}.");
        }

        // 5. Insert payment record
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

        // 6. Trigger financial status recalculation
        await _financialStatusEngine.RecalculateStatusAsync(dto.InvoiceId, businessId);

        return ServiceResult.Ok(paymentId);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> VoidPaymentAsync(int paymentId, int businessId)
    {
        // 1. Validate payment exists and belongs to businessId
        var payment = await _paymentRepository.GetByIdAndBusinessIdAsync(paymentId, businessId);
        if (payment == null)
            return ServiceResult.Fail("Payment not found.");

        // 2. Check if already voided
        if (payment.IsVoided)
            return ServiceResult.Fail("This payment has already been voided.");

        // 3. Set IsVoided = 1
        await _paymentRepository.VoidAsync(paymentId);

        // 4. Trigger financial status recalculation on parent invoice
        await _financialStatusEngine.RecalculateStatusAsync(payment.InvoiceId, businessId);

        return ServiceResult.Ok();
    }

    /// <inheritdoc />
    public async Task<List<PaymentHistoryDto>> GetPaymentHistoryAsync(int invoiceId, int businessId)
    {
        var payments = await _paymentRepository.GetAllPaymentsByInvoiceIdAsync(invoiceId, businessId);

        // Load payment method types for name lookup
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
            IsVoided = p.IsVoided
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

    /// <summary>
    /// Gets the currency symbol for the business from BusinessProfile.
    /// Falls back to "€" if no profile is found.
    /// </summary>
    private async Task<string> GetCurrencySymbolAsync(int businessId)
    {
        var currencySymbol = await _portalDbContext.BusinessProfiles
            .Where(bp => bp.BusinessId == businessId)
            .Select(bp => bp.CurrencySymbol)
            .FirstOrDefaultAsync();

        return currencySymbol ?? "€";
    }
}
