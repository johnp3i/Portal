using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities.Sales;

namespace Portal.Infrastructure.Repositories.Sales;

/// <summary>
/// Repository for [sales].[LeadStatusType] lookup table.
/// </summary>
public class LeadStatusTypeRepository : GenericStoredProcedureRepository<LeadStatusType>
{
    public LeadStatusTypeRepository(DbContext context) : base(context) { }

    public async Task<List<LeadStatusType>> GetAllAsync()
    {
        try
        {
            const string query = @"
                SELECT [Id], [Name], [DisplayOrder], [Colour], [IsTerminal]
                FROM [sales].[LeadStatusType]";

            var results = await ExecuteStoredProcedureUnfiltered(query);
            return results.OrderBy(x => x.DisplayOrder).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
