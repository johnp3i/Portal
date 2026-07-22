using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities.Sales;

namespace Portal.Infrastructure.Repositories.Sales;

/// <summary>
/// Repository for [sales].[LeadSourceType] lookup table.
/// </summary>
public class LeadSourceTypeRepository : GenericStoredProcedureRepository<LeadSourceType>
{
    public LeadSourceTypeRepository(DbContext context) : base(context) { }

    public async Task<List<LeadSourceType>> GetAllAsync()
    {
        try
        {
            const string query = @"
                SELECT [Id], [Name], [IsActive]
                FROM [sales].[LeadSourceType]
                WHERE [IsActive] = 1";

            var results = await ExecuteStoredProcedureUnfiltered(query);
            return results.OrderBy(x => x.Name).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
