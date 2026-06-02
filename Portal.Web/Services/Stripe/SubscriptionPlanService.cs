using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Repositories;
using Portal.Web.Models.Stripe;

namespace Portal.Web.Services.Stripe;

/// <summary>
/// Provides subscription status and plan feature access information for a business.
/// Results are cached in HttpContext.Items for the HTTP request lifetime to avoid repeated database queries.
/// </summary>
public class SubscriptionPlanService : ISubscriptionPlanService
{
    private const string CacheKeyPrefix = "SubscriptionAccess_";

    private readonly SubscriptionRepository _subscriptionRepository;
    private readonly PortalDbContext _portalDbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SubscriptionPlanService> _logger;

    public SubscriptionPlanService(
        SubscriptionRepository subscriptionRepository,
        PortalDbContext portalDbContext,
        IHttpContextAccessor httpContextAccessor,
        ILogger<SubscriptionPlanService> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _portalDbContext = portalDbContext;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SubscriptionAccessResult> GetAccessAsync(int businessId)
    {
        try
        {
            // Check request-scoped cache first
            var cached = GetFromCache(businessId);
            if (cached != null)
            {
                return cached;
            }

            // Query subscription by BusinessId
            var subscription = await _subscriptionRepository.GetByBusinessIdAsync(businessId);

            if (subscription == null)
            {
                _logger.LogWarning(
                    "No subscription found for business {BusinessId}",
                    businessId);

                var noSubscriptionResult = new SubscriptionAccessResult
                {
                    HasActiveSubscription = false,
                    SubscriptionStatus = string.Empty,
                    PlanName = string.Empty,
                    IncludedModules = new HashSet<string>()
                };

                StoreInCache(businessId, noSubscriptionResult);
                return noSubscriptionResult;
            }

            // Determine if subscription is active (active, trialing, or past_due)
            var isActive = subscription.Status is "active" or "trialing" or "past_due";
            var isGraceAccess = false;

            // Expiry detection: check if an "active" or "trialing" subscription has passed its billing period end
            if ((subscription.Status is "active" or "trialing") && subscription.CurrentPeriodEnd < DateTime.UtcNow)
            {
                // BusinessId == 1 (Three Inventors) is exempt from expiry detection
                if (subscription.BusinessId != 1)
                {
                    if (!subscription.IsGraceAccessUsed)
                    {
                        // Attempt atomic grace access consumption
                        try
                        {
                            var graceConsumed = await _subscriptionRepository.ConsumeGraceAccessAsync(subscription.Id);

                            if (graceConsumed)
                            {
                                // Grace access granted — allow this request with warning
                                isActive = true;
                                isGraceAccess = true;

                                var httpContext = _httpContextAccessor.HttpContext;
                                if (httpContext != null)
                                {
                                    httpContext.Items["GraceAccessGranted"] = true;
                                }
                            }
                            else
                            {
                                // Race lost — another request already consumed grace access
                                isActive = false;
                            }
                        }
                        catch (Exception ex)
                        {
                            // Fail-open: log warning and allow current request to proceed
                            _logger.LogWarning(ex,
                                "Failed to consume grace access for subscription {SubscriptionId} (business {BusinessId}). Allowing request (fail-open).",
                                subscription.Id, businessId);

                            isActive = true;
                            isGraceAccess = true;
                        }
                    }
                    else
                    {
                        // Grace access already used — deny access
                        isActive = false;
                    }
                }
            }

            // Resolve plan name and features in a single query
            var planData = await _portalDbContext.Plans
                .Where(p => p.Id == subscription.PlanId)
                .Select(p => new
                {
                    p.Name,
                    IncludedModules = p.PlanFeatures
                        .Where(pf => pf.IsIncluded)
                        .Select(pf => pf.ModuleName)
                        .ToList()
                })
                .FirstOrDefaultAsync();

            var result = new SubscriptionAccessResult
            {
                HasActiveSubscription = isActive,
                IsGraceAccess = isGraceAccess,
                SubscriptionStatus = subscription.Status,
                PlanName = planData?.Name ?? string.Empty,
                StripeSubscriptionId = subscription.StripeSubscriptionId,
                IncludedModules = new HashSet<string>(planData?.IncludedModules ?? Enumerable.Empty<string>())
            };

            StoreInCache(businessId, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error resolving subscription access for business {BusinessId}",
                businessId);
            throw;
        }
    }

    private SubscriptionAccessResult? GetFromCache(int businessId)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return null;
        }

        var cacheKey = $"{CacheKeyPrefix}{businessId}";
        if (httpContext.Items.TryGetValue(cacheKey, out var cached) && cached is SubscriptionAccessResult result)
        {
            return result;
        }

        return null;
    }

    private void StoreInCache(int businessId, SubscriptionAccessResult result)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return;
        }

        var cacheKey = $"{CacheKeyPrefix}{businessId}";
        httpContext.Items[cacheKey] = result;
    }
}
