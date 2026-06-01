using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository interface for BusinessPlan entity operations against the [dbo].[BusinessPlan] table.
/// </summary>
public interface IBusinessPlanRepository
{
    Task<BusinessPlan?> GetActiveByBusinessIdAsync(int businessId);
}
