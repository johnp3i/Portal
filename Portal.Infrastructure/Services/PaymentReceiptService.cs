using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Receipt;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Orchestrates payment receipt generation, retrieval, and voiding.
/// Handles per-invoice payments (single line), global payments (multi-line from children),
/// and credit-applied payments.
/// </summary>
public class PaymentReceiptService : IPaymentReceiptService
{
    private readonly PaymentReceiptRepository _receiptRepository;
    private readonly PaymentReceiptLineRepository _lineRepository;
    private readonly PaymentReceiptShareRepository _shareRepository;
    private readonly PaymentRepository _paymentRepository;
    private readonly SignatureRepository _signatureRepository;
    private readonly CreditNoteRepository _creditNoteRepository;
    private readonly PortalDbContext _dbContext;

    public PaymentReceiptService(
        PaymentReceiptRepository receiptRepository,
        PaymentReceiptLineRepository lineRepository,
        PaymentReceiptShareRepository shareRepository,
        PaymentRepository paymentRepository,
        SignatureRepository signatureRepository,
        CreditNoteRepository creditNoteRepository,
        PortalDbContext dbContext)
    {
        _receiptRepository = receiptRepository;
        _lineRepository = lineRepository;
        _shareRepository = shareRepository;
        _paymentRepository = paymentRepository;
        _signatureRepository = signatureRepository;
        _creditNoteRepository = creditNoteRepository;
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<ServiceResult<ReceiptViewModel>> GenerateReceiptAsync(
        int paymentId, int businessId, string userId, int? signatureId = null, string? notes = null)
    {
        try
        {
            var payment = await _paymentRepository.GetByIdAndBusinessIdAsync(paymentId, businessId);
            if (payment == null)
                return ServiceResult<ReceiptViewModel>.Fail("Payment not found.");

            if (payment.IsVoided)
                return ServiceResult<ReceiptViewModel>.Fail("Cannot generate a receipt for a voided payment.");

            // Check if an active (non-voided) receipt already exists
            var existingReceipt = await _receiptRepository.GetByPaymentIdAsync(paymentId, businessId);
            if (existingReceipt != null && !existingReceipt.IsVoided)
                return ServiceResult<ReceiptViewModel>.Fail("A receipt has already been generated for this payment.");

            // Determine customer
            int customerId;
            if (payment.CustomerId.HasValue)
                customerId = payment.CustomerId.Value;
            else if (payment.InvoiceId.HasValue)
            {
                var invoice = await _dbContext.Invoices.IgnoreQueryFilters()
                    .Where(i => i.Id == payment.InvoiceId.Value && i.BusinessId == businessId)
                    .Select(i => i.CustomerId)
                    .FirstOrDefaultAsync();
                customerId = invoice;
            }
            else
                return ServiceResult<ReceiptViewModel>.Fail("Cannot determine customer for this payment.");

            // Generate receipt number atomically
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                // Build line items first to determine the primary invoice number
                var lines = new List<PaymentReceiptLine>();

                if (payment.InvoiceId == null && payment.CustomerId != null)
                {
                    // Global/parent payment — get child allocations
                    var children = await _paymentRepository.GetChildAllocationsAsync(paymentId, businessId);
                    var activeChildren = children.Where(c => !c.IsVoided && c.InvoiceId.HasValue).ToList();

                    foreach (var child in activeChildren)
                    {
                        var inv = await _dbContext.Invoices.IgnoreQueryFilters()
                            .Where(i => i.Id == child.InvoiceId!.Value && i.BusinessId == businessId)
                            .Select(i => new { i.InvoiceNumber, i.TotalAmount })
                            .FirstOrDefaultAsync();

                        if (inv == null) continue;

                        var totalPaid = await _paymentRepository.GetTotalPaidAsync(child.InvoiceId!.Value, businessId);
                        var totalCredited = await _creditNoteRepository.GetTotalAppliedCreditAsync(child.InvoiceId!.Value, businessId);
                        var outstandingAfter = inv.TotalAmount - totalPaid - totalCredited;
                        var outstandingBefore = outstandingAfter + child.Amount;

                        lines.Add(new PaymentReceiptLine
                        {
                            PaymentId = child.Id,
                            InvoiceId = child.InvoiceId!.Value,
                            InvoiceNumber = inv.InvoiceNumber,
                            Amount = child.Amount,
                            InvoiceTotal = inv.TotalAmount,
                            InvoiceOutstandingBefore = outstandingBefore,
                            InvoiceOutstandingAfter = outstandingAfter
                        });
                    }
                }
                else if (payment.InvoiceId.HasValue)
                {
                    // Per-invoice or child payment
                    var inv = await _dbContext.Invoices.IgnoreQueryFilters()
                        .Where(i => i.Id == payment.InvoiceId.Value && i.BusinessId == businessId)
                        .Select(i => new { i.InvoiceNumber, i.TotalAmount })
                        .FirstOrDefaultAsync();

                    if (inv != null)
                    {
                        var totalPaid = await _paymentRepository.GetTotalPaidAsync(payment.InvoiceId.Value, businessId);
                        var totalCredited = await _creditNoteRepository.GetTotalAppliedCreditAsync(payment.InvoiceId.Value, businessId);
                        var outstandingAfter = inv.TotalAmount - totalPaid - totalCredited;
                        var outstandingBefore = outstandingAfter + payment.Amount;

                        lines.Add(new PaymentReceiptLine
                        {
                            PaymentId = payment.Id,
                            InvoiceId = payment.InvoiceId.Value,
                            InvoiceNumber = inv.InvoiceNumber,
                            Amount = payment.Amount,
                            InvoiceTotal = inv.TotalAmount,
                            InvoiceOutstandingBefore = outstandingBefore,
                            InvoiceOutstandingAfter = outstandingAfter
                        });
                    }
                }

                // Compute total outstanding balance after for customer
                var outstandingInvoices = await _paymentRepository.GetOutstandingInvoicesForCustomerAsync(customerId, businessId);
                var customerOutstandingAfter = outstandingInvoices.Sum(i => i.OutstandingBalance);

                // Generate receipt number from the primary (first/lowest) invoice
                var primaryInvoiceNumber = lines.Count > 0
                    ? lines.OrderBy(l => l.InvoiceNumber).First().InvoiceNumber
                    : "UNKNOWN";
                var receiptNumber = await _receiptRepository.GenerateReceiptNumberAsync(businessId, primaryInvoiceNumber, payment.PaymentDateUtc);

                // Create receipt entity
                var receipt = new PaymentReceipt
                {
                    BusinessId = businessId,
                    ReceiptNumber = receiptNumber,
                    CustomerId = customerId,
                    PaymentId = paymentId,
                    ReceiptDate = payment.PaymentDateUtc,
                    TotalAmountReceived = payment.Amount,
                    OutstandingBalanceAfter = customerOutstandingAfter,
                    PaymentMethodTypeId = payment.PaymentMethodTypeId,
                    PaymentReference = payment.Reference,
                    Notes = notes,
                    SignatureId = signatureId,
                    IsVoided = false,
                    CreatedByUserId = userId,
                    CreatedAtUtc = DateTime.UtcNow
                };

                var receiptId = await _receiptRepository.InsertAsync(receipt);

                // Insert lines
                foreach (var line in lines)
                {
                    line.PaymentReceiptId = receiptId;
                    await _lineRepository.InsertAsync(line);
                }

                await transaction.CommitAsync();

                // Build view model for response
                var viewModel = await GetReceiptAsync(receiptId, businessId);
                if (viewModel == null)
                    return ServiceResult<ReceiptViewModel>.Fail("Receipt created but failed to load details. Receipt ID: " + receiptId);

                return ServiceResult<ReceiptViewModel>.Ok(viewModel);
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
    public async Task<ReceiptViewModel?> GetReceiptAsync(int receiptId, int businessId)
    {
        try
        {
            var receipt = await _receiptRepository.GetByIdAsync(receiptId, businessId);
            if (receipt == null) return null;

            var lines = await _lineRepository.GetByReceiptIdAsync(receiptId);

            var customer = await _dbContext.Customers.IgnoreQueryFilters()
                .Where(c => c.Id == receipt.CustomerId && c.BusinessId == businessId)
                .Select(c => new { c.Name, c.AddressLine1, c.AddressLine2, c.City, c.PostalCode, c.Country, c.Email })
                .FirstOrDefaultAsync();

            var business = await _dbContext.Businesses
                .Where(b => b.Id == businessId)
                .Select(b => new { b.Name })
                .FirstOrDefaultAsync();

            var profile = await _dbContext.BusinessProfiles.IgnoreQueryFilters()
                .Where(bp => bp.BusinessId == businessId)
                .Select(bp => new { bp.AddressLine1, bp.AddressLine2, bp.City, bp.PostalCode, bp.Country, bp.TelephoneNumber, bp.Email, bp.VatRegistrationNumber, bp.CurrencySymbol })
                .FirstOrDefaultAsync();

            var paymentMethod = await _dbContext.PaymentMethodTypes
                .Where(pm => pm.Id == receipt.PaymentMethodTypeId)
                .Select(pm => pm.Name)
                .FirstOrDefaultAsync() ?? "Unknown";

            var logo = await _dbContext.BusinessLogos.IgnoreQueryFilters()
                .Where(l => l.BusinessId == businessId && l.IsPrimary)
                .Select(l => l.PublicUrl)
                .FirstOrDefaultAsync();

            // Signature
            string? sigLabel = null, sigPosition = null, sigPath = null;
            if (receipt.SignatureId.HasValue)
            {
                var sig = await _signatureRepository.GetByIdAsync(receipt.SignatureId.Value, businessId);
                if (sig != null)
                {
                    sigLabel = sig.Label;
                    sigPosition = sig.Position;
                    sigPath = sig.FilePath;
                }
            }

            // Credit amount (for global payments with overpayment)
            decimal? creditAmount = null;
            var payment = await _paymentRepository.GetByIdAndBusinessIdAsync(receipt.PaymentId, businessId);
            if (payment != null && payment.CreditAmount > 0)
                creditAmount = payment.CreditAmount;

            var customerAddress = string.Join(", ",
                new[] { customer?.AddressLine1, customer?.AddressLine2, customer?.City, customer?.PostalCode, customer?.Country }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));

            var businessAddress = string.Join(", ",
                new[] { profile?.AddressLine1, profile?.AddressLine2, profile?.City, profile?.PostalCode, profile?.Country }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));

            return new ReceiptViewModel
            {
                Id = receipt.Id,
                ReceiptNumber = receipt.ReceiptNumber,
                ReceiptDate = receipt.ReceiptDate,
                CustomerName = customer?.Name ?? "Unknown",
                CustomerAddress = customerAddress,
                CustomerEmail = customer?.Email,
                TotalAmountReceived = receipt.TotalAmountReceived,
                OutstandingBalanceAfter = receipt.OutstandingBalanceAfter,
                PaymentMethodName = paymentMethod,
                PaymentReference = receipt.PaymentReference,
                Notes = receipt.Notes,
                IsVoided = receipt.IsVoided,
                CurrencySymbol = profile?.CurrencySymbol ?? "€",
                CreditAmount = creditAmount,
                BusinessName = business?.Name ?? "Unknown",
                BusinessAddress = businessAddress,
                BusinessPhone = profile?.TelephoneNumber,
                BusinessEmail = profile?.Email,
                BusinessVatNumber = profile?.VatRegistrationNumber,
                BusinessLogoPath = logo,
                SignatureLabel = sigLabel,
                SignaturePosition = sigPosition,
                SignatureFilePath = sigPath,
                Lines = lines.Select(l => new ReceiptLineViewModel
                {
                    InvoiceNumber = l.InvoiceNumber,
                    InvoiceTotal = l.InvoiceTotal,
                    Amount = l.Amount,
                    InvoiceOutstandingBefore = l.InvoiceOutstandingBefore,
                    InvoiceOutstandingAfter = l.InvoiceOutstandingAfter
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<(List<ReceiptListDto> Items, int TotalCount)> GetReceiptsPagedAsync(
        int businessId, int? customerId, DateTime? fromDate, DateTime? toDate, bool? isVoided,
        int page, int pageSize)
    {
        try
        {
            var offset = (page - 1) * pageSize;
            return await _receiptRepository.GetPagedAsync(businessId, customerId, fromDate, toDate, isVoided, offset, pageSize);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> VoidReceiptAsync(int receiptId, int businessId)
    {
        try
        {
            var receipt = await _receiptRepository.GetByIdAsync(receiptId, businessId);
            if (receipt == null)
                return ServiceResult.Fail("Receipt not found.");

            if (receipt.IsVoided)
                return ServiceResult.Fail("This receipt has already been voided.");

            await _receiptRepository.VoidAsync(receiptId, businessId);
            await _shareRepository.DeactivateByReceiptIdAsync(receiptId, businessId);

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task VoidByPaymentIdAsync(int paymentId, int businessId)
    {
        try
        {
            var receipt = await _receiptRepository.GetByPaymentIdAsync(paymentId, businessId);
            if (receipt != null && !receipt.IsVoided)
            {
                await _receiptRepository.VoidAsync(receipt.Id, businessId);
                await _shareRepository.DeactivateByReceiptIdAsync(receipt.Id, businessId);
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> HasReceiptAsync(int paymentId, int businessId)
    {
        try
        {
            var receipt = await _receiptRepository.GetByPaymentIdAsync(paymentId, businessId);
            return receipt != null && !receipt.IsVoided;
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
