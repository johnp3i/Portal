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

    public async Task<Plan?> GetBySlugAsync(string slug)
    {
        try
        {
            const string query = @"
                SELECT [Plan].[Id], [Plan].[Name], [Plan].[Slug], [Plan].[MonthlyPriceEur],
                       [Plan].[AnnualPriceEur], [Plan].[MaxUsers], [Plan].[IsActive],
                       [Plan].[DisplayOrder], [Plan].[Description],
                       [Plan].[CreatedAtUtc], [Plan].[UpdatedAtUtc]
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
                SELECT [Plan].[Id], [Plan].[Name], [Plan].[Slug], [Plan].[MonthlyPriceEur],
                       [Plan].[AnnualPriceEur], [Plan].[MaxUsers], [Plan].[IsActive],
                       [Plan].[DisplayOrder], [Plan].[Description],
                       [Plan].[CreatedAtUtc], [Plan].[UpdatedAtUtc]
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
                SELECT [Plan].[Id], [Plan].[Name], [Plan].[Slug], [Plan].[MonthlyPriceEur],
                       [Plan].[AnnualPriceEur], [Plan].[MaxUsers], [Plan].[IsActive],
                       [Plan].[DisplayOrder], [Plan].[Description],
                       [Plan].[CreatedAtUtc], [Plan].[UpdatedAtUtc]
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
