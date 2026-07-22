using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Sales;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;
using Portal.Infrastructure.Repositories.Sales;

namespace Portal.Infrastructure.Services.Sales;

public class TeamMemberService : ITeamMemberService
{
    private readonly TeamMemberRepository _repository;
    private readonly ICurrentTenantService _tenantService;
    private readonly PortalDbContext _context;

    public TeamMemberService(TeamMemberRepository repository, ICurrentTenantService tenantService, PortalDbContext context)
    {
        _repository = repository;
        _tenantService = tenantService;
        _context = context;
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

            // Get active lead counts per team member (non-cancelled, active, non-terminal stages)
            // Terminal stages: Won=6, Lost=7, Inactive=8
            var terminalStageIds = new[] { 6, 7, 8 };
            var leadCounts = await _context.LeadRequests
                .Where(l => l.TeamMemberId != null && !l.IsCancelled && l.IsActive && !terminalStageIds.Contains(l.LeadStatusTypeId))
                .GroupBy(l => l.TeamMemberId)
                .Select(g => new { TeamMemberId = g.Key, Count = g.Count() })
                .ToListAsync();

            var countLookup = leadCounts.ToDictionary(x => x.TeamMemberId!.Value, x => x.Count);

            return members.Select(m =>
            {
                var dto = MapToDto(m);
                dto.ActiveLeadCount = countLookup.GetValueOrDefault(m.Id, 0);
                return dto;
            }).ToList();
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

    public async Task<int> GetUnassignedLeadCountAsync()
    {
        try
        {
            var terminalStageIds = new[] { 6, 7, 8 };
            return await _context.LeadRequests
                .Where(l => l.TeamMemberId == null && !l.IsCancelled && l.IsActive && !terminalStageIds.Contains(l.LeadStatusTypeId))
                .CountAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
