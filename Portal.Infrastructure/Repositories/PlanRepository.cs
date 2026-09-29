using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for Plan entity operations against the [dbo].[Plan] table.
/// </summary>
public class PlanRepository : GenericStoredProcedureRepository<Plan>, IPlanRepository
{
    public PlanRepository(DbContext context) : base(context) { }

    /// <summary>
    /// The full column list every <see cref="Plan"/> read query must select. EF Core requires
    /// every mapped scalar property of the entity to be present in a raw-SQL result set, or it
    /// throws "The required column 'X' was not present in the results" at materialization time.
    /// Kept in one place so a newly added Plan column is added to all reads at once (a missing
    /// column here is exactly the regression that once broke Billing). Verified by
    /// PlanRepositorySqlColumnTests against the EF model.
    /// </summary>
    public const string SelectColumns = @"
                       [Plan].[Id], [Plan].[Name], [Plan].[Slug], [Plan].[MonthlyPriceEur],
                       [Plan].[AnnualPriceEur], [Plan].[MaxUsers], [Plan].[StorageLimitMb], [Plan].[IsActive],
                       [Plan].[DisplayOrder], [Plan].[Description],
                       [Plan].[StripeProductId], [Plan].[StripePriceId],
                       [Plan].[CreatedAtUtc], [Plan].[UpdatedAtUtc]";

    public async Task<Plan?> GetBySlugAsync(string slug)
    {
        try
        {
            const string query = @"
                SELECT " + SelectColumns + @"
                FROM [dbo].[Plan]
                WHERE [Plan].[Slug] = @Slug";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Slug", slug ?? (object)DBNull.Value));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<Plan?> GetByIdAsync(int id)
    {
        try
        {
            const string query = @"
                SELECT " + SelectColumns + @"
                FROM [dbo].[Plan]
                WHERE [Plan].[Id] = @Id";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Id", id));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<List<Plan>> GetAllActiveAsync()
    {
        try
        {
            const string query = @"
                SELECT " + SelectColumns + @"
                FROM [dbo].[Plan]
                WHERE [Plan].[IsActive] = 1
                ORDER BY [Plan].[DisplayOrder]";

            return await ExecuteStoredProcedure(query);
        }
        catch (Exception)
        {
            throw;
        }
    }
}
