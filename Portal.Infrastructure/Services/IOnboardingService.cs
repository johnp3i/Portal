using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

public interface IOnboardingService
{
    Task<OnboardingStateDto> GetOnboardingStateAsync(int businessId);
    Task DismissOnboardingAsync(int businessId);
}
