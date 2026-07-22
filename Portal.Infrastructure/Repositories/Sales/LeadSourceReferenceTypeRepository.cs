using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities.Sales;

namespace Portal.Infrastructure.Repositories.Sales;

/// <summary>
/// Repository for [sales].[LeadSourceReferenceType] lookup table.
/// </summary>
public class LeadSourceReferenceTypeRepository : GenericStoredProcedureRepository<LeadSourceReferenceType>
{
    public LeadSourceReferenceTypeRepository(DbContext context) : base(context) { }

    public async Task<List<LeadSourceReferenceType>> GetAllAsync()
    {
        try
        {
            const string query = @"
                SELECT [Id], [Name], [IsActive]
                FROM [sales].[LeadSourceReferenceType]
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
