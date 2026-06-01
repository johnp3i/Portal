using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository interface for Plan entity operations against the [dbo].[Plan] table.
/// </summary>
public interface IPlanRepository
{
    Task<Plan?> GetBySlugAsync(string slug);
    Task<Plan?> GetByIdAsync(int id);
    Task<List<Plan>> GetAllActiveAsync();
}
