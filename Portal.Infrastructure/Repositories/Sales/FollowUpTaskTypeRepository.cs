using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities.Sales;

namespace Portal.Infrastructure.Repositories.Sales;

/// <summary>
/// Repository for the [sales].[FollowUpTaskTypes] lookup table.
/// Mirrors <see cref="MeetingTypeRepository"/>; ordered by Id to preserve the
/// seeded UI order (Call, Email, Follow-up, Meeting Prep, Other).
/// </summary>
public class FollowUpTaskTypeRepository : GenericStoredProcedureRepository<FollowUpTaskType>
{
    public FollowUpTaskTypeRepository(DbContext context) : base(context) { }

    public async Task<List<FollowUpTaskType>> GetAllAsync()
    {
        try
        {
            const string query = @"
                SELECT [Id], [Name]
                FROM [sales].[FollowUpTaskTypes]";

            var results = await ExecuteStoredProcedureUnfiltered(query);
            return results.OrderBy(x => x.Id).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
