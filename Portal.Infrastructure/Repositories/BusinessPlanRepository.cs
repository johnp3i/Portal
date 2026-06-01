using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for BusinessPlan entity operations against the [dbo].[BusinessPlan] table.
/// </summary>
public class BusinessPlanRepository : GenericStoredProcedureRepository<BusinessPlan>, IBusinessPlanRepository
{
    public BusinessPlanRepository(DbContext context) : base(context) { }

    public async Task<BusinessPlan?> GetActiveByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [BusinessPlan].[Id], [BusinessPlan].[BusinessId], [BusinessPlan].[PlanId],
                       [BusinessPlan].[StartDateUtc], [BusinessPlan].[EndDateUtc],
                       [BusinessPlan].[IsActive], [BusinessPlan].[CreatedAtUtc]
                FROM [dbo].[BusinessPlan]
                WHERE [BusinessPlan].[BusinessId] = @BusinessId
                  AND [BusinessPlan].[IsActive] = 1";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }
}
