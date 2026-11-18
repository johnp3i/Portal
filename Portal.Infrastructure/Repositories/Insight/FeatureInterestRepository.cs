using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities.Insight;

namespace Portal.Infrastructure.Repositories.Insight;

/// <summary>
/// Repository for [insight].[FeatureInterest] — a user's registered interest in a "coming later"
/// feature (for example the Discounts and Price drift teasers). Every read and write is scoped to
/// the tenant AND the owner. Registering the same combination twice is a silent no-op.
/// </summary>
public class FeatureInterestRepository : GenericStoredProcedureRepository<FeatureInterest>
{
    /// <summary>
    /// Registered feature keys for a user. The column is aliased to [Value] because
    /// <c>SqlQueryRaw&lt;string&gt;</c> maps a scalar result by that name.
    /// </summary>
    internal const string GetFeatureKeysSql = @"
        SELECT [insight].[FeatureInterest].[FeatureKey] AS [Value]
        FROM [insight].[FeatureInterest]
        WHERE [insight].[FeatureInterest].[BusinessId] = @BusinessId
          AND [insight].[FeatureInterest].[UserId] = @UserId";

    /// <summary>
    /// Atomic idempotent insert. UPDLOCK, HOLDLOCK makes the existence check and insert one unit,
    /// so concurrent identical clicks cannot race into a unique-key violation. CreatedAtUtc is
    /// omitted so the column default (GETUTCDATE()) applies.
    /// </summary>
    internal const string InsertIfNotExistsSql = @"
        INSERT INTO [insight].[FeatureInterest] ([BusinessId], [UserId], [FeatureKey])
        SELECT @BusinessId, @UserId, @FeatureKey
        WHERE NOT EXISTS (
            SELECT 1
            FROM [insight].[FeatureInterest] WITH (UPDLOCK, HOLDLOCK)
            WHERE [insight].[FeatureInterest].[BusinessId] = @BusinessId
              AND [insight].[FeatureInterest].[UserId] = @UserId
              AND [insight].[FeatureInterest].[FeatureKey] = @FeatureKey)";

    public FeatureInterestRepository(DbContext context) : base(context) { }

    /// <summary>The feature keys the user has registered interest in (empty when none).</summary>
    public async Task<List<string>> GetFeatureKeysAsync(int businessId, string userId)
    {
        try
        {
            return await _context.Database.SqlQueryRaw<string>(GetFeatureKeysSql,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@UserId", userId)).ToListAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Registers interest if no row exists for (BusinessId, UserId, FeatureKey). An existing row is
    /// left unchanged and no error is raised.
    /// </summary>
    public async Task InsertIfNotExistsAsync(int businessId, string userId, string featureKey)
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync(InsertIfNotExistsSql,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@UserId", userId),
                new SqlParameter("@FeatureKey", featureKey));
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
