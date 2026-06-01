using Portal.Web.Models.Stripe;

namespace Portal.Web.Services.Stripe;

/// <summary>
/// Manages the post-signup setup wizard flow where new business owners configure
/// their business details before accessing the dashboard.
/// </summary>
public interface ISetupWizardService
{
    /// <summary>
    /// Checks whether the business has completed the setup wizard (has a BusinessProfile record).
    /// </summary>
    Task<bool> IsSetupCompleteAsync(int businessId);

    /// <summary>
    /// Validates and saves the setup wizard form data.
    /// Creates a BusinessProfile record and updates Business.Name with the provided business name.
    /// </summary>
    Task<SetupWizardResult> CompleteSetupAsync(int businessId, SetupWizardModel model);

    /// <summary>
    /// Checks if a business name is already in use by another tenant.
    /// Returns true if the name is taken by a business other than the excluded one.
    /// </summary>
    Task<bool> IsBusinessNameTakenAsync(string name, int excludeBusinessId);
}
