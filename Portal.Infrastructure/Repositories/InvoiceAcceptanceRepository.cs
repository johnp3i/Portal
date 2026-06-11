using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for InvoiceAcceptance entity operations against the [invoice].[InvoiceAcceptance] table.
/// Exposes only INSERT and SELECT — no update or delete methods (enforces immutability).
/// </summary>
public class InvoiceAcceptanceRepository : GenericStoredProcedureRepository<InvoiceAcceptance>
{
    public InvoiceAcceptanceRepository(DbContext context) : base(context) { }

    public virtual async Task InsertAsync(InvoiceAcceptance entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [invoice].[InvoiceAcceptance]
                    ([InvoiceShareId], [AcceptedTerms], [AcceptedAtUtc], [IpAddress], [UserAgent])
                VALUES
                    (@InvoiceShareId, @AcceptedTerms, @AcceptedAtUtc, @IpAddress, @UserAgent)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@InvoiceShareId", entity.InvoiceShareId),
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

    public virtual async Task<InvoiceAcceptance?> GetByInvoiceShareIdAsync(int invoiceShareId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [InvoiceShareId], [AcceptedTerms], [AcceptedAtUtc],
                       [IpAddress], [UserAgent], [CreatedAtUtc]
                FROM [invoice].[InvoiceAcceptance]
                WHERE [InvoiceShareId] = @InvoiceShareId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@InvoiceShareId", invoiceShareId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns a set of InvoiceShareIds that have acceptance records, filtered to a given list of share IDs.
    /// Used for batch-loading acceptance status on invoice list pages.
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
                SELECT [InvoiceShareId]
                FROM [invoice].[InvoiceAcceptance]
                WHERE [InvoiceShareId] IN ({string.Join(", ", placeholders)})";

            var results = await _context.Set<InvoiceAcceptance>()
                .FromSqlRaw(query, parameters.ToArray())
                .Select(a => a.InvoiceShareId)
                .ToListAsync();

            return new HashSet<int>(results);
        }
        catch (Exception)
        {
            throw;
        }
    }
}
