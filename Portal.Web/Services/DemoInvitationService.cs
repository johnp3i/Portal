using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Entities.Identity;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;

namespace Portal.Web.Services;

/// <summary>
/// Service for managing demo access invitations — creation with token generation,
/// validation with access tracking, revocation, resend, and paginated listing.
/// </summary>
public class DemoInvitationService : IDemoInvitationService
{
    private const int TokenByteLength = 32;
    private const int MaxTokenCollisionRetries = 3;

    private readonly DemoInvitationRepository _repository;
    private readonly IEmailService _emailService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<DemoInvitationService> _logger;
    private readonly MembershipDbContext _membershipDbContext;

    public DemoInvitationService(
        DemoInvitationRepository repository,
        IEmailService emailService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<DemoInvitationService> logger,
        MembershipDbContext membershipDbContext)
    {
        _repository = repository;
        _emailService = emailService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _membershipDbContext = membershipDbContext;
    }

    /// <inheritdoc />
    public string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[TokenByteLength];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <inheritdoc />
    public async Task<DemoInvitation> CreateAsync(CreateDemoInvitationRequest request, string createdByUserId)
    {
        try
        {
            // Validate email format
            if (!IsValidEmail(request.RecipientEmail))
            {
                throw new ValidationException("A valid email address is required.");
            }

            // Reject invitations to existing registered users (but allow for demo-only users)
            var normalizedEmail = request.RecipientEmail.Trim().ToUpperInvariant();
            var existingUser = await _membershipDbContext.Users
                .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);
            if (existingUser != null)
            {
                var roles = await _membershipDbContext.UserRoles
                    .Where(ur => ur.UserId == existingUser.Id)
                    .Join(_membershipDbContext.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                    .ToListAsync();

                // Allow if user only has the DemoUser role (or no roles at all) — they're a previous demo recipient
                var isOnlyDemoUser = roles.Count == 0 || (roles.Count == 1 && roles[0] == "DemoUser");
                if (!isOnlyDemoUser)
                {
                    throw new ValidationException("This email belongs to an existing user. Demo invitations can only be sent to new prospects.");
                }
            }

            // Validate business is a demo account
            var demoBusinesses = await _repository.GetDemoBusinessesAsync();
            var business = demoBusinesses.FirstOrDefault(b => b.Id == request.BusinessId);
            if (business == null)
            {
                throw new ValidationException("The selected business is not a valid demo account.");
            }

            // Validate expiry is in the future
            if (request.ExpiresAtUtc <= DateTime.UtcNow)
            {
                throw new ValidationException("Expiry date must be in the future.");
            }

            // Validate at least one permission with 'full' or 'readonly'
            var hasGrantedPermission = request.Permissions.Any(p =>
                p.AccessLevel == AccessLevels.Full || p.AccessLevel == AccessLevels.ReadOnly);

            if (!hasGrantedPermission)
            {
                throw new ValidationException("At least one module must have 'full' or 'readonly' access.");
            }

            // Generate unique token with collision retry
            string? token = null;
            for (int attempt = 1; attempt <= MaxTokenCollisionRetries; attempt++)
            {
                var candidateToken = GenerateToken();
                var existing = await _repository.GetByTokenAsync(candidateToken);

                if (existing == null)
                {
                    token = candidateToken;
                    break;
                }

                _logger.LogWarning("Token generation collision on attempt {AttemptNumber}", attempt);
            }

            if (token == null)
            {
                throw new InvalidOperationException("Token generation failed after 3 attempts. Please try again.");
            }

            // Create invitation entity
            var invitation = new DemoInvitation
            {
                BusinessId = request.BusinessId,
                Token = token,
                RecipientEmail = request.RecipientEmail.Trim(),
                RecipientName = string.IsNullOrWhiteSpace(request.RecipientName) ? null : request.RecipientName.Trim(),
                ExpiresAtUtc = request.ExpiresAtUtc,
                Status = "sent",
                CreatedByUserId = createdByUserId,
                AccessCount = 0,
                CreatedAtUtc = DateTime.UtcNow
            };

            // Create permission entities
            var permissions = request.Permissions
                .Select(p => new DemoInvitationPermission
                {
                    Module = p.Module,
                    AccessLevel = p.AccessLevel,
                    CreatedAtUtc = DateTime.UtcNow
                })
                .ToList();

            // Persist invitation and permissions
            await _repository.InsertAsync(invitation, permissions);

            // Send invitation email
            try
            {
                var magicLink = BuildMagicLink(token);
                await _emailService.SendDemoInvitationEmailAsync(
                    request.RecipientEmail.Trim(),
                    magicLink,
                    business.Name,
                    request.ExpiresAtUtc);

                _logger.LogInformation("Demo invitation email sent to {Email} for business {Business}",
                    request.RecipientEmail, business.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send demo invitation email to {Email}. Invitation persisted with Id {InvitationId}",
                    request.RecipientEmail, invitation.Id);
            }

            return invitation;
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<DemoInvitationValidationResult> ValidateAndTrackAccessAsync(string token)
    {
        try
        {
            var invitation = await _repository.GetByTokenAsync(token);

            if (invitation == null)
            {
                return new DemoInvitationValidationResult
                {
                    IsValid = false,
                    ErrorReason = "invalid"
                };
            }

            // Check if revoked
            if (invitation.Status == "revoked")
            {
                return new DemoInvitationValidationResult
                {
                    IsValid = false,
                    ErrorReason = "revoked",
                    Invitation = invitation
                };
            }

            // Check if expired
            if (invitation.ExpiresAtUtc <= DateTime.UtcNow)
            {
                // Update status to expired
                await _repository.UpdateStatusAsync(invitation.Id, "expired");

                return new DemoInvitationValidationResult
                {
                    IsValid = false,
                    ErrorReason = "expired",
                    Invitation = invitation
                };
            }

            // Valid token — track access
            var now = DateTime.UtcNow;
            var isFirstAccess = invitation.FirstAccessedAtUtc == null;

            await _repository.UpdateAccessTrackingAsync(invitation.Id, now, isFirstAccess);

            // Update local entity to reflect changes
            invitation.AccessCount += 1;
            invitation.LastAccessedAtUtc = now;
            if (isFirstAccess)
            {
                invitation.FirstAccessedAtUtc = now;
                invitation.Status = "accessed";
            }

            return new DemoInvitationValidationResult
            {
                IsValid = true,
                Invitation = invitation
            };
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task RevokeAsync(int invitationId)
    {
        try
        {
            var now = DateTime.UtcNow;
            await _repository.UpdateStatusAsync(invitationId, "revoked", now);

            _logger.LogInformation("Demo invitation {InvitationId} revoked at {RevokedAtUtc}", invitationId, now);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ResendEmailAsync(int invitationId)
    {
        try
        {
            var invitations = await _repository.GetAllAsync();
            var invitation = invitations.FirstOrDefault(i => i.Id == invitationId);

            if (invitation == null)
            {
                throw new InvalidOperationException("Invitation not found.");
            }

            // Verify status is 'sent' or 'accessed' and not expired
            if (invitation.Status != "sent" && invitation.Status != "accessed")
            {
                throw new InvalidOperationException("Cannot resend email for an invitation that is expired or revoked.");
            }

            if (invitation.ExpiresAtUtc <= DateTime.UtcNow)
            {
                throw new InvalidOperationException("Cannot resend email for an expired invitation.");
            }

            // Resolve business name
            var businesses = await _repository.GetDemoBusinessesAsync();
            var business = businesses.FirstOrDefault(b => b.Id == invitation.BusinessId);
            var businessName = business?.Name ?? "Demo Business";

            var magicLink = BuildMagicLink(invitation.Token);
            await _emailService.SendDemoInvitationEmailAsync(
                invitation.RecipientEmail,
                magicLink,
                businessName,
                invitation.ExpiresAtUtc);

            _logger.LogInformation("Demo invitation email resent to {Email} for invitation {InvitationId}",
                invitation.RecipientEmail, invitationId);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<PagedResult<DemoInvitationListItem>> GetAllPagedAsync(int page, int pageSize)
    {
        try
        {
            var totalCount = await _repository.GetTotalCountAsync();
            var invitations = await _repository.GetPagedAsync(page, pageSize);

            // Resolve business names
            var businesses = await _repository.GetDemoBusinessesAsync();
            var businessLookup = businesses.ToDictionary(b => b.Id, b => b.Name);

            var items = invitations.Select(inv => new DemoInvitationListItem
            {
                Id = inv.Id,
                RecipientEmail = inv.RecipientEmail,
                RecipientName = inv.RecipientName,
                BusinessName = businessLookup.GetValueOrDefault(inv.BusinessId, "Unknown"),
                Status = inv.Status,
                ExpiresAtUtc = inv.ExpiresAtUtc,
                AccessCount = inv.AccessCount,
                FirstAccessedAtUtc = inv.FirstAccessedAtUtc,
                CreatedAtUtc = inv.CreatedAtUtc
            }).ToList();

            return new PagedResult<DemoInvitationListItem>
            {
                Items = items,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<DemoBusinessItem>> GetDemoBusinessesAsync()
    {
        try
        {
            var businesses = await _repository.GetDemoBusinessesAsync();

            return businesses.Select(b => new DemoBusinessItem
            {
                Id = b.Id,
                Name = b.Name
            }).ToList();
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string>> GetPermissionsForInvitationAsync(int invitationId)
    {
        try
        {
            var permissions = await _repository.GetPermissionsByInvitationIdAsync(invitationId);

            return permissions.ToDictionary(p => p.Module, p => p.AccessLevel);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task EnsureDemoUserBusinessAsync(string userId, int businessId)
    {
        try
        {
            var exists = await _membershipDbContext.UserBusinesses
                .AnyAsync(ub => ub.UserId == userId && ub.BusinessId == businessId);

            if (!exists)
            {
                var userBusiness = new UserBusiness
                {
                    UserId = userId,
                    BusinessId = businessId,
                    IsDefault = true,
                    IsActive = true,
                    IsOwner = false,
                    CreatedAtUtc = DateTime.UtcNow
                };

                _membershipDbContext.UserBusinesses.Add(userBusiness);
                await _membershipDbContext.SaveChangesAsync();

                _logger.LogInformation("Created UserBusiness for demo user {UserId} and business {BusinessId}",
                    userId, businessId);
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdatePermissionsAsync(int invitationId, List<ModulePermissionEntry> permissions)
    {
        try
        {
            // Validate at least one permission with 'full' or 'readonly'
            var hasGrantedPermission = permissions.Any(p =>
                p.AccessLevel == AccessLevels.Full || p.AccessLevel == AccessLevels.ReadOnly);

            if (!hasGrantedPermission)
            {
                throw new ValidationException("At least one module must have 'full' or 'readonly' access.");
            }

            // Delete existing permissions and reinsert
            await _repository.DeletePermissionsByInvitationIdAsync(invitationId);

            var permissionEntities = permissions
                .Select(p => new DemoInvitationPermission
                {
                    DemoInvitationId = invitationId,
                    Module = p.Module,
                    AccessLevel = p.AccessLevel,
                    CreatedAtUtc = DateTime.UtcNow
                })
                .ToList();

            await _repository.InsertPermissionsAsync(invitationId, permissionEntities);

            _logger.LogInformation("Updated permissions for invitation {InvitationId}", invitationId);
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Validates that the email is well-formed using a basic regex pattern.
    /// </summary>
    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        const string pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        return Regex.IsMatch(email, pattern, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Builds the full magic link URL for the demo entry endpoint.
    /// </summary>
    private string BuildMagicLink(string token)
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request == null)
            return $"/Demo/Enter?token={token}";

        return $"{request.Scheme}://{request.Host}/Demo/Enter?token={token}";
    }
}
