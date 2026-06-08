namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository interface for InvoiceSequence entity operations against the [billing].[InvoiceSequence] table.
/// </summary>
public interface IInvoiceSequenceRepository
{
    /// <summary>
    /// Atomically increments and returns the next sequence number for the given year.
    /// Creates the year row if it does not exist.
    /// Throws InvalidOperationException if the annual limit (9999) is exceeded.
    /// Must be called within an active database transaction.
    /// </summary>
    Task<int> IncrementAndGetAsync(int year);
}
