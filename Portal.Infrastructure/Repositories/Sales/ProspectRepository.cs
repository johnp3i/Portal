using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities.Sales;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Repositories.Sales;

/// <summary>
/// Repository for [sales].[Prospect] — researched targets inside a campaign.
/// A Prospect is NOT a Sales Contact; it never touches the pipeline until Convert-to-Lead.
/// </summary>
public class ProspectRepository : GenericStoredProcedureRepository<Prospect>
{
    // Full entity column list — FromSqlRaw materializes every mapped property.
    private const string Columns =
        "[Id], [BusinessId], [ProspectCampaignId], [Name], [Segment], [Location], [BusinessType], " +
        "[PublicContactRole], [Phone], [Email], [Website], [PublicEvidence], [WhyFit], " +
        "[ResearchSourceUrl], [RecommendedFirstContact], [IcpFitScore], [PainProbabilityScore], " +
        "[AccessibilityScore], [LearningValueScore], [Status], [AssignedToUserId], [NextAction], " +
        "[NextActionDate], [ConvertedLeadRequestId], [ConvertedAtUtc], [ImportRowId], [CreatedAtUtc]";

    public ProspectRepository(DbContext context) : base(context) { }

    public async Task<int> InsertAsync(Prospect entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [sales].[Prospect]
                    ([BusinessId], [ProspectCampaignId], [Name], [Segment], [Location], [BusinessType],
                     [PublicContactRole], [Phone], [Email], [Website], [PublicEvidence], [WhyFit],
                     [ResearchSourceUrl], [RecommendedFirstContact], [IcpFitScore], [PainProbabilityScore],
                     [AccessibilityScore], [LearningValueScore], [Status], [AssignedToUserId], [NextAction],
                     [NextActionDate], [ImportRowId], [CreatedAtUtc])
                VALUES
                    (@BusinessId, @ProspectCampaignId, @Name, @Segment, @Location, @BusinessType,
                     @PublicContactRole, @Phone, @Email, @Website, @PublicEvidence, @WhyFit,
                     @ResearchSourceUrl, @RecommendedFirstContact, @IcpFitScore, @PainProbabilityScore,
                     @AccessibilityScore, @LearningValueScore, @Status, @AssignedToUserId, @NextAction,
                     @NextActionDate, @ImportRowId, @CreatedAtUtc);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var connection = _context.Database.GetDbConnection();
            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;
                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                AddInsertParams(command, entity);
                command.Parameters.Add(new SqlParameter("@CreatedAtUtc", DateTime.UtcNow));

                var result = await command.ExecuteScalarAsync();
                return (int)result!;
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private static void AddInsertParams(System.Data.Common.DbCommand command, Prospect e)
    {
        command.Parameters.Add(new SqlParameter("@BusinessId", e.BusinessId));
        command.Parameters.Add(new SqlParameter("@ProspectCampaignId", e.ProspectCampaignId));
        command.Parameters.Add(new SqlParameter("@Name", e.Name));
        command.Parameters.Add(new SqlParameter("@Segment", e.Segment ?? (object)DBNull.Value));
        command.Parameters.Add(new SqlParameter("@Location", e.Location ?? (object)DBNull.Value));
        command.Parameters.Add(new SqlParameter("@BusinessType", e.BusinessType ?? (object)DBNull.Value));
        command.Parameters.Add(new SqlParameter("@PublicContactRole", e.PublicContactRole ?? (object)DBNull.Value));
        command.Parameters.Add(new SqlParameter("@Phone", e.Phone ?? (object)DBNull.Value));
        command.Parameters.Add(new SqlParameter("@Email", e.Email ?? (object)DBNull.Value));
        command.Parameters.Add(new SqlParameter("@Website", e.Website ?? (object)DBNull.Value));
        command.Parameters.Add(new SqlParameter("@PublicEvidence", e.PublicEvidence ?? (object)DBNull.Value));
        command.Parameters.Add(new SqlParameter("@WhyFit", e.WhyFit ?? (object)DBNull.Value));
        command.Parameters.Add(new SqlParameter("@ResearchSourceUrl", e.ResearchSourceUrl ?? (object)DBNull.Value));
        command.Parameters.Add(new SqlParameter("@RecommendedFirstContact", e.RecommendedFirstContact ?? (object)DBNull.Value));
        command.Parameters.Add(new SqlParameter("@IcpFitScore", e.IcpFitScore));
        command.Parameters.Add(new SqlParameter("@PainProbabilityScore", e.PainProbabilityScore));
        command.Parameters.Add(new SqlParameter("@AccessibilityScore", e.AccessibilityScore));
        command.Parameters.Add(new SqlParameter("@LearningValueScore", e.LearningValueScore));
        command.Parameters.Add(new SqlParameter("@Status", e.Status));
        command.Parameters.Add(new SqlParameter("@AssignedToUserId", e.AssignedToUserId ?? (object)DBNull.Value));
        command.Parameters.Add(new SqlParameter("@NextAction", e.NextAction ?? (object)DBNull.Value));
        command.Parameters.Add(new SqlParameter("@NextActionDate", e.NextActionDate.HasValue ? e.NextActionDate.Value.ToDateTime(TimeOnly.MinValue) : (object)DBNull.Value));
        command.Parameters.Add(new SqlParameter("@ImportRowId", e.ImportRowId ?? (object)DBNull.Value));
    }

    public async Task UpdateAsync(Prospect entity)
    {
        try
        {
            const string query = @"
                UPDATE [sales].[Prospect]
                SET [Name] = @Name, [Segment] = @Segment, [Location] = @Location, [BusinessType] = @BusinessType,
                    [PublicContactRole] = @PublicContactRole, [Phone] = @Phone, [Email] = @Email, [Website] = @Website,
                    [PublicEvidence] = @PublicEvidence, [WhyFit] = @WhyFit, [ResearchSourceUrl] = @ResearchSourceUrl,
                    [RecommendedFirstContact] = @RecommendedFirstContact, [IcpFitScore] = @IcpFitScore,
                    [PainProbabilityScore] = @PainProbabilityScore, [AccessibilityScore] = @AccessibilityScore,
                    [LearningValueScore] = @LearningValueScore, [AssignedToUserId] = @AssignedToUserId,
                    [NextAction] = @NextAction, [NextActionDate] = @NextActionDate
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@Name", entity.Name),
                new SqlParameter("@Segment", entity.Segment ?? (object)DBNull.Value),
                new SqlParameter("@Location", entity.Location ?? (object)DBNull.Value),
                new SqlParameter("@BusinessType", entity.BusinessType ?? (object)DBNull.Value),
                new SqlParameter("@PublicContactRole", entity.PublicContactRole ?? (object)DBNull.Value),
                new SqlParameter("@Phone", entity.Phone ?? (object)DBNull.Value),
                new SqlParameter("@Email", entity.Email ?? (object)DBNull.Value),
                new SqlParameter("@Website", entity.Website ?? (object)DBNull.Value),
                new SqlParameter("@PublicEvidence", entity.PublicEvidence ?? (object)DBNull.Value),
                new SqlParameter("@WhyFit", entity.WhyFit ?? (object)DBNull.Value),
                new SqlParameter("@ResearchSourceUrl", entity.ResearchSourceUrl ?? (object)DBNull.Value),
                new SqlParameter("@RecommendedFirstContact", entity.RecommendedFirstContact ?? (object)DBNull.Value),
                new SqlParameter("@IcpFitScore", entity.IcpFitScore),
                new SqlParameter("@PainProbabilityScore", entity.PainProbabilityScore),
                new SqlParameter("@AccessibilityScore", entity.AccessibilityScore),
                new SqlParameter("@LearningValueScore", entity.LearningValueScore),
                new SqlParameter("@AssignedToUserId", entity.AssignedToUserId ?? (object)DBNull.Value),
                new SqlParameter("@NextAction", entity.NextAction ?? (object)DBNull.Value),
                new SqlParameter("@NextActionDate", entity.NextActionDate.HasValue ? entity.NextActionDate.Value.ToDateTime(TimeOnly.MinValue) : (object)DBNull.Value)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task UpdateStatusAsync(int id, int businessId, byte status)
    {
        try
        {
            const string query = @"
                UPDATE [sales].[Prospect] SET [Status] = @Status
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@Status", status));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>Marks a prospect converted (Status=5), records the created lead and the timestamp.</summary>
    public async Task SetConvertedAsync(int id, int businessId, int leadRequestId)
    {
        try
        {
            const string query = @"
                UPDATE [sales].[Prospect]
                SET [Status] = 5,
                    [ConvertedLeadRequestId] = @LeadRequestId,
                    [ConvertedAtUtc] = @ConvertedAtUtc
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@LeadRequestId", leadRequestId),
                new SqlParameter("@ConvertedAtUtc", DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<Prospect?> GetByIdAsync(int id, int businessId)
    {
        try
        {
            var query = $@"
                SELECT {Columns}
                FROM [sales].[Prospect]
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Import duplicate detection within a campaign: match on ImportRowId first, else Name +
    /// (Website or Email). Returns the existing prospect id if found, else null.
    /// </summary>
    public async Task<int?> FindForImportAsync(int campaignId, int businessId, string? importRowId, string name, string? website, string? email)
    {
        try
        {
            const string query = @"
                SELECT TOP 1 [Id]
                FROM [sales].[Prospect]
                WHERE [ProspectCampaignId] = @CampaignId AND [BusinessId] = @BusinessId
                  AND (
                        (@ImportRowId IS NOT NULL AND [ImportRowId] = @ImportRowId)
                     OR ([Name] = @Name AND (
                            (@Website IS NOT NULL AND [Website] = @Website)
                         OR (@Email IS NOT NULL AND [Email] = @Email)))
                  )";

            var connection = _context.Database.GetDbConnection();
            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;
                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    command.Transaction = transaction.GetDbTransaction();

                command.Parameters.Add(new SqlParameter("@CampaignId", campaignId));
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@ImportRowId", (object?)importRowId ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Name", name));
                command.Parameters.Add(new SqlParameter("@Website", (object?)website ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Email", (object?)email ?? DBNull.Value));

                var result = await command.ExecuteScalarAsync();
                return result != null && result != DBNull.Value ? (int)result : (int?)null;
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Paged prospect list for a campaign with optional filters. Priority filter is applied
    /// against the computed Total (sum of the four scores): A = 16-20, B = 12-15, Hold &lt; 12.
    /// nextAction: "due_today" | "overdue" | "this_week" | null.
    /// Returns entities plus the total count.
    /// </summary>
    public async Task<PagedResult<Prospect>> GetPagedByCampaignAsync(
        int campaignId, int businessId, string? priority, byte? status, string? segment,
        string? nextAction, int page, int pageSize)
    {
        try
        {
            var where = "[BusinessId] = @BusinessId AND [ProspectCampaignId] = @CampaignId";
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@CampaignId", campaignId)
            };

            var totalExpr = "([IcpFitScore] + [PainProbabilityScore] + [AccessibilityScore] + [LearningValueScore])";
            if (!string.IsNullOrWhiteSpace(priority))
            {
                switch (priority.Trim().ToUpperInvariant())
                {
                    case "A": where += $" AND {totalExpr} >= 16"; break;
                    case "B": where += $" AND {totalExpr} BETWEEN 12 AND 15"; break;
                    case "HOLD": where += $" AND {totalExpr} < 12"; break;
                }
            }
            if (status.HasValue)
            {
                where += " AND [Status] = @Status";
                parameters.Add(new SqlParameter("@Status", status.Value));
            }
            if (!string.IsNullOrWhiteSpace(segment))
            {
                where += " AND [Segment] = @Segment";
                parameters.Add(new SqlParameter("@Segment", segment));
            }
            if (!string.IsNullOrWhiteSpace(nextAction))
            {
                var today = DateTime.UtcNow.Date;
                switch (nextAction.Trim().ToLowerInvariant())
                {
                    case "due_today":
                        where += " AND [NextActionDate] = @Today";
                        parameters.Add(new SqlParameter("@Today", today));
                        break;
                    case "overdue":
                        where += " AND [NextActionDate] < @Today AND [Status] NOT IN (5,6)";
                        parameters.Add(new SqlParameter("@Today", today));
                        break;
                    case "this_week":
                        where += " AND [NextActionDate] BETWEEN @Today AND @WeekEnd";
                        parameters.Add(new SqlParameter("@Today", today));
                        parameters.Add(new SqlParameter("@WeekEnd", today.AddDays(7)));
                        break;
                }
            }

            var countQuery = $"SELECT COUNT(*) FROM [sales].[Prospect] WHERE {where}";
            var dataQuery = $@"
                SELECT {Columns}
                FROM [sales].[Prospect]
                WHERE {where}
                ORDER BY {totalExpr} DESC, [Name] ASC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var connection = _context.Database.GetDbConnection();
            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                var transaction = _context.Database.CurrentTransaction;

                int totalCount;
                using (var countCommand = connection.CreateCommand())
                {
                    countCommand.CommandText = countQuery;
                    if (transaction != null) countCommand.Transaction = transaction.GetDbTransaction();
                    foreach (var p in parameters)
                        countCommand.Parameters.Add(new SqlParameter(p.ParameterName, p.Value));
                    var countResult = await countCommand.ExecuteScalarAsync();
                    totalCount = countResult != null && countResult != DBNull.Value ? (int)countResult : 0;
                }

                if (totalCount == 0)
                    return new PagedResult<Prospect> { Items = new List<Prospect>(), CurrentPage = 1, PageSize = pageSize, TotalCount = 0 };

                int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
                if (page > totalPages) page = totalPages;
                if (page < 1) page = 1;
                int offset = (page - 1) * pageSize;

                var items = new List<Prospect>();
                using (var dataCommand = connection.CreateCommand())
                {
                    dataCommand.CommandText = dataQuery;
                    if (transaction != null) dataCommand.Transaction = transaction.GetDbTransaction();
                    foreach (var p in parameters)
                        dataCommand.Parameters.Add(new SqlParameter(p.ParameterName, p.Value));
                    dataCommand.Parameters.Add(new SqlParameter("@Offset", offset));
                    dataCommand.Parameters.Add(new SqlParameter("@PageSize", pageSize));

                    using var reader = await dataCommand.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                        items.Add(ReadProspect(reader));
                }

                return new PagedResult<Prospect> { Items = items, CurrentPage = page, PageSize = pageSize, TotalCount = totalCount };
            }
            finally
            {
                if (connection.State == ConnectionState.Open && _context.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private static Prospect ReadProspect(System.Data.Common.DbDataReader r)
    {
        string? S(string c) => r.IsDBNull(r.GetOrdinal(c)) ? null : r.GetString(r.GetOrdinal(c));
        DateOnly? D(string c) => r.IsDBNull(r.GetOrdinal(c)) ? null : DateOnly.FromDateTime(r.GetDateTime(r.GetOrdinal(c)));
        int? I(string c) => r.IsDBNull(r.GetOrdinal(c)) ? null : r.GetInt32(r.GetOrdinal(c));
        DateTime? DT(string c) => r.IsDBNull(r.GetOrdinal(c)) ? null : r.GetDateTime(r.GetOrdinal(c));

        return new Prospect
        {
            Id = r.GetInt32(r.GetOrdinal("Id")),
            BusinessId = r.GetInt32(r.GetOrdinal("BusinessId")),
            ProspectCampaignId = r.GetInt32(r.GetOrdinal("ProspectCampaignId")),
            Name = r.GetString(r.GetOrdinal("Name")),
            Segment = S("Segment"),
            Location = S("Location"),
            BusinessType = S("BusinessType"),
            PublicContactRole = S("PublicContactRole"),
            Phone = S("Phone"),
            Email = S("Email"),
            Website = S("Website"),
            PublicEvidence = S("PublicEvidence"),
            WhyFit = S("WhyFit"),
            ResearchSourceUrl = S("ResearchSourceUrl"),
            RecommendedFirstContact = S("RecommendedFirstContact"),
            IcpFitScore = r.GetByte(r.GetOrdinal("IcpFitScore")),
            PainProbabilityScore = r.GetByte(r.GetOrdinal("PainProbabilityScore")),
            AccessibilityScore = r.GetByte(r.GetOrdinal("AccessibilityScore")),
            LearningValueScore = r.GetByte(r.GetOrdinal("LearningValueScore")),
            Status = r.GetByte(r.GetOrdinal("Status")),
            AssignedToUserId = S("AssignedToUserId"),
            NextAction = S("NextAction"),
            NextActionDate = D("NextActionDate"),
            ConvertedLeadRequestId = I("ConvertedLeadRequestId"),
            ConvertedAtUtc = DT("ConvertedAtUtc"),
            ImportRowId = S("ImportRowId"),
            CreatedAtUtc = r.GetDateTime(r.GetOrdinal("CreatedAtUtc"))
        };
    }
}
