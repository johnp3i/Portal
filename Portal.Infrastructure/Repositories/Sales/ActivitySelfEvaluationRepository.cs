using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities.Sales;

namespace Portal.Infrastructure.Repositories.Sales;

/// <summary>
/// Repository for [sales].[ActivitySelfEvaluation] — a private, author-only Call Reflection attached
/// to a prospect activity. Every read and write is scoped to the tenant (BusinessId) AND the author
/// (EvaluatorUserId): a reflection is only ever returned to, or mutated by, the user who wrote it.
/// Recording a reflection never touches commercial data.
/// </summary>
public class ActivitySelfEvaluationRepository : GenericStoredProcedureRepository<ActivitySelfEvaluation>
{
    // Full entity column list — FromSqlRaw materializes every mapped property.
    private const string Columns =
        "[Id], [BusinessId], [ProspectActivityId], [EvaluatorUserId], " +
        "[OpeningCredibility], [ListeningDiscovery], [Adaptability], [ClarityRelevance], " +
        "[ConversationControl], [NextStepAgreement], [Observations], [Strengths], [NextImprovement], " +
        "[PreCallObjective], [PreCallFocus], [CreatedAtUtc], [UpdatedAtUtc]";

    public ActivitySelfEvaluationRepository(DbContext context) : base(context) { }

