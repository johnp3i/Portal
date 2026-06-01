using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Interface for PlanFeature repository operations against the [dbo].[PlanFeature] table.
/// </summary>
public interface IPlanFeatureRepository
{
    Task<List<PlanFeature>> GetByPlanIdAsync(int planId);
}
