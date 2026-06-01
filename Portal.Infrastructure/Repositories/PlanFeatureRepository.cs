using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for PlanFeature entity operations against the [dbo].[PlanFeature] table.
/// </summary>
public class PlanFeatureRepository : GenericStoredProcedureRepository<PlanFeature>, IPlanFeatureRepository
{
    public PlanFeatureRepository(DbContext context) : base(context) { }

    public async Task<List<PlanFeature>> GetByPlanIdAsync(int planId)
    {
        try
        {
            const string query = @"
                SELECT [PlanFeature].[Id], [PlanFeature].[PlanId], [PlanFeature].[ModuleName],
                       [PlanFeature].[IsIncluded], [PlanFeature].[CreatedAtUtc]
                FROM [dbo].[PlanFeature]
                WHERE [PlanFeature].[PlanId] = @PlanId";

            return await ExecuteStoredProcedure(query,
                new SqlParameter("@PlanId", planId));
        }
        catch (Exception)
        {
            throw;
        }
    }
}
