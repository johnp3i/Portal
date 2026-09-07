using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;

namespace Portal.Infrastructure.Services.Notifications;

/// <summary>
/// Shared helpers for digest composers: business name + currency lookup, and the
/// "this week at a glance" figures (invoices issued / payments received in the cycle window).
/// Tenant-less — everything is keyed by explicit businessId.
/// </summary>
public abstract class DigestComposerBase
{
    private const int InvoiceStatusIssued = 2; // [invoice].InvoiceStatusType Issued

    protected readonly PortalDbContext DbContext;

    protected DigestComposerBase(PortalDbContext dbContext)
    {
        DbContext = dbContext;
    }

    protected async Task<(string BusinessName, string CurrencySymbol)> LoadBusinessBrandingAsync(int businessId)
    {
        var businessName = await DbContext.Businesses
            .AsNoTracking()
            .Where(b => b.Id == businessId)
            .Select(b => b.Name)
            .FirstOrDefaultAsync() ?? "Your business";

        var currencySymbol = await DbContext.BusinessProfiles
            .AsNoTracking()
            .Where(bp => bp.BusinessId == businessId)
            .Select(bp => bp.CurrencySymbol)
            .FirstOrDefaultAsync() ?? "€";

        return (businessName, currencySymbol);
    }

    /// <summary>Invoices issued and payments received within [from, to] (inclusive dates).</summary>
    protected async Task<WeekGlance> LoadWeekGlanceAsync(int businessId, DateOnly from, DateOnly to)
    {
        var issued = await DbContext.Invoices
            .AsNoTracking()
            .Where(i => i.BusinessId == businessId
                        && !i.IsDeleted
                        && i.InvoiceStatusTypeId == InvoiceStatusIssued
                        && i.InvoiceDate >= from
                        && i.InvoiceDate <= to)
            .GroupBy(i => 1)
            .Select(g => new { Count = g.Count(), Total = g.Sum(i => i.TotalAmount) })
            .FirstOrDefaultAsync();

        var fromDt = from.ToDateTime(TimeOnly.MinValue);
        var toDt = to.ToDateTime(TimeOnly.MinValue).AddDays(1); // exclusive upper bound

        var collected = await DbContext.Payments
            .AsNoTracking()
            .Where(p => p.BusinessId == businessId
                        && !p.IsVoided
                        && p.ParentPaymentId == null
                        && p.PaymentDateUtc >= fromDt
                        && p.PaymentDateUtc < toDt)
            .GroupBy(p => 1)
            .Select(g => new { Count = g.Count(), Total = g.Sum(p => p.Amount) })
            .FirstOrDefaultAsync();

        return new WeekGlance
        {
            InvoicesIssued = issued?.Count ?? 0,
            IssuedAmount = issued?.Total ?? 0m,
            PaymentsReceived = collected?.Count ?? 0,
            Collected = collected?.Total ?? 0m
        };
    }
}

public class WeekGlance
{
    public int InvoicesIssued { get; set; }
    public decimal IssuedAmount { get; set; }
    public int PaymentsReceived { get; set; }
    public decimal Collected { get; set; }
}
