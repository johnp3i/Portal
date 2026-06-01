using Portal.Web.Models.Stripe;

namespace Portal.Web.Services.Stripe;

/// <summary>
/// Provisions a new tenant from a completed Stripe checkout session.
/// Creates Business, UserBusiness, Subscription, StripeCustomer, Invoice, Payment,
/// and Permissions within a single database transaction.
/// </summary>
public interface IProvisioningService
{
    /// <summary>
    /// Provisions a new tenant with all associated records from a completed checkout session.
    /// All operations execute within a single database transaction to ensure atomicity.
    /// Returns a success result with the new BusinessId, or an error if provisioning fails.
    /// </summary>
    Task<ProvisioningResult> ProvisionTenantAsync(ProvisioningRequest request);
}
