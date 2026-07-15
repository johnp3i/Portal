using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Generates a system health briefing for SuperAdmin users by evaluating
/// platform-level signals from the Logging and Portal databases.
/// </summary>
public interface ISystemBriefingService
{
    Task<BriefingViewModel> GenerateBriefingAsync();
}
