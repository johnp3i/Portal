using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.PaymentReminders;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Manages payment reminder schedule configuration including CRUD operations,
/// default resolution, and tier validation for a business.
/// </summary>
public interface IPaymentReminderScheduleService
{
    /// <summary>
    /// Returns the reminder schedule for the specified business,
    /// or system defaults if no schedule has been configured.
    /// </summary>
    Task<List<PaymentReminderScheduleDto>> GetScheduleAsync(int businessId);

    /// <summary>
    /// Saves or updates the full reminder schedule (3 tiers) for a business.
    /// </summary>
    Task SaveScheduleAsync(int businessId, List<SaveReminderScheduleRequest> tiers);

    /// <summary>
    /// Validates tier ordering and value constraints.
    /// Returns a failed ServiceResult with a message if validation fails.
    /// </summary>
    ServiceResult ValidateSchedule(List<SaveReminderScheduleRequest> tiers);
}
