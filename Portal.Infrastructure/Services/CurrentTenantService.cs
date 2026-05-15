using Microsoft.AspNetCore.Http;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Resolves the current tenant's BusinessId from the authenticated user's claims.
/// Registered as a scoped service (one per request).
/// </summary>
public class CurrentTenantService : ICurrentTenantService
{
    /// <summary>
    /// The claim type used to store the BusinessId in the user's authentication token.
    /// </summary>
    public const string BusinessIdClaimType = "BusinessId";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentTenantService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public int CurrentBusinessId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst(BusinessIdClaimType);

            if (claim is not null && int.TryParse(claim.Value, out var businessId))
            {
                return businessId;
            }

            return 0;
        }
    }
}
