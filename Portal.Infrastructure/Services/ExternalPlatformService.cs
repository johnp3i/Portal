using System.Text.RegularExpressions;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for External Platform management. Validates platform code format and uniqueness,
/// stores codes uppercased, and scopes all operations to the current tenant.
/// </summary>
public class ExternalPlatformService : IExternalPlatformService
{
    private static readonly Regex PlatformCodePattern = new("^[A-Za-z0-9]{1,10}$", RegexOptions.Compiled);

    private readonly ExternalPlatformRepository _repository;
    private readonly AuditLogRepository _auditLogRepository;
    private readonly ICurrentTenantService _currentTenantService;

    public ExternalPlatformService(
        ExternalPlatformRepository repository,
        AuditLogRepository auditLogRepository,
        ICurrentTenantService currentTenantService)
    {
        _repository = repository;
        _auditLogRepository = auditLogRepository;
        _currentTenantService = currentTenantService;
    }

    public async Task<List<ExternalPlatform>> GetAllAsync(bool includeInactive)
    {
        return await _repository.GetByBusinessIdAsync(_currentTenantService.CurrentBusinessId, includeInactive);
    }

    public async Task<List<ExternalPlatform>> GetActiveAsync()
    {
        return await _repository.GetByBusinessIdAsync(_currentTenantService.CurrentBusinessId, includeInactive: false);
    }

    public async Task<ExternalPlatform?> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAndBusinessIdAsync(id, _currentTenantService.CurrentBusinessId);
    }

    public async Task<ServiceResult> CreateAsync(string name, string platformCode, string? description)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        var validation = Validate(name, platformCode, out var normalizedCode);
        if (validation != null)
            return ServiceResult.Fail(validation);

        var existing = await _repository.GetByCodeAndBusinessIdAsync(businessId, normalizedCode);
        if (existing != null)
            return ServiceResult.Fail($"A platform with code '{normalizedCode}' already exists.");

        var entity = new ExternalPlatform
        {
            BusinessId = businessId,
            Name = name.Trim(),
            PlatformCode = normalizedCode,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var newId = await _repository.InsertAsync(entity);

        await _auditLogRepository.InsertAsync(new AuditLog
        {
            BusinessId = businessId,
            Action = "Create",
            TableName = "revenue.ExternalPlatform",
            RecordId = newId.ToString(),
            Timestamp = DateTime.UtcNow
        });

        return ServiceResult.Ok(newId);
    }

    public async Task<ServiceResult> UpdateAsync(int id, string name, string platformCode, string? description)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        var validation = Validate(name, platformCode, out var normalizedCode);
        if (validation != null)
            return ServiceResult.Fail(validation);

        var existing = await _repository.GetByIdAndBusinessIdAsync(id, businessId);
        if (existing == null)
            return ServiceResult.Fail("Platform not found.");

        // Uniqueness: another platform (different Id) must not already use the code
        var byCode = await _repository.GetByCodeAndBusinessIdAsync(businessId, normalizedCode);
        if (byCode != null && byCode.Id != id)
            return ServiceResult.Fail($"A platform with code '{normalizedCode}' already exists.");

        existing.Name = name.Trim();
        existing.PlatformCode = normalizedCode;
        existing.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        await _repository.UpdateAsync(existing);

        await _auditLogRepository.InsertAsync(new AuditLog
        {
            BusinessId = businessId,
            Action = "Update",
            TableName = "revenue.ExternalPlatform",
            RecordId = id.ToString(),
            Timestamp = DateTime.UtcNow
        });

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> SetActiveAsync(int id, bool isActive)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        var existing = await _repository.GetByIdAndBusinessIdAsync(id, businessId);
        if (existing == null)
            return ServiceResult.Fail("Platform not found.");

        await _repository.SetActiveAsync(id, businessId, isActive);

        await _auditLogRepository.InsertAsync(new AuditLog
        {
            BusinessId = businessId,
            Action = isActive ? "Activate" : "Deactivate",
            TableName = "revenue.ExternalPlatform",
            RecordId = id.ToString(),
            Timestamp = DateTime.UtcNow
        });

        return ServiceResult.Ok();
    }

    /// <summary>
    /// Validates name and platform code. Returns an error message when invalid, or null when valid.
    /// Outputs the normalized (uppercased, trimmed) platform code.
    /// </summary>
    private static string? Validate(string name, string platformCode, out string normalizedCode)
    {
        normalizedCode = (platformCode ?? string.Empty).Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(name))
            return "Platform name is required.";
        if (name.Trim().Length > 200)
            return "Platform name must not exceed 200 characters.";
        if (string.IsNullOrWhiteSpace(normalizedCode))
            return "Platform code is required.";
        if (!PlatformCodePattern.IsMatch(normalizedCode))
            return "Platform code must be 1–10 letters or numbers.";

        return null;
    }
}
