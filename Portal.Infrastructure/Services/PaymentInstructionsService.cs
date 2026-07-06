using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

public class PaymentInstructionsService : IPaymentInstructionsService
{
    private readonly PortalDbContext _dbContext;

    public PaymentInstructionsService(PortalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaymentInstructionsData?> GetPaymentInstructionsAsync(int invoiceId, int businessId)
    {
        try
        {
            // Check toggle
            var business = await _dbContext.Businesses
                .Include(b => b.BusinessProfile)
                .FirstOrDefaultAsync(b => b.Id == businessId);

            if (business == null || !business.IsPaymentInstructionsEnabled)
                return null;

            // Get active payment detail with lowest SortOrder
            var paymentDetail = await _dbContext.BusinessPaymentDetails
                .Where(pd => pd.BusinessId == businessId && pd.IsActive)
                .OrderBy(pd => pd.SortOrder)
                .FirstOrDefaultAsync();

            if (paymentDetail == null)
                return null;

            // Get invoice
            var invoice = await _dbContext.Invoices
                .FirstOrDefaultAsync(i => i.Id == invoiceId && i.BusinessId == businessId);

            if (invoice == null)
                return null;

            // Calculate outstanding amount: TotalAmount minus sum of non-voided payments
            var totalPaid = await _dbContext.Payments
                .Where(p => p.InvoiceId == invoiceId && !p.IsVoided)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            var outstandingAmount = Math.Max(0, invoice.TotalAmount - totalPaid);

            // Build transfer reference: "{InvoiceNumber} — {BusinessName}" (em-dash)
            var transferReference = $"{invoice.InvoiceNumber} \u2014 {business.Name}";

            var currencySymbol = business.BusinessProfile?.CurrencySymbol ?? "\u20ac";

            return new PaymentInstructionsData
            {
                BusinessName = business.Name,
                BankName = paymentDetail.BankName,
                Iban = paymentDetail.Iban,
                PayeeName = paymentDetail.PayeeName,
                SwiftBic = paymentDetail.SwiftBic,
                OutstandingAmount = outstandingAmount,
                CurrencySymbol = currencySymbol,
                DueDate = invoice.DueDate,
                TransferReference = transferReference
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<PaymentDeclarationResult> DeclarePaymentAsync(string shareToken, string ipAddress)
    {
        try
        {
            // 1. Validate share token
            var share = await _dbContext.InvoiceShares
                .FirstOrDefaultAsync(s => s.ShareToken == shareToken);

            if (share == null || !share.IsActive || share.ExpiresAtUtc <= DateTimeOffset.UtcNow)
                return new PaymentDeclarationResult { Success = false, Message = "This invoice link is no longer active." };

            // 2. Rate limit check (3 declarations per share token per hour)
            var oneHourAgo = DateTime.UtcNow.AddHours(-1);
            var recentDeclarations = await _dbContext.AuditLogs
                .CountAsync(a => a.TableName == "Invoice"
                              && a.Action == "PaymentDeclared"
                              && a.RecordId == share.InvoiceId.ToString()
                              && a.OldValues != null && a.OldValues.Contains(shareToken)
                              && a.Timestamp >= oneHourAgo);

            if (recentDeclarations >= 3)
                return new PaymentDeclarationResult { Success = false, Message = "Too many payment declarations. Please try again later." };

            // 3. Check invoice financial status is eligible
            var invoice = await _dbContext.Invoices
                .FirstOrDefaultAsync(i => i.Id == share.InvoiceId);

            if (invoice == null)
                return new PaymentDeclarationResult { Success = false, Message = "Invoice not found." };

            var eligibleStatuses = new[] { 1, 2, 4 }; // Unpaid, PartiallyPaid, Overdue
            if (!eligibleStatuses.Contains(invoice.InvoiceFinancialStatusTypeId))
            {
                if (invoice.InvoiceFinancialStatusTypeId == 6)
                    return new PaymentDeclarationResult { Success = false, Message = "A payment declaration has already been recorded for this invoice." };

                return new PaymentDeclarationResult { Success = false, Message = "This invoice is not eligible for payment declaration." };
            }

            // 4. Update invoice financial status to PaymentOnboard (6)
            invoice.InvoiceFinancialStatusTypeId = 6;

            // 5. Create audit log entry
            var now = DateTime.UtcNow;
            _dbContext.AuditLogs.Add(new AuditLog
            {
                BusinessId = share.BusinessId,
                UserId = "anonymous",
                TableName = "Invoice",
                Action = "PaymentDeclared",
                RecordId = share.InvoiceId.ToString(),
                OldValues = $"ShareToken={shareToken};IP={ipAddress}",
                NewValues = $"InvoiceFinancialStatusTypeId=6;DeclaredAtUtc={now:O}",
                Timestamp = now
            });

            await _dbContext.SaveChangesAsync();

            return new PaymentDeclarationResult
            {
                Success = true,
                Message = "Thank you. The business has been notified of your payment.",
                DeclaredAtUtc = now
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ToggleResult> SetPaymentInstructionsEnabledAsync(int businessId, bool enabled)
    {
        try
        {
            var business = await _dbContext.Businesses
                .FirstOrDefaultAsync(b => b.Id == businessId);

            if (business == null)
                return new ToggleResult { Success = false, Message = "Business not found." };

            if (enabled)
            {
                // Check if business has at least one active payment detail
                var hasActiveDetails = await _dbContext.BusinessPaymentDetails
                    .AnyAsync(pd => pd.BusinessId == businessId && pd.IsActive);

                if (!hasActiveDetails)
                    return new ToggleResult { Success = false, Message = "Add bank details in your payment details section before enabling this option." };
            }

            business.IsPaymentInstructionsEnabled = enabled;
            await _dbContext.SaveChangesAsync();

            var message = enabled
                ? "Payment instructions enabled. Customers will see a 'Pay by Bank Transfer' button on shared invoices."
                : "Payment instructions disabled.";

            return new ToggleResult { Success = true, Message = message };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<bool> IsEnabledForBusinessAsync(int businessId)
    {
        try
        {
            return await _dbContext.Businesses
                .Where(b => b.Id == businessId)
                .Select(b => b.IsPaymentInstructionsEnabled)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
