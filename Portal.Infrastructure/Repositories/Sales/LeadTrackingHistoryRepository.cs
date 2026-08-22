using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities.Sales;

namespace Portal.Infrastructure.Repositories.Sales;

/// <summary>
/// Repository for [sales].[LeadTrackingHistory] entity operations.
/// </summary>
public class LeadTrackingHistoryRepository : GenericStoredProcedureRepository<LeadTrackingHistory>
{
    public LeadTrackingHistoryRepository(DbContext context) : base(context) { }

    public async Task InsertAsync(LeadTrackingHistory entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [sales].[LeadTrackingHistory]
                    ([LeadRequestId], [BusinessId], [LeadTrackingActionTypeId],
                     [FromLeadStatusTypeId], [ToLeadStatusTypeId], [RelatedEntityId],
                     [CreatedByUserId], [CreatedAtUtc])
                VALUES
                    (@LeadRequestId, @BusinessId, @LeadTrackingActionTypeId,
                     @FromLeadStatusTypeId, @ToLeadStatusTypeId, @RelatedEntityId,
                     @CreatedByUserId, @CreatedAtUtc)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@LeadRequestId", entity.LeadRequestId),
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@LeadTrackingActionTypeId", entity.LeadTrackingActionTypeId),
                new SqlParameter("@FromLeadStatusTypeId", entity.FromLeadStatusTypeId ?? (object)DBNull.Value),
                new SqlParameter("@ToLeadStatusTypeId", entity.ToLeadStatusTypeId),
                new SqlParameter("@RelatedEntityId", entity.RelatedEntityId ?? (object)DBNull.Value),
                new SqlParameter("@CreatedByUserId", entity.CreatedByUserId ?? (object)DBNull.Value),
                new SqlParameter("@CreatedAtUtc", entity.CreatedAtUtc)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<LeadTrackingHistory>> GetByLeadRequestIdAsync(int leadRequestId, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [sales].[LeadTrackingHistory].[Id],
                       [sales].[LeadTrackingHistory].[LeadRequestId],
                       [sales].[LeadTrackingHistory].[BusinessId],
                       [sales].[LeadTrackingHistory].[LeadTrackingActionTypeId],
                       [sales].[LeadTrackingHistory].[FromLeadStatusTypeId],
                       [sales].[LeadTrackingHistory].[ToLeadStatusTypeId],
                       [sales].[LeadTrackingHistory].[RelatedEntityId],
                       [sales].[LeadTrackingHistory].[CreatedByUserId],
                       [sales].[LeadTrackingHistory].[CreatedAtUtc]
                FROM [sales].[LeadTrackingHistory]
                WHERE [sales].[LeadTrackingHistory].[LeadRequestId] = @LeadRequestId
                  AND [sales].[LeadTrackingHistory].[BusinessId] = @BusinessId
                ORDER BY [sales].[LeadTrackingHistory].[CreatedAtUtc] DESC";

            var results = await ExecuteStoredProcedureUnfiltered(query,
                new SqlParameter("@LeadRequestId", leadRequestId),
                new SqlParameter("@BusinessId", businessId));
            return results.ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
