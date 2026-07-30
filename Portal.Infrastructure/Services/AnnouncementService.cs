using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for feature announcements — visibility filtering, dismissals, and admin CRUD.
/// </summary>
public class AnnouncementService : IAnnouncementService
{
    private readonly AnnouncementRepository _repository;
    private readonly IPlanCheckService _planCheckService;

    private static readonly Dictionary<string, int> TierRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Starter"] = 1,
        ["Foundation"] = 1,
        ["Professional"] = 2,
        ["Enterprise"] = 3
    };

    public AnnouncementService(AnnouncementRepository repository, IPlanCheckService planCheckService)
    {
        _repository = repository;
        _planCheckService = planCheckService;
    }

    /// <inheritdoc />
    public async Task<List<AnnouncementDto>> GetVisibleForUserAsync(string userId)
    {
        try
        {
            var userTier = await _planCheckService.GetCurrentPlanNameAsync() ?? "Starter";
            var utcNow = DateTime.UtcNow;

            var announcements = await _repository.GetVisibleAsync(utcNow);
            var dismissals = await _repository.GetDismissalsForUserAsync(userId);
            var dismissedIds = new HashSet<int>(dismissals.Select(d => d.FeatureAnnouncementId));

            return announcements
                .Where(a => IsTierVisible(a.TargetPlanTier, userTier))
                .Select(a => new AnnouncementDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    Summary = a.Summary,
                    DetailHtml = a.DetailHtml,
                    ModuleKey = a.ModuleKey,
                    CtaLabel = a.CtaLabel,
                    CtaUrl = a.CtaUrl,
                    TargetPlanTier = a.TargetPlanTier,
                    PublishedAtUtc = a.PublishedAtUtc,
                    IsDismissed = dismissedIds.Contains(a.Id)
                })
                .ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> GetUnreadCountAsync(string userId)
    {
        try
        {
            var visible = await GetVisibleForUserAsync(userId);
            return visible.Count(a => !a.IsDismissed);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<AnnouncementDto?> GetBannerAnnouncementAsync(string userId)
    {
        try
        {
            var visible = await GetVisibleForUserAsync(userId);
            return visible
                .Where(a => !a.IsDismissed)
                .OrderByDescending(a => a.PublishedAtUtc)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> DismissAsync(string userId, int announcementId)
    {
        try
        {
            await _repository.DismissAsync(userId, announcementId);
            return await GetUnreadCountAsync(userId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> DismissAllAsync(string userId)
    {
        try
        {
            var visible = await GetVisibleForUserAsync(userId);
            var undismissedIds = visible.Where(a => !a.IsDismissed).Select(a => a.Id).ToList();

            if (undismissedIds.Count > 0)
            {
                await _repository.DismissAllAsync(userId, undismissedIds);
            }

            return 0;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<AdminAnnouncementDto>> GetAllForAdminAsync()
    {
        try
        {
            var announcements = await _repository.GetAllAsync();
            return announcements.Select(a => MapToAdminDto(a)).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<AdminAnnouncementDto?> GetByIdForAdminAsync(int id)
    {
        try
        {
            var entity = await _repository.GetByIdAsync(id);
            return entity != null ? MapToAdminDto(entity) : null;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult<int>> CreateAsync(CreateAnnouncementRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return ServiceResult<int>.Fail("Title is required.");
            if (string.IsNullOrWhiteSpace(request.Summary))
                return ServiceResult<int>.Fail("Summary is required.");
            if (request.PublishedAtUtc == default)
                return ServiceResult<int>.Fail("Publish date is required.");

            var entity = new FeatureAnnouncement
            {
                Title = request.Title.Trim(),
                Summary = request.Summary.Trim(),
                DetailHtml = request.DetailHtml,
                ModuleKey = request.ModuleKey,
                CtaLabel = request.CtaLabel,
                CtaUrl = request.CtaUrl,
                TargetPlanTier = request.TargetPlanTier,
                IsActive = request.IsActive,
                PublishedAtUtc = request.PublishedAtUtc,
                ExpiresAtUtc = request.ExpiresAtUtc
            };

            var id = await _repository.InsertAsync(entity);
            return ServiceResult<int>.Ok(id);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateAsync(UpdateAnnouncementRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return ServiceResult.Fail("Title is required.");
            if (string.IsNullOrWhiteSpace(request.Summary))
                return ServiceResult.Fail("Summary is required.");
            if (request.PublishedAtUtc == default)
                return ServiceResult.Fail("Publish date is required.");

            var existing = await _repository.GetByIdAsync(request.Id);
            if (existing == null)
                return ServiceResult.Fail("Announcement not found.");

            existing.Title = request.Title.Trim();
            existing.Summary = request.Summary.Trim();
            existing.DetailHtml = request.DetailHtml;
            existing.ModuleKey = request.ModuleKey;
            existing.CtaLabel = request.CtaLabel;
            existing.CtaUrl = request.CtaUrl;
            existing.TargetPlanTier = request.TargetPlanTier;
            existing.IsActive = request.IsActive;
            existing.PublishedAtUtc = request.PublishedAtUtc;
            existing.ExpiresAtUtc = request.ExpiresAtUtc;

            await _repository.UpdateAsync(existing);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ToggleActiveAsync(int id, bool isActive)
    {
        try
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
                return ServiceResult.Fail("Announcement not found.");

            await _repository.ToggleActiveAsync(id, isActive);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Determines whether an announcement's target tier is visible to a user's plan tier.
    /// NULL or "All" target = visible to everyone.
    /// </summary>
    private static bool IsTierVisible(string? targetTier, string userTier)
    {
        if (string.IsNullOrEmpty(targetTier) || targetTier.Equals("All", StringComparison.OrdinalIgnoreCase))
            return true;

        var targetRank = TierRank.GetValueOrDefault(targetTier, 0);
        var userRank = TierRank.GetValueOrDefault(userTier, 1);

        return userRank >= targetRank;
    }

    private static AdminAnnouncementDto MapToAdminDto(FeatureAnnouncement a)
    {
        return new AdminAnnouncementDto
        {
            Id = a.Id,
            Title = a.Title,
            Summary = a.Summary,
            DetailHtml = a.DetailHtml,
            ModuleKey = a.ModuleKey,
            CtaLabel = a.CtaLabel,
            CtaUrl = a.CtaUrl,
            TargetPlanTier = a.TargetPlanTier,
            IsActive = a.IsActive,
            PublishedAtUtc = a.PublishedAtUtc,
            ExpiresAtUtc = a.ExpiresAtUtc,
            CreatedAtUtc = a.CreatedAtUtc
        };
    }
}
