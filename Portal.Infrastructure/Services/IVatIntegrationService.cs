using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Computes VAT-related KPIs for the revenue dashboard.
/// Provides Output VAT, Input VAT, Net VAT Payable, and period-over-period liability data.
/// </summary>
public interface IVatIntegrationService
{
    /// <summary>
    /// Computes Output VAT, Input VAT, Net VAT Payable, and Output/Input ratio
    /// for the current VAT period.
    /// </summary>
    /// <param name="businessId">The business tenant ID.</param>
    /// <returns>A <see cref="VatSummaryDto"/> with current period VAT metrics.</returns>
    Task<VatSummaryDto> GetCurrentPeriodSummaryAsync(int businessId);

    /// <summary>
    /// Returns Net VAT Payable values for the last 6 VAT periods.
    /// </summary>
    /// <param name="businessId">The business tenant ID.</param>
    /// <returns>A list of <see cref="VatPeriodLiabilityDto"/> for the last 6 periods.</returns>
    Task<List<VatPeriodLiabilityDto>> GetVatLiabilityByPeriodAsync(int businessId);
}
