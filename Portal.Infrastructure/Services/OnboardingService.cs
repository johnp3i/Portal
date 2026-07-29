using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

public class OnboardingService : IOnboardingService
{
    private readonly PortalDbContext _dbContext;

    public OnboardingService(PortalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<OnboardingStateDto> GetOnboardingStateAsync(int businessId)
    {
        try
        {
            var business = await _dbContext.Businesses
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == businessId);

            if (business == null || business.IsOnboardingDismissed)
            {
                return new OnboardingStateDto { IsVisible = false };
            }

            var hasProfile = await _dbContext.BusinessProfiles
                .AnyAsync(bp => bp.BusinessId == businessId
                    && bp.AddressLine1 != null && bp.AddressLine1 != ""
                    && bp.VatRegistrationNumber != null && bp.VatRegistrationNumber != "");

            var hasLogo = await _dbContext.BusinessLogos
                .AnyAsync(bl => bl.BusinessId == businessId);

            var hasPaymentDetails = await _dbContext.BusinessPaymentDetails
                .AnyAsync(pd => pd.BusinessId == businessId && pd.IsActive);

            var hasCustomer = await _dbContext.Customers
                .AnyAsync(c => c.BusinessId == businessId);

            var hasQuotationOrInvoice = await _dbContext.Quotations.AnyAsync(q => q.BusinessId == businessId)
                || await _dbContext.Invoices.AnyAsync(i => i.BusinessId == businessId);

            var hasIssuedInvoice = await _dbContext.Invoices
                .AnyAsync(i => i.BusinessId == businessId && i.InvoiceStatusTypeId == 2);

            var completedCount = new[] { hasProfile, hasLogo, hasPaymentDetails, hasCustomer, hasQuotationOrInvoice, hasIssuedInvoice }
                .Count(b => b);

            var isCelebration = completedCount == 6;

            return new OnboardingStateDto
            {
                IsVisible = true,
                IsCelebration = isCelebration,
                CompletedCount = completedCount,
                HasBusinessProfile = hasProfile,
                HasLogo = hasLogo,
                HasPaymentDetails = hasPaymentDetails,
                HasCustomer = hasCustomer,
                HasQuotationOrInvoice = hasQuotationOrInvoice,
                HasIssuedInvoice = hasIssuedInvoice
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task DismissOnboardingAsync(int businessId)
    {
        try
        {
            var business = await _dbContext.Businesses
                .FirstOrDefaultAsync(b => b.Id == businessId);

            if (business != null)
            {
                business.IsOnboardingDismissed = true;
                await _dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
