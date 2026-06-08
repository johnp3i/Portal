using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Billing;

namespace Portal.Web.Services.Billing;

/// <summary>
/// Defines the contract for backfilling invoice numbers on existing BillingInvoice records.
/// </summary>
public interface IInvoiceBackfillService
{
    /// <summary>
    /// Backfills invoice numbers for all BillingInvoice records with null InvoiceNumber.
    /// Processes year by year in chronological order.
    /// Returns the count of records updated.
    /// </summary>
    Task<int> BackfillAsync();
}

/// <summary>
/// Assigns InvoiceNumbers to existing BillingInvoice records that have a null InvoiceNumber.
/// Processes records grouped by year, ordered chronologically within each group,
/// using a single transaction per year to maintain sequence integrity.
/// </summary>
public class InvoiceBackfillService : IInvoiceBackfillService
{
    private readonly PortalDbContext _portalDbContext;
    private readonly IInvoiceNumberGenerator _invoiceNumberGenerator;
    private readonly ILogger<InvoiceBackfillService> _logger;

    public InvoiceBackfillService(
        PortalDbContext portalDbContext,
        IInvoiceNumberGenerator invoiceNumberGenerator,
        ILogger<InvoiceBackfillService> logger)
    {
        _portalDbContext = portalDbContext;
        _invoiceNumberGenerator = invoiceNumberGenerator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> BackfillAsync()
    {
        try
        {
            // Query all invoices with null InvoiceNumber, grouped by year, ordered by CreatedAtUtc ascending
            var invoicesWithNullNumber = await _portalDbContext.BillingInvoices
                .Where(bi => bi.InvoiceNumber == null)
                .OrderBy(bi => bi.CreatedAtUtc)
                .ToListAsync();

            if (invoicesWithNullNumber.Count == 0)
            {
                _logger.LogInformation("Backfill: No billing invoices with null InvoiceNumber found. Nothing to do.");
                return 0;
            }

            // Group by CreatedAtUtc year
            var yearGroups = invoicesWithNullNumber
                .GroupBy(bi => bi.CreatedAtUtc.Year)
                .OrderBy(g => g.Key);

            int totalUpdated = 0;

            foreach (var yearGroup in yearGroups)
            {
                var year = yearGroup.Key;
                var invoicesInYear = yearGroup.OrderBy(bi => bi.CreatedAtUtc).ToList();

                _logger.LogInformation(
                    "Backfill: Processing {Count} invoices for year {Year}",
                    invoicesInYear.Count, year);

                await using var transaction = await _portalDbContext.Database.BeginTransactionAsync();
                try
                {
                    foreach (var invoice in invoicesInYear)
                    {
                        // Skip records that already have an InvoiceNumber (idempotent)
                        if (!string.IsNullOrEmpty(invoice.InvoiceNumber))
                        {
                            continue;
                        }

                        var invoiceNumber = await _invoiceNumberGenerator.GenerateNextAsync(invoice.CreatedAtUtc);
                        invoice.InvoiceNumber = invoiceNumber;
                        totalUpdated++;

                        _logger.LogInformation(
                            "Backfill: Assigned InvoiceNumber {InvoiceNumber} to BillingInvoice {InvoiceId} (BusinessId: {BusinessId})",
                            invoiceNumber, invoice.Id, invoice.BusinessId);
                    }

                    await _portalDbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation(
                        "Backfill: Committed {Count} invoice number assignments for year {Year}",
                        invoicesInYear.Count, year);
                }
                catch
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(
                        "Backfill: Transaction rolled back for year {Year}. No invoices in this year group were updated.",
                        year);
                    throw;
                }
            }

            _logger.LogInformation("Backfill: Completed. Total records updated: {TotalUpdated}", totalUpdated);
            return totalUpdated;
        }
        catch (Exception)
        {
            throw;
        }
    }
}
