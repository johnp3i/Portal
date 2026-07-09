using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Evaluates VAT deadline conflicts when a payment schedule is being created.
/// Compares the first instalment's due date against the invoice's VAT submission
/// period deadline to warn users about potential cash flow risk.
/// </summary>
public class VatWarningService : IVatWarningService
{
    private readonly PortalDbContext _dbContext;

    /// <summary>
    /// Standard VAT filing offset: the submission deadline is 23 days after the period end date
    /// (the 23rd of the month following the bi-monthly period end in Ireland).
    /// </summary>
    private const int FilingOffsetDays = 23;

    public VatWarningService(PortalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<VatWarningDto?> GetVatWarningAsync(int invoiceId, DateOnly? firstInstalmentDueDate, decimal firstInstalmentAmount, int businessId)
    {
        try
        {
            // If no due date provided, we cannot compare against the deadline
            if (firstInstalmentDueDate == null)
                return null;

            // Look up the invoice to get VatSubmissionPeriodId, TaxAmount, and InvoiceDate
            var invoice = await _dbContext.Invoices
                .Where(i => i.Id == invoiceId && i.BusinessId == businessId && !i.IsDeleted)
                .Select(i => new { i.VatSubmissionPeriodId, i.TaxAmount, i.InvoiceDate })
                .FirstOrDefaultAsync();

            if (invoice == null)
                return null;

            // If TaxAmount is 0, no VAT concern
            if (invoice.TaxAmount <= 0)
                return null;

            DateOnly periodEndDate;

            if (invoice.VatSubmissionPeriodId != null)
            {
                // Invoice has an assigned period — use it directly
                var vatPeriod = await _dbContext.VatSubmissionPeriods
                    .Where(p => p.Id == invoice.VatSubmissionPeriodId.Value)
                    .Select(p => new { p.PeriodEndDate })
                    .FirstOrDefaultAsync();

                if (vatPeriod == null)
                    return null;

                periodEndDate = vatPeriod.PeriodEndDate;
            }
            else
            {
                // Invoice has no assigned period — derive from InvoiceDate
                // Try to find an existing period row that contains the invoice date
                var derivedPeriod = await _dbContext.VatSubmissionPeriods
                    .Where(p => p.BusinessId == businessId
                             && p.PeriodStartDate <= invoice.InvoiceDate
                             && p.PeriodEndDate >= invoice.InvoiceDate)
                    .Select(p => new { p.PeriodEndDate })
                    .FirstOrDefaultAsync();

                if (derivedPeriod != null)
                {
                    periodEndDate = derivedPeriod.PeriodEndDate;
                }
                else
                {
                    // No period row exists — calculate from business VAT configuration
                    var profile = await _dbContext.BusinessProfiles
                        .Where(bp => bp.BusinessId == businessId)
                        .Select(bp => new { bp.VatRegistrationDate, bp.VatPeriodLengthInMonths })
                        .FirstOrDefaultAsync();

                    if (profile == null)
                        return null;

                    periodEndDate = CalculatePeriodEndDate(invoice.InvoiceDate, profile.VatRegistrationDate, profile.VatPeriodLengthInMonths);
                }
            }

            // Calculate the submission deadline: PeriodEndDate + filing offset (23 days)
            var submissionDeadline = periodEndDate.AddDays(FilingOffsetDays);

            // If the first instalment due date is NOT after the deadline, no warning needed
            if (firstInstalmentDueDate.Value <= submissionDeadline)
                return null;

            // First instalment due date IS after the deadline — build the warning
            var highlightVatAmount = firstInstalmentAmount < invoice.TaxAmount;

            return new VatWarningDto
            {
                ShowWarning = true,
                HighlightVatAmount = highlightVatAmount,
                TaxAmount = invoice.TaxAmount,
                SubmissionDeadline = submissionDeadline,
                Message = $"The VAT for this invoice (\u20AC{invoice.TaxAmount:N2}) will need to be paid to the tax authority regardless of when you receive payment. Consider setting your first instalment to at least cover the VAT amount."
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Calculates the VAT period end date for a given invoice date based on
    /// the business's VAT registration date and period length.
    /// </summary>
    private static DateOnly CalculatePeriodEndDate(DateOnly invoiceDate, DateOnly vatRegistrationDate, int periodLengthInMonths)
    {
        // Find how many complete periods have elapsed since registration
        var registrationStart = new DateOnly(vatRegistrationDate.Year, vatRegistrationDate.Month, 1);

        // Calculate months elapsed from registration start to invoice date
        var totalMonths = (invoiceDate.Year - registrationStart.Year) * 12 + (invoiceDate.Month - registrationStart.Month);

        // Which period number does the invoice date fall into?
        var periodIndex = totalMonths / periodLengthInMonths;

        // Calculate the start of that period
        var periodStart = registrationStart.AddMonths(periodIndex * periodLengthInMonths);

        // If invoice date is before periodStart (edge case with day-of-month), go back one period
        if (invoiceDate < periodStart)
        {
            periodIndex--;
            periodStart = registrationStart.AddMonths(periodIndex * periodLengthInMonths);
        }

        // Period end = last day of (periodStart + periodLength - 1 month)
        var periodEndMonth = periodStart.AddMonths(periodLengthInMonths);
        var periodEndDate = periodEndMonth.AddDays(-1);

        return periodEndDate;
    }
}
