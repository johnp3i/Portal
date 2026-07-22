using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;

namespace Portal.Infrastructure.Services.Sales;

public interface ITeamMemberService
{
    Task<ServiceResult> CreateAsync(CreateTeamMemberRequest request);
    Task<ServiceResult> UpdateAsync(UpdateTeamMemberRequest request);
    Task<ServiceResult> DeactivateAsync(int id);
    Task<ServiceResult> ActivateAsync(int id);
    Task<List<TeamMemberDto>> GetActiveAsync();
    Task<List<TeamMemberDto>> GetAllAsync();
    Task<TeamMemberDto?> GetByIdAsync(int id);
}
