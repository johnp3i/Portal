using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Generates VAT submission periods based on the tenant's first user-defined period
/// and VatPeriodLengthInMonths from BusinessProfile.
/// 
/// The first period is always created manually by the user (to accommodate government-assigned
/// VAT cycles that may not align with the registration date). Subsequent periods are auto-generated
/// as calendar-month-aligned blocks derived from the previous period's end date.
/// </summary>
public class VatPeriodGenerationService : IVatPeriodGenerationService
{
    private static readonly HashSet<int> AllowedPeriodLengths = new() { 1, 2, 3, 4, 6, 12 };

    private readonly ICurrentTenantService _currentTenantService;
    private readonly VatSubmissionPeriodRepository _vatSubmissionPeriodRepository;
    private readonly PortalDbContext _portalDbContext;

    public VatPeriodGenerationService(
        ICurrentTenantService currentTenantService,
        VatSubmissionPeriodRepository vatSubmissionPeriodRepository,
        PortalDbContext portalDbContext)
    {
        _currentTenantService = currentTenantService;
        _vatSubmissionPeriodRepository = vatSubmissionPeriodRepository;
        _portalDbContext = portalDbContext;
    }

    /// <inheritdoc />
    public async Task<List<VatSubmissionPeriod>> GeneratePeriodsAsync()
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        // Retrieve the BusinessProfile for the current tenant
        var businessProfile = await _portalDbContext.BusinessProfiles
            .FirstOrDefaultAsync(bp => bp.BusinessId == businessId);

        if (businessProfile == null)
        {
            return new List<VatSubmissionPeriod>();
        }

        // Validate VatPeriodLengthInMonths ∈ {1, 2, 3, 4, 6, 12}
        if (!AllowedPeriodLengths.Contains(businessProfile.VatPeriodLengthInMonths))
        {
            return new List<VatSubmissionPeriod>();
        }

        var periodLengthInMonths = businessProfile.VatPeriodLengthInMonths;
        var currentDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // Get the latest existing period — if none exists, the user must create the first one manually
        var latestPeriod = await _vatSubmissionPeriodRepository.GetLatestByBusinessIdAsync(businessId);

        if (latestPeriod == null)
        {
            // No periods exist — user must create the first period manually
            return new List<VatSubmissionPeriod>();
        }

        // Generate subsequent periods from the latest period forward
        // Next period starts on the 1st of the month after the latest period's end month
        var nextStart = new DateOnly(latestPeriod.PeriodEndDate.Year, latestPeriod.PeriodEndDate.Month, 1)
            .AddMonths(1);

        while (nextStart <= currentDate)
        {
            // End date is the last day of (startMonth + periodLength - 1)
            var endDate = nextStart.AddMonths(periodLengthInMonths).AddDays(-1);

            // PeriodLabel uses en-dash (–) not hyphen (-)
            var periodLabel = $"{nextStart:dd MMM yyyy} \u2013 {endDate:dd MMM yyyy}";

            var newPeriod = new VatSubmissionPeriod
            {
                BusinessId = businessId,
                PeriodStartDate = nextStart,
                PeriodEndDate = endDate,
                PeriodLabel = periodLabel,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _vatSubmissionPeriodRepository.InsertAsync(newPeriod);

            // Next period starts on the 1st of the month after this period's end
            nextStart = new DateOnly(endDate.Year, endDate.Month, 1).AddMonths(1);
        }

        // Return all periods ordered by PeriodStartDate descending
        return await _vatSubmissionPeriodRepository.GetAllByBusinessIdAsync(businessId);
    }

    /// <summary>
    /// Creates the first VAT period for a tenant. The user defines the start and end months.
    /// Both dates are calendar-month-aligned (1st of start month, last day of end month).
    /// </summary>
    public async Task<ServiceResult> CreateFirstPeriodAsync(int startYear, int startMonth, int endYear, int endMonth)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        // Validate that no periods already exist
        var existing = await _vatSubmissionPeriodRepository.GetLatestByBusinessIdAsync(businessId);
        if (existing != null)
        {
            return ServiceResult.Fail("A VAT period already exists. Subsequent periods are generated automatically.");
        }

        // Validate month/year ranges
        if (startMonth < 1 || startMonth > 12 || endMonth < 1 || endMonth > 12)
        {
            return ServiceResult.Fail("Invalid month value. Must be between 1 and 12.");
        }

        if (startYear < 2000 || endYear < 2000)
        {
            return ServiceResult.Fail("Invalid year value.");
        }

        var periodStart = new DateOnly(startYear, startMonth, 1);
        var periodEnd = new DateOnly(endYear, endMonth, DateTime.DaysInMonth(endYear, endMonth));

        // Validate end is after start
        if (periodEnd <= periodStart)
        {
            return ServiceResult.Fail("The end date must be after the start date.");
        }

        // PeriodLabel uses en-dash (–)
        var periodLabel = $"{periodStart:dd MMM yyyy} \u2013 {periodEnd:dd MMM yyyy}";

        var newPeriod = new VatSubmissionPeriod
        {
            BusinessId = businessId,
            PeriodStartDate = periodStart,
            PeriodEndDate = periodEnd,
            PeriodLabel = periodLabel,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _vatSubmissionPeriodRepository.InsertAsync(newPeriod);

        return ServiceResult.Ok();
    }
}
