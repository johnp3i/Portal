using Portal.Infrastructure.Entities.Sales;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;
using Portal.Infrastructure.Repositories.Sales;

namespace Portal.Infrastructure.Services.Sales;

public class TeamMemberService : ITeamMemberService
{
    private readonly TeamMemberRepository _repository;
    private readonly ICurrentTenantService _tenantService;

    public TeamMemberService(TeamMemberRepository repository, ICurrentTenantService tenantService)
    {
        _repository = repository;
        _tenantService = tenantService;
    }

    public async Task<ServiceResult> CreateAsync(CreateTeamMemberRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            if (string.IsNullOrWhiteSpace(request.FirstName))
                return ServiceResult.Fail("First name is required.");

            // Check email uniqueness
            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var existing = await _repository.CheckDuplicateEmailAsync(request.Email, businessId);
                if (existing != null)
                    return ServiceResult.Fail($"A team member with this email already exists: {existing.FirstName} {existing.LastName}");
            }

            var entity = new TeamMember
            {
                BusinessId = businessId,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                Role = request.Role,
                UserId = request.UserId,
                IsActive = true
            };

            var id = await _repository.InsertAsync(entity);
            return ServiceResult.Ok(id);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> UpdateAsync(UpdateTeamMemberRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            var existing = await _repository.GetByIdAsync(request.Id, businessId);
            if (existing == null)
                return ServiceResult.Fail("Team member not found.");

            if (string.IsNullOrWhiteSpace(request.FirstName))
                return ServiceResult.Fail("First name is required.");

            // Check email uniqueness (excluding current)
            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var dup = await _repository.CheckDuplicateEmailAsync(request.Email, businessId, request.Id);
                if (dup != null)
                    return ServiceResult.Fail($"A team member with this email already exists: {dup.FirstName} {dup.LastName}");
            }

            existing.FirstName = request.FirstName;
            existing.LastName = request.LastName;
            existing.Email = request.Email;
            existing.PhoneNumber = request.PhoneNumber;
            existing.Role = request.Role;
            existing.UserId = request.UserId;

            await _repository.UpdateAsync(existing);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> DeactivateAsync(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            await _repository.DeactivateAsync(id, businessId);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> ActivateAsync(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            await _repository.ActivateAsync(id, businessId);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<TeamMemberDto>> GetActiveAsync()
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var members = await _repository.GetActiveByBusinessIdAsync(businessId);
            return members.Select(MapToDto).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<TeamMemberDto>> GetAllAsync()
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var members = await _repository.GetAllByBusinessIdAsync(businessId);
            return members.Select(MapToDto).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<TeamMemberDto?> GetByIdAsync(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var member = await _repository.GetByIdAsync(id, businessId);
            return member != null ? MapToDto(member) : null;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private static TeamMemberDto MapToDto(TeamMember m)
    {
        var displayName = string.IsNullOrWhiteSpace(m.LastName)
            ? m.FirstName
            : $"{m.FirstName} {m.LastName}";

        return new TeamMemberDto
        {
            Id = m.Id,
            FirstName = m.FirstName,
            LastName = m.LastName,
            DisplayName = displayName,
            Email = m.Email,
            PhoneNumber = m.PhoneNumber,
            Role = m.Role,
            UserId = m.UserId,
            IsLinkedToPortalUser = !string.IsNullOrWhiteSpace(m.UserId),
            IsActive = m.IsActive,
            CreatedAtUtc = m.CreatedAtUtc
        };
    }
}
