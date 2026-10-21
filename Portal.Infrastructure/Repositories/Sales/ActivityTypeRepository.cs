using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities.Sales;

namespace Portal.Infrastructure.Repositories.Sales;

/// <summary>
/// Repository for the [sales].[ActivityType] reference table. Global (not tenant-scoped) static
/// lookup; new types are added by inserting rows.
/// </summary>
public class ActivityTypeRepository : GenericStoredProcedureRepository<ActivityType>
{
    public ActivityTypeRepository(DbContext context) : base(context) { }

    /// <summary>All types ordered by SortOrder then Id. <paramref name="activeOnly"/> hides inactive
    /// rows (used by new-entry pickers; history/name resolution should pass false).</summary>
    public async Task<List<ActivityType>> GetAllAsync(bool activeOnly)
    {
        try
        {
            var where = activeOnly ? "WHERE [IsActive] = 1" : string.Empty;
            var query = $@"
                SELECT [Id], [Name], [SortOrder], [IsActive]
                FROM [sales].[ActivityType]
                {where}";

            // Order in LINQ so it lands on the outer query (FromSqlRaw is wrapped by EF).
            return await _context.Set<ActivityType>()
                .FromSqlRaw(query)
                .OrderBy(t => t.SortOrder)
                .ThenBy(t => t.Id)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
