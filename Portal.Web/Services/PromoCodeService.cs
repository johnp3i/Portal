using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Web.Models.PromoCode;
using InfraFilter = Portal.Infrastructure.Models.PromoCodeFilter;
using WebFilter = Portal.Web.Models.PromoCode.PromoCodeFilter;

namespace Portal.Web.Services;

/// <summary>
/// Service for promo code administration: creation with cryptographic code generation,
/// input validation, revocation, and paginated listing with status derivation.
/// </summary>
public class PromoCodeService : IPromoCodeService
{
    private const string CharacterSet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 8;
    private const int MaxCollisionRetries = 5;

    private readonly PromoCodeRepository _promoCodeRepository;
    private readonly IPlanRepository _planRepository;
    private readonly ILogger<PromoCodeService> _logger;

    public PromoCodeService(
        PromoCodeRepository promoCodeRepository,
        IPlanRepository planRepository,
        ILogger<PromoCodeService> logger)
    {
        _promoCodeRepository = promoCodeRepository;
        _planRepository = planRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PromoCodeCreateResult> CreateAsync(CreatePromoCodeRequest request, string createdByUserId)
    {
        try
        {
            // Input validation
            var validationError = ValidateCreateRequest(request);
            if (validationError != null)
            {
                return new PromoCodeCreateResult
                {
                    Success = false,
                    ErrorMessage = validationError
                };
            }

            // If BoundEmail is provided, force MaxRedemptions to 1
            var maxRedemptions = !string.IsNullOrWhiteSpace(request.BoundEmail)
                ? 1
                : request.MaxRedemptions;

            // Generate unique code with collision retry
            string? generatedCode = null;
            for (int attempt = 1; attempt <= MaxCollisionRetries; attempt++)
            {
                var candidateCode = GenerateCode();
                var exists = await _promoCodeRepository.CodeExistsAsync(candidateCode);

                if (!exists)
                {
                    generatedCode = candidateCode;
                    break;
                }

                _logger.LogWarning("Code generation collision on attempt {AttemptNumber} with code {ExistingCode}",
                    attempt, candidateCode);
            }

            if (generatedCode == null)
            {
                return new PromoCodeCreateResult
                {
                    Success = false,
                    ErrorMessage = "Code generation failed. Please try again."
                };
            }

            // Create the promo code entity
            var promoCode = new PromoCode
            {
                Code = generatedCode,
                DurationMonths = request.DurationMonths,
                MaxRedemptions = maxRedemptions,
                CurrentRedemptions = 0,
                ExpiresAtUtc = request.ExpiresAtUtc,
                BoundEmail = string.IsNullOrWhiteSpace(request.BoundEmail) ? null : request.BoundEmail.Trim(),
                IsRevoked = false,
                PlanId = request.PlanId,
                CreatedByUserId = createdByUserId,
                CreatedAtUtc = DateTime.UtcNow
            };

            var id = await _promoCodeRepository.InsertAsync(promoCode);

            var type = promoCode.BoundEmail != null ? "Email-Bound" : "Generic";
            _logger.LogInformation("Promo code created: UserId={UserId}, PromoCodeId={PromoCodeId}, Code={Code}, Type={Type}",
                createdByUserId, id, generatedCode, type);

            return new PromoCodeCreateResult
            {
                Success = true,
                Code = generatedCode
            };
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> RevokeAsync(int promoCodeId, string revokedByUserId)
    {
        try
        {
            var revoked = await _promoCodeRepository.RevokeAsync(promoCodeId);

            if (!revoked)
            {
                return ServiceResult.Fail("Promo code cannot be revoked. It may already be revoked, expired, or fully redeemed.");
            }

            _logger.LogInformation("Promo code revoked. UserId={UserId}, PromoCodeId={PromoCodeId}",
                revokedByUserId, promoCodeId);

            return ServiceResult.Ok();
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<PagedResult<PromoCodeListItem>> GetAllAsync(WebFilter filter)
    {
        try
        {
            // Map from web filter to infrastructure filter
            var infraFilter = new InfraFilter
            {
                Status = filter.Status,
                Page = filter.Page,
                PageSize = filter.PageSize
            };

            var pagedResult = await _promoCodeRepository.GetFilteredAsync(infraFilter);

            // Load plans for name resolution
            var plans = await _planRepository.GetAllActiveAsync();
            var planLookup = plans.ToDictionary(p => p.Id, p => p.Name);

            // Map entities to list item DTOs with status derivation
            var items = pagedResult.Items.Select(entity =>
            {
                var item = MapToListItem(entity);
                if (entity.PlanId.HasValue && planLookup.TryGetValue(entity.PlanId.Value, out var name))
                    item.PlanName = name;
                return item;
            }).ToList();

            return new PagedResult<PromoCodeListItem>
            {
                Items = items,
                CurrentPage = pagedResult.CurrentPage,
                PageSize = pagedResult.PageSize,
                TotalCount = pagedResult.TotalCount
            };
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<PromoCodeListItem?> GetByIdAsync(int id)
    {
        try
        {
            var entity = await _promoCodeRepository.GetByIdAsync(id);
            if (entity == null)
                return null;

            return MapToListItem(entity);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task IncrementSentCountAsync(int promoCodeId)
    {
        try
        {
            await _promoCodeRepository.IncrementSentCountAsync(promoCodeId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ResetSentCountAsync(int promoCodeId)
    {
        try
        {
            await _promoCodeRepository.ResetSentCountAsync(promoCodeId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Generates an 8-character code using cryptographic randomness from the allowed character set.
    /// Character set: ABCDEFGHJKLMNPQRSTUVWXYZ23456789 (32 chars, excludes O, 0, I, l, 1).
    /// </summary>
    private static string GenerateCode()
    {
        Span<char> code = stackalloc char[CodeLength];

        for (int i = 0; i < CodeLength; i++)
        {
            var index = RandomNumberGenerator.GetInt32(CharacterSet.Length);
            code[i] = CharacterSet[index];
        }

        return new string(code);
    }

    /// <summary>
    /// Validates the create request and returns an error message if invalid, or null if valid.
    /// </summary>
    private static string? ValidateCreateRequest(CreatePromoCodeRequest request)
    {
        // Duration: 1-24 months inclusive
        if (request.DurationMonths < 1 || request.DurationMonths > 24)
        {
            return "Duration must be between 1 and 24 months.";
        }

        // MaxRedemptions: 1-500 inclusive (for generic codes)
        if (string.IsNullOrWhiteSpace(request.BoundEmail))
        {
            if (request.MaxRedemptions < 1 || request.MaxRedemptions > 500)
            {
                return "Max redemptions must be between 1 and 500.";
            }
        }

        // Expiry: at least 1 day (24 hours) in the future from UTC now
        var minimumExpiry = DateTime.UtcNow.AddDays(1);
        if (request.ExpiresAtUtc < minimumExpiry)
        {
            return "Expiry date must be at least 1 day in the future.";
        }

        // BoundEmail: if provided, must be well-formed email
        if (!string.IsNullOrWhiteSpace(request.BoundEmail))
        {
            if (!IsValidEmail(request.BoundEmail.Trim()))
            {
                return "A valid email address is required for email-bound codes.";
            }
        }

        return null;
    }

    /// <summary>
    /// Validates that the email is well-formed using a basic regex pattern.
    /// </summary>
    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        // Basic email validation pattern
        const string pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        return Regex.IsMatch(email, pattern, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Maps a PromoCode entity to a PromoCodeListItem DTO with derived status and type.
    /// Status derivation order:
    ///   1. "Revoked" if IsRevoked = true
    ///   2. "Redeemed" if CurrentRedemptions = MaxRedemptions (and not revoked)
    ///   3. "Expired" if ExpiresAtUtc &lt; UtcNow (and not revoked, not fully redeemed)
    ///   4. "Active" otherwise
    /// </summary>
    private static PromoCodeListItem MapToListItem(PromoCode entity)
    {
        return new PromoCodeListItem
        {
            Id = entity.Id,
            Code = entity.Code,
            Type = entity.BoundEmail != null ? "Email-Bound" : "Generic",
            PlanId = entity.PlanId,
            PlanName = null, // Resolved below if plans are loaded
            DurationMonths = entity.DurationMonths,
            CurrentRedemptions = entity.CurrentRedemptions,
            MaxRedemptions = entity.MaxRedemptions,
            ExpiresAtUtc = entity.ExpiresAtUtc,
            BoundEmail = entity.BoundEmail,
            Status = DeriveStatus(entity),
            SentCount = entity.SentCount,
            CreatedAtUtc = entity.CreatedAtUtc
        };
    }

    /// <summary>
    /// Derives the display status from the promo code entity state.
    /// </summary>
    private static string DeriveStatus(PromoCode entity)
    {
        if (entity.IsRevoked)
            return "Revoked";

        if (entity.CurrentRedemptions >= entity.MaxRedemptions)
            return "Redeemed";

        if (entity.ExpiresAtUtc < DateTime.UtcNow)
            return "Expired";

        return "Active";
    }
}
