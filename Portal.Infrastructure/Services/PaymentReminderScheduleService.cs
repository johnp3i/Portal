using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.PaymentReminders;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Manages payment reminder schedule configuration including CRUD operations,
/// default resolution, and tier validation for a business.
/// </summary>
public class PaymentReminderScheduleService : IPaymentReminderScheduleService
{
    private readonly PortalDbContext _context;

    public PaymentReminderScheduleService(PortalDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<List<PaymentReminderScheduleDto>> GetScheduleAsync(int businessId)
    {
        try
        {
            var schedules = await _context.PaymentReminderSchedules
                .Where(s => s.BusinessId == businessId)
                .ToListAsync();

            if (schedules.Count == 0)
            {
                return GetSystemDefaults();
            }

            return schedules.Select(s => new PaymentReminderScheduleDto
            {
                EscalationTier = s.EscalationTier,
                DaysOffset = s.DaysOffset,
                MaxRemindersPerTier = s.MaxRemindersPerTier,
                MinIntervalDays = s.MinIntervalDays,
                PartialPaymentSuppressionDays = s.PartialPaymentSuppressionDays,
                IsEnabled = s.IsEnabled
            }).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SaveScheduleAsync(int businessId, List<SaveReminderScheduleRequest> tiers)
    {
        try
        {
            foreach (var tier in tiers)
            {
                var existing = await _context.PaymentReminderSchedules
                    .FirstOrDefaultAsync(s => s.BusinessId == businessId && s.EscalationTier == tier.EscalationTier);

                if (existing != null)
                {
                    existing.DaysOffset = tier.DaysOffset;
                    existing.MaxRemindersPerTier = tier.MaxRemindersPerTier;
                    existing.MinIntervalDays = tier.MinIntervalDays;
                    existing.PartialPaymentSuppressionDays = tier.PartialPaymentSuppressionDays;
                    existing.IsEnabled = tier.IsEnabled;
                    existing.UpdatedAtUtc = DateTime.UtcNow;
                }
                else
                {
                    var newSchedule = new PaymentReminderSchedule
                    {
                        BusinessId = businessId,
                        EscalationTier = tier.EscalationTier,
                        DaysOffset = tier.DaysOffset,
                        MaxRemindersPerTier = tier.MaxRemindersPerTier,
                        MinIntervalDays = tier.MinIntervalDays,
                        PartialPaymentSuppressionDays = tier.PartialPaymentSuppressionDays,
                        IsEnabled = tier.IsEnabled,
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow
                    };

                    _context.PaymentReminderSchedules.Add(newSchedule);
                }
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public ServiceResult ValidateSchedule(List<SaveReminderScheduleRequest> tiers)
    {
        try
        {
            if (tiers == null || tiers.Count != 3)
            {
                return ServiceResult.Fail("Schedule must contain exactly 3 tiers (Friendly, Firm, Formal).");
            }

            var friendly = tiers.FirstOrDefault(t => t.EscalationTier == "Friendly");
            var firm = tiers.FirstOrDefault(t => t.EscalationTier == "Firm");
            var formal = tiers.FirstOrDefault(t => t.EscalationTier == "Formal");

            if (friendly == null || firm == null || formal == null)
            {
                return ServiceResult.Fail("Schedule must contain exactly 3 tiers (Friendly, Firm, Formal).");
            }

            if (friendly.DaysOffset >= firm.DaysOffset)
            {
                return ServiceResult.Fail("Friendly days offset must be less than Firm days offset.");
            }

            if (firm.DaysOffset >= formal.DaysOffset)
            {
                return ServiceResult.Fail("Firm days offset must be less than Formal days offset.");
            }

            foreach (var tier in tiers)
            {
                if (tier.MaxRemindersPerTier < 1 || tier.MaxRemindersPerTier > 5)
                {
                    return ServiceResult.Fail($"Max reminders per tier must be between 1 and 5 for {tier.EscalationTier}.");
                }

                if (tier.MinIntervalDays < 1 || tier.MinIntervalDays > 30)
                {
                    return ServiceResult.Fail($"Min interval days must be between 1 and 30 for {tier.EscalationTier}.");
                }

                if (tier.PartialPaymentSuppressionDays < 1 || tier.PartialPaymentSuppressionDays > 30)
                {
                    return ServiceResult.Fail($"Partial payment suppression days must be between 1 and 30 for {tier.EscalationTier}.");
                }
            }

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns the system default schedule when no business-specific schedule has been configured.
    /// Defaults: Friendly at -3 days (enabled), Firm at +7 days (disabled), Formal at +21 days (disabled).
    /// </summary>
    private static List<PaymentReminderScheduleDto> GetSystemDefaults()
    {
        return new List<PaymentReminderScheduleDto>
        {
            new PaymentReminderScheduleDto
            {
                EscalationTier = "Friendly",
                DaysOffset = -3,
                MaxRemindersPerTier = 1,
                MinIntervalDays = 3,
                PartialPaymentSuppressionDays = 7,
                IsEnabled = true
            },
            new PaymentReminderScheduleDto
            {
                EscalationTier = "Firm",
                DaysOffset = 7,
                MaxRemindersPerTier = 2,
                MinIntervalDays = 5,
                PartialPaymentSuppressionDays = 7,
                IsEnabled = false
            },
            new PaymentReminderScheduleDto
            {
                EscalationTier = "Formal",
                DaysOffset = 21,
                MaxRemindersPerTier = 1,
                MinIntervalDays = 7,
                PartialPaymentSuppressionDays = 7,
                IsEnabled = false
            }
        };
    }
}
