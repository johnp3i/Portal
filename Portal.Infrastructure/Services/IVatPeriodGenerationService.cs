using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Generates VAT submission periods for a tenant. The first period is user-defined
/// (to accommodate government-assigned VAT cycles). Subsequent periods are auto-generated
/// from the latest period's end date using VatPeriodLengthInMonths from BusinessProfile.
/// </summary>
public interface IVatPeriodGenerationService
{
    /// <summary>
    /// Generates all missing VAT periods from the latest existing period forward to the current date.
    /// If no periods exist, returns an empty list (user must create the first period manually).
    /// Returns the complete list of periods ordered by PeriodStartDate descending.
    /// </summary>
    Task<List<VatSubmissionPeriod>> GeneratePeriodsAsync();

    /// <summary>
    /// Creates the first VAT period for a tenant. The user defines the start and end months.
    /// Both dates are calendar-month-aligned (1st of start month, last day of end month).
    /// </summary>
    Task<ServiceResult> CreateFirstPeriodAsync(int startYear, int startMonth, int endYear, int endMonth);
}
