using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Generates the narrative briefing for the dashboard by evaluating business signals.
/// </summary>
public interface IDashboardBriefingService
{
    Task<BriefingViewModel> GenerateBriefingAsync(int businessId, DashboardScopeDto scope, string currencySymbol);
}
