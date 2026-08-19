using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities.Sales;

namespace Portal.Infrastructure.Repositories.Sales;

/// <summary>
/// Repository for [sales].[LeadPriorityType] lookup table.
/// </summary>
public class LeadPriorityTypeRepository : GenericStoredProcedureRepository<LeadPriorityType>
{
    public LeadPriorityTypeRepository(DbContext context) : base(context) { }

    public async Task<List<LeadPriorityType>> GetAllAsync()
    {
        try
        {
            const string query = @"
                SELECT [Id], [Name], [DisplayOrder], [Colour], [CreatedAtUtc]
                FROM [sales].[LeadPriorityType]";

            var results = await ExecuteStoredProcedureUnfiltered(query);
            return results.OrderBy(x => x.DisplayOrder).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
