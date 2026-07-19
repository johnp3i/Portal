using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for Revenue Source management (POS devices/registers).
/// </summary>
public class RevenueSourceService : IRevenueSourceService
{
    private readonly RevenueSourceRepository _revenueSourceRepository;
    private readonly AuditLogRepository _auditLogRepository;
    private readonly ICurrentTenantService _currentTenantService;

    public RevenueSourceService(
        RevenueSourceRepository revenueSourceRepository,
        AuditLogRepository auditLogRepository,
        ICurrentTenantService currentTenantService)
    {
        _revenueSourceRepository = revenueSourceRepository;
        _auditLogRepository = auditLogRepository;
        _currentTenantService = currentTenantService;
    }

    public async Task<List<RevenueSource>> GetAllAsync()
    {
        return await _revenueSourceRepository.GetAllByBusinessIdAsync(_currentTenantService.CurrentBusinessId);
    }

    public async Task<List<RevenueSource>> GetActiveAsync()
    {
        return await _revenueSourceRepository.GetActiveByBusinessIdAsync(_currentTenantService.CurrentBusinessId);
    }

    public async Task<RevenueSource?> GetByIdAsync(int id)
    {
        return await _revenueSourceRepository.GetByIdAndBusinessIdAsync(id, _currentTenantService.CurrentBusinessId);
    }

    public async Task<ServiceResult> CreateAsync(RevenueSource source)
    {
        if (string.IsNullOrWhiteSpace(source.Name))
        {
            return ServiceResult.Fail("Revenue source name is required.");
        }

        if (source.Name.Length > 200)
        {
            return ServiceResult.Fail("Revenue source name must not exceed 200 characters.");
        }

        source.BusinessId = _currentTenantService.CurrentBusinessId;
        source.IsActive = true;
        source.CreatedAtUtc = DateTime.UtcNow;

        var newId = await _revenueSourceRepository.InsertAsync(source);

        await _auditLogRepository.InsertAsync(new AuditLog
        {
            BusinessId = _currentTenantService.CurrentBusinessId,
            Action = "Create",
            TableName = "revenue.RevenueSource",
            RecordId = newId.ToString(),
            Timestamp = DateTime.UtcNow
        });

        return ServiceResult.Ok(newId);
    }

    public async Task<ServiceResult> UpdateAsync(RevenueSource source)
    {
        if (string.IsNullOrWhiteSpace(source.Name))
        {
            return ServiceResult.Fail("Revenue source name is required.");
        }

        if (source.Name.Length > 200)
        {
            return ServiceResult.Fail("Revenue source name must not exceed 200 characters.");
        }

        var existing = await _revenueSourceRepository.GetByIdAndBusinessIdAsync(source.Id, _currentTenantService.CurrentBusinessId);
        if (existing == null)
        {
            return ServiceResult.Fail("Revenue source not found.");
        }

        source.BusinessId = _currentTenantService.CurrentBusinessId;
        await _revenueSourceRepository.UpdateAsync(source);

        await _auditLogRepository.InsertAsync(new AuditLog
        {
            BusinessId = _currentTenantService.CurrentBusinessId,
            Action = "Update",
            TableName = "revenue.RevenueSource",
            RecordId = source.Id.ToString(),
            Timestamp = DateTime.UtcNow
        });

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ToggleActiveAsync(int id, bool isActive)
    {
        var existing = await _revenueSourceRepository.GetByIdAndBusinessIdAsync(id, _currentTenantService.CurrentBusinessId);
        if (existing == null)
        {
            return ServiceResult.Fail("Revenue source not found.");
        }

        // If deactivating, check if this source has any Z-reports associated
        if (!isActive)
        {
            var hasSummaries = await _revenueSourceRepository.HasSummariesAsync(id, _currentTenantService.CurrentBusinessId);
            if (hasSummaries)
            {
                // Allow deactivation but warn — it's an advisory, not blocking
            }
        }

        await _revenueSourceRepository.SetIsActiveAsync(id, _currentTenantService.CurrentBusinessId, isActive);

        await _auditLogRepository.InsertAsync(new AuditLog
        {
            BusinessId = _currentTenantService.CurrentBusinessId,
            Action = isActive ? "Activate" : "Deactivate",
            TableName = "revenue.RevenueSource",
            RecordId = id.ToString(),
            Timestamp = DateTime.UtcNow
        });

        return ServiceResult.Ok();
    }
}
