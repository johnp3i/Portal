using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities.Sales;

namespace Portal.Infrastructure.Repositories.Sales;

/// <summary>
/// Repository for [sales].[MeetingType] lookup table.
/// </summary>
public class MeetingTypeRepository : GenericStoredProcedureRepository<MeetingType>
{
    public MeetingTypeRepository(DbContext context) : base(context) { }

    public async Task<List<MeetingType>> GetAllAsync()
    {
        try
        {
            const string query = @"
                SELECT [Id], [Name]
                FROM [sales].[MeetingType]";

            var results = await ExecuteStoredProcedureUnfiltered(query);
            return results.OrderBy(x => x.Name).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
