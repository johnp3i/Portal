using Portal.Infrastructure.Models.PaymentReminders;

namespace Portal.Web.Models;

/// <summary>
/// View model used by AxPostSaveSchedule to receive the full reminder schedule from the client.
/// </summary>
public class SaveScheduleViewModel
{
    public List<SaveReminderScheduleRequest> Tiers { get; set; } = new();
}
