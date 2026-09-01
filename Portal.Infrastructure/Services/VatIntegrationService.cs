using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Computes VAT-related KPIs for the revenue dashboard by querying
/// VatSubmissionPeriod, Invoice, and Purchase tables.
/// Tenant isolation is handled by EF Core global query filters on BusinessId.
/// </summary>
public class VatIntegrationService : IVatIntegrationService
{
    private readonly PortalDbContext _dbContext;

    public VatIntegrationService(PortalDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<VatSummaryDto> GetCurrentPeriodSummaryAsync(int businessId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Find current VAT period where today falls between PeriodStartDate and PeriodEndDate
        var currentPeriod = await _dbContext.VatSubmissionPeriods
            .Where(p => p.PeriodStartDate <= today && p.PeriodEndDate >= today)
            .FirstOrDefaultAsync();

        if (currentPeriod == null)
        {
            return new VatSummaryDto
            {
                TotalOutputVat = 0m,
                TotalInputVat = 0m,
                NetVatPayable = 0m,
                PeriodLabel = "No active period",
                HasData = false
            };
        }

        // Compute Output VAT: sum of Invoice.TaxAmount for fully paid invoices
        // (InvoiceFinancialStatusTypeId = 3) with InvoiceDate in current period
        var outputVat = await _dbContext.Invoices
            .Where(i => i.InvoiceFinancialStatusTypeId == 3
                && i.InvoiceDate >= currentPeriod.PeriodStartDate
                && i.InvoiceDate <= currentPeriod.PeriodEndDate
                && !i.IsDeleted)
            .SumAsync(i => (decimal?)i.TaxAmount) ?? 0m;

        // Z-Report Revenue: add RevenueSummary.TotalVat for Z-Reports assigned to this period
        var businessProfile = await _dbContext.BusinessProfiles
            .FirstOrDefaultAsync(bp => bp.BusinessId == businessId);
        if (businessProfile?.IsZReportEnabled == true)
        {
            var zReportVat = await _dbContext.RevenueSummaries
                .Where(rs => rs.BusinessId == businessId
                    && rs.IsActive
                    && rs.VatSubmissionPeriodId == currentPeriod.Id)
                .SumAsync(rs => (decimal?)rs.TotalVat) ?? 0m;
            outputVat += zReportVat;
        }

        // External sales records (imported POS + external platform sales) assigned to this period
        var externalSalesVat = await _dbContext.ExternalSalesRecords
            .Where(esr => esr.BusinessId == businessId
                && esr.IsActive
                && esr.VatSubmissionPeriodId == currentPeriod.Id)
            .SumAsync(esr => (decimal?)esr.VatAmount) ?? 0m;
        outputVat += externalSalesVat;

        // Compute Input VAT: sum of Purchase.VatAmount for non-cancelled purchases
        // with InvoiceDate in current period
        var inputVat = await _dbContext.Purchases
            .Where(p => !p.IsCancelled
                && p.InvoiceDate >= currentPeriod.PeriodStartDate
                && p.InvoiceDate <= currentPeriod.PeriodEndDate)
            .SumAsync(p => (decimal?)p.VatAmount) ?? 0m;

        // Compute Net VAT Payable: Output - Input
        var netVatPayable = outputVat - inputVat;

        return new VatSummaryDto
        {
            TotalOutputVat = outputVat,
            TotalInputVat = inputVat,
            NetVatPayable = netVatPayable,
            PeriodLabel = currentPeriod.PeriodLabel,
            HasData = true
        };
    }

    /// <inheritdoc />
    public async Task<List<VatPeriodLiabilityDto>> GetVatLiabilityByPeriodAsync(int businessId)
    {
        // Get last 6 VAT periods ordered by PeriodStartDate descending
        var periods = await _dbContext.VatSubmissionPeriods
            .OrderByDescending(p => p.PeriodStartDate)
            .Take(6)
            .OrderBy(p => p.PeriodStartDate)
            .ToListAsync();

        var result = new List<VatPeriodLiabilityDto>();

        // Load business profile once for Z-Report feature check
        var businessProfile = await _dbContext.BusinessProfiles
            .FirstOrDefaultAsync(bp => bp.BusinessId == businessId);

        foreach (var period in periods)
        {
            // Compute Output VAT for this period: sum of Invoice.TaxAmount for fully paid invoices
            var outputVat = await _dbContext.Invoices
                .Where(i => i.InvoiceFinancialStatusTypeId == 3
                    && i.InvoiceDate >= period.PeriodStartDate
                    && i.InvoiceDate <= period.PeriodEndDate
                    && !i.IsDeleted)
                .SumAsync(i => (decimal?)i.TaxAmount) ?? 0m;

            // Z-Report Revenue: add RevenueSummary.TotalVat for Z-Reports assigned to this period
            if (businessProfile?.IsZReportEnabled == true)
            {
                var zReportVat = await _dbContext.RevenueSummaries
                    .Where(rs => rs.BusinessId == businessId
                        && rs.IsActive
                        && rs.VatSubmissionPeriodId == period.Id)
                    .SumAsync(rs => (decimal?)rs.TotalVat) ?? 0m;
                outputVat += zReportVat;
            }

            // External sales records (imported POS + external platform sales) assigned to this period
            var externalSalesVat = await _dbContext.ExternalSalesRecords
                .Where(esr => esr.BusinessId == businessId
                    && esr.IsActive
                    && esr.VatSubmissionPeriodId == period.Id)
                .SumAsync(esr => (decimal?)esr.VatAmount) ?? 0m;
            outputVat += externalSalesVat;

            // Compute Input VAT for this period: sum of Purchase.VatAmount for non-cancelled purchases
            var inputVat = await _dbContext.Purchases
                .Where(p => !p.IsCancelled
                    && p.InvoiceDate >= period.PeriodStartDate
                    && p.InvoiceDate <= period.PeriodEndDate)
                .SumAsync(p => (decimal?)p.VatAmount) ?? 0m;

            // Compute Net Payable: Output - Input
            var netPayable = outputVat - inputVat;

            result.Add(new VatPeriodLiabilityDto
            {
                PeriodLabel = period.PeriodLabel,
                OutputVat = outputVat,
                InputVat = inputVat,
                NetPayable = netPayable
            });
        }

        return result;
    }
}
