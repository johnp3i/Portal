using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for ProposalAcceptance entity operations against the [quotation].[ProposalAcceptance] table.
/// Exposes only INSERT and SELECT — no update or delete methods (enforces immutability).
/// </summary>
public class ProposalAcceptanceRepository : GenericStoredProcedureRepository<ProposalAcceptance>
{
    public ProposalAcceptanceRepository(DbContext context) : base(context) { }

    public virtual async Task InsertAsync(ProposalAcceptance entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [quotation].[ProposalAcceptance]
                    ([ProposalShareId], [AcceptedTerms], [AcceptedAtUtc], [IpAddress], [UserAgent])
                VALUES
                    (@ProposalShareId, @AcceptedTerms, @AcceptedAtUtc, @IpAddress, @UserAgent)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@ProposalShareId", entity.ProposalShareId),
                new SqlParameter("@AcceptedTerms", entity.AcceptedTerms ?? (object)DBNull.Value),
                new SqlParameter("@AcceptedAtUtc", entity.AcceptedAtUtc),
                new SqlParameter("@IpAddress", entity.IpAddress ?? (object)DBNull.Value),
                new SqlParameter("@UserAgent", entity.UserAgent ?? (object)DBNull.Value)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task<ProposalAcceptance?> GetByProposalShareIdAsync(int proposalShareId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [ProposalShareId], [AcceptedTerms], [AcceptedAtUtc],
                       [IpAddress], [UserAgent], [CreatedAtUtc]
                FROM [quotation].[ProposalAcceptance]
                WHERE [ProposalShareId] = @ProposalShareId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@ProposalShareId", proposalShareId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns a set of ProposalShareIds that have acceptance records, filtered to a given list of share IDs.
    /// Used for batch-loading acceptance status on quotation list pages.
    /// </summary>
    public virtual async Task<HashSet<int>> GetAcceptedShareIdsAsync(IEnumerable<int> shareIds)
    {
        try
        {
            var ids = shareIds.ToList();
            if (ids.Count == 0) return new HashSet<int>();

            // Build parameterised IN clause
            var parameters = new List<SqlParameter>();
            var placeholders = new List<string>();
            for (int i = 0; i < ids.Count; i++)
            {
                var paramName = $"@ShareId{i}";
                placeholders.Add(paramName);
                parameters.Add(new SqlParameter(paramName, ids[i]));
            }

            var query = $@"
                SELECT [Id], [ProposalShareId], [AcceptedTerms], [AcceptedAtUtc],
                       [IpAddress], [UserAgent], [CreatedAtUtc]
                FROM [quotation].[ProposalAcceptance]
                WHERE [ProposalShareId] IN ({string.Join(", ", placeholders)})";

            var results = await _context.Set<ProposalAcceptance>()
                .FromSqlRaw(query, parameters.ToArray())
                .Select(a => a.ProposalShareId)
                .ToListAsync();

            return new HashSet<int>(results);
        }
        catch (Exception)
        {
            throw;
        }
    }
}