    /// <summary>
    /// The author's reflection for one activity, or null. Scoped to tenant + author so a user can
    /// never read another user's reflection by guessing an activity id.
    /// </summary>
    public async Task<ActivitySelfEvaluation?> GetByActivityAsync(int prospectActivityId, int businessId, string evaluatorUserId)
    {
        try
        {
            var query = $@"
                SELECT {Columns}
                FROM [sales].[ActivitySelfEvaluation]
                WHERE [ProspectActivityId] = @ProspectActivityId
                  AND [BusinessId] = @BusinessId
                  AND [EvaluatorUserId] = @EvaluatorUserId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@ProspectActivityId", prospectActivityId),
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@EvaluatorUserId", evaluatorUserId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// The author's reflections in a date range, newest first — source for the personal-development
    /// dashboard (Phase C). Always scoped to tenant + author.
    /// </summary>
    public async Task<List<ActivitySelfEvaluation>> GetForDashboardAsync(
        int businessId, string evaluatorUserId, DateTime? fromUtc, DateTime? toUtc)
    {
        try
        {
            var query = $@"
                SELECT {Columns}
                FROM [sales].[ActivitySelfEvaluation]
                WHERE [BusinessId] = @BusinessId
                  AND [EvaluatorUserId] = @EvaluatorUserId
                  AND (@FromUtc IS NULL OR [CreatedAtUtc] >= @FromUtc)
                  AND (@ToUtc IS NULL OR [CreatedAtUtc] < @ToUtc)";

            // Order in LINQ so it lands on the outer query (the global query filter wraps the raw
            // SQL as a derived table, and SQL Server rejects ORDER BY inside one without TOP/OFFSET).
            return await _context.Set<ActivitySelfEvaluation>()
                .FromSqlRaw(query,
                    new SqlParameter("@BusinessId", businessId),
                    new SqlParameter("@EvaluatorUserId", evaluatorUserId),
                    new SqlParameter("@FromUtc", fromUtc ?? (object)DBNull.Value),
                    new SqlParameter("@ToUtc", toUtc ?? (object)DBNull.Value))
                .OrderByDescending(e => e.CreatedAtUtc)
                .ThenByDescending(e => e.Id)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Idempotent create-or-update of the author's reflection for an activity. Honours the unique
    /// (BusinessId, ProspectActivityId, EvaluatorUserId) key: a second save for the same activity by
    /// the same author updates the existing row rather than inserting a duplicate.
    /// </summary>
    public async Task UpsertAsync(ActivitySelfEvaluation entity)
    {
        try
        {
            var existing = await GetByActivityAsync(entity.ProspectActivityId, entity.BusinessId, entity.EvaluatorUserId);
            if (existing == null)
            {
                await InsertAsync(entity);
            }
            else
            {
                entity.Id = existing.Id;
                await UpdateAsync(entity);
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private async Task InsertAsync(ActivitySelfEvaluation entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [sales].[ActivitySelfEvaluation]
                    ([BusinessId], [ProspectActivityId], [EvaluatorUserId],
                     [OpeningCredibility], [ListeningDiscovery], [Adaptability], [ClarityRelevance],
                     [ConversationControl], [NextStepAgreement],
                     [Observations], [Strengths], [NextImprovement],
                     [PreCallObjective], [PreCallFocus], [CreatedAtUtc])
                VALUES
                    (@BusinessId, @ProspectActivityId, @EvaluatorUserId,
                     @OpeningCredibility, @ListeningDiscovery, @Adaptability, @ClarityRelevance,
                     @ConversationControl, @NextStepAgreement,
                     @Observations, @Strengths, @NextImprovement,
                     @PreCallObjective, @PreCallFocus, @CreatedAtUtc)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@ProspectActivityId", entity.ProspectActivityId),
                new SqlParameter("@EvaluatorUserId", entity.EvaluatorUserId),
                RatingParam("@OpeningCredibility", entity.OpeningCredibility),
                RatingParam("@ListeningDiscovery", entity.ListeningDiscovery),
                RatingParam("@Adaptability", entity.Adaptability),
                RatingParam("@ClarityRelevance", entity.ClarityRelevance),
                RatingParam("@ConversationControl", entity.ConversationControl),
                RatingParam("@NextStepAgreement", entity.NextStepAgreement),
                new SqlParameter("@Observations", entity.Observations ?? (object)DBNull.Value),
                new SqlParameter("@Strengths", entity.Strengths ?? (object)DBNull.Value),
                new SqlParameter("@NextImprovement", entity.NextImprovement ?? (object)DBNull.Value),
                new SqlParameter("@PreCallObjective", entity.PreCallObjective ?? (object)DBNull.Value),
                new SqlParameter("@PreCallFocus", entity.PreCallFocus ?? (object)DBNull.Value),
                new SqlParameter("@CreatedAtUtc", DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private async Task UpdateAsync(ActivitySelfEvaluation entity)
    {
        try
        {
            const string query = @"
                UPDATE [sales].[ActivitySelfEvaluation]
                SET [OpeningCredibility] = @OpeningCredibility,
                    [ListeningDiscovery] = @ListeningDiscovery,
                    [Adaptability] = @Adaptability,
                    [ClarityRelevance] = @ClarityRelevance,
                    [ConversationControl] = @ConversationControl,
                    [NextStepAgreement] = @NextStepAgreement,
                    [Observations] = @Observations,
                    [Strengths] = @Strengths,
                    [NextImprovement] = @NextImprovement,
                    [PreCallObjective] = @PreCallObjective,
                    [PreCallFocus] = @PreCallFocus,
                    [UpdatedAtUtc] = @UpdatedAtUtc
                WHERE [Id] = @Id
                  AND [BusinessId] = @BusinessId
                  AND [EvaluatorUserId] = @EvaluatorUserId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@EvaluatorUserId", entity.EvaluatorUserId),
                RatingParam("@OpeningCredibility", entity.OpeningCredibility),
                RatingParam("@ListeningDiscovery", entity.ListeningDiscovery),
                RatingParam("@Adaptability", entity.Adaptability),
                RatingParam("@ClarityRelevance", entity.ClarityRelevance),
                RatingParam("@ConversationControl", entity.ConversationControl),
                RatingParam("@NextStepAgreement", entity.NextStepAgreement),
                new SqlParameter("@Observations", entity.Observations ?? (object)DBNull.Value),
                new SqlParameter("@Strengths", entity.Strengths ?? (object)DBNull.Value),
                new SqlParameter("@NextImprovement", entity.NextImprovement ?? (object)DBNull.Value),
                new SqlParameter("@PreCallObjective", entity.PreCallObjective ?? (object)DBNull.Value),
                new SqlParameter("@PreCallFocus", entity.PreCallFocus ?? (object)DBNull.Value),
                new SqlParameter("@UpdatedAtUtc", DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Deletes the author's reflection for an activity. Scoped to tenant + author so one user can
    /// never delete another's. Returns the number of rows removed (0 if none matched).
    /// </summary>
    public async Task<int> DeleteAsync(int prospectActivityId, int businessId, string evaluatorUserId)
    {
        try
        {
            const string query = @"
                DELETE FROM [sales].[ActivitySelfEvaluation]
                WHERE [ProspectActivityId] = @ProspectActivityId
                  AND [BusinessId] = @BusinessId
                  AND [EvaluatorUserId] = @EvaluatorUserId";

            return await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@ProspectActivityId", prospectActivityId),
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@EvaluatorUserId", evaluatorUserId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns the set of activity ids (from the given candidate list) that already have a reflection
    /// by this author — lets the prospect detail show the private "reflected" indicator without an
    /// N+1. Scoped to tenant + author.
    /// </summary>
    public async Task<HashSet<int>> GetEvaluatedActivityIdsAsync(IEnumerable<int> activityIds, int businessId, string evaluatorUserId)
    {
        try
        {
            var ids = activityIds?.Distinct().ToList() ?? new List<int>();
            if (ids.Count == 0) return new HashSet<int>();

            // Author-scoped; IDs are integers we control (no injection surface).
            var inList = string.Join(",", ids);
            var query = $@"
                SELECT {Columns}
                FROM [sales].[ActivitySelfEvaluation]
                WHERE [BusinessId] = @BusinessId
                  AND [EvaluatorUserId] = @EvaluatorUserId
                  AND [ProspectActivityId] IN ({inList})";

            var rows = await ExecuteStoredProcedure(query,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@EvaluatorUserId", evaluatorUserId));

            return rows.Select(r => r.ProspectActivityId).ToHashSet();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    // A rating is 1-5 or NULL ("not assessed"). Guards the DB CHECK constraint so an out-of-range
    // value is coerced to NULL rather than throwing at the database (service-layer validation is the
    // primary gate; this is defence in depth).
    private static SqlParameter RatingParam(string name, byte? value)
    {
        var valid = value.HasValue && value.Value >= 1 && value.Value <= 5;
        return new SqlParameter(name, SqlDbType.TinyInt)
        {
            Value = valid ? value!.Value : (object)DBNull.Value
        };
    }
}
