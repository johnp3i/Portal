using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Determines whether a VAT deadline conflict warning should be displayed
/// when creating a payment schedule, based on the invoice's VAT submission period
/// and the first instalment's timing.
/// </summary>
public interface IVatWarningService
{
    /// <summary>
    /// Evaluates whether the first instalment's due date conflicts with the invoice's
    /// VAT submission deadline. Returns a warning DTO if a conflict exists, or null if no warning is needed.
    /// </summary>
    /// <param name="invoiceId">The invoice to check for VAT period assignment.</param>
    /// <param name="firstInstalmentDueDate">The due date of the first instalment (null = no comparison possible).</param>
    /// <param name="firstInstalmentAmount">The amount of the first instalment.</param>
    /// <param name="businessId">The business tenant ID.</param>
    /// <returns>A <see cref="VatWarningDto"/> if a warning should be shown, or null otherwise.</returns>
    Task<VatWarningDto?> GetVatWarningAsync(int invoiceId, DateOnly? firstInstalmentDueDate, decimal firstInstalmentAmount, int businessId);
}
