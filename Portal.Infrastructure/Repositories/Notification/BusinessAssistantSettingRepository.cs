using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities.Notification;

namespace Portal.Infrastructure.Repositories.Notification;

/// <summary>
/// Raw-SQL repository for [notification].[BusinessAssistantSetting].
/// Absence of a row means "use assistant defaults" — the caller applies the fallback.
/// </summary>
public class BusinessAssistantSettingRepository : GenericStoredProcedureRepository<BusinessAssistantSetting>
{
    public BusinessAssistantSettingRepository(DbContext context) : base(context) { }

    public async Task<BusinessAssistantSetting?> GetAsync(int businessId, int assistantTypeId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [AssistantTypeId], [IsEnabled], [IsBrandingFooterEnabled],
                       [WorkingHoursStart], [WorkingHoursEnd],
                       [SendDayOfWeek], [SendTimeLocal], [RecipientOverride], [IsRecipientOwnerIncluded], [IncludedFiguresCsv],
                       [VatNoticeLeadDays], [TaskMeetingLookAheadDays],
                       [CreatedAtUtc], [UpdatedAtUtc]
                FROM [notification].[BusinessAssistantSetting]
                WHERE [notification].[BusinessAssistantSetting].[BusinessId] = @BusinessId
                  AND [notification].[BusinessAssistantSetting].[AssistantTypeId] = @AssistantTypeId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@AssistantTypeId", assistantTypeId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<BusinessAssistantSetting>> GetAllForBusinessAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [AssistantTypeId], [IsEnabled], [IsBrandingFooterEnabled],
                       [WorkingHoursStart], [WorkingHoursEnd],
                       [SendDayOfWeek], [SendTimeLocal], [RecipientOverride], [IsRecipientOwnerIncluded], [IncludedFiguresCsv],
                       [VatNoticeLeadDays], [TaskMeetingLookAheadDays],
                       [CreatedAtUtc], [UpdatedAtUtc]
                FROM [notification].[BusinessAssistantSetting]
                WHERE [notification].[BusinessAssistantSetting].[BusinessId] = @BusinessId";

            return await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>Insert or update the (business, assistant) setting row.</summary>
    public async Task UpsertAsync(BusinessAssistantSetting entity)
    {
        try
        {
            const string query = @"
                MERGE [notification].[BusinessAssistantSetting] AS target
                USING (SELECT @BusinessId AS BusinessId, @AssistantTypeId AS AssistantTypeId) AS source
                ON target.[BusinessId] = source.[BusinessId] AND target.[AssistantTypeId] = source.[AssistantTypeId]
                WHEN MATCHED THEN
                    UPDATE SET [IsEnabled] = @IsEnabled,
                               [IsBrandingFooterEnabled] = @IsBrandingFooterEnabled,
                               [WorkingHoursStart] = @WorkingHoursStart,
                               [WorkingHoursEnd] = @WorkingHoursEnd,
                               [SendDayOfWeek] = @SendDayOfWeek,
                               [SendTimeLocal] = @SendTimeLocal,
                               [RecipientOverride] = @RecipientOverride,
                               [IsRecipientOwnerIncluded] = @IsRecipientOwnerIncluded,
                               [IncludedFiguresCsv] = @IncludedFiguresCsv,
                               [VatNoticeLeadDays] = @VatNoticeLeadDays,
                               [TaskMeetingLookAheadDays] = @TaskMeetingLookAheadDays,
                               [UpdatedAtUtc] = GETUTCDATE()
                WHEN NOT MATCHED THEN
                    INSERT ([BusinessId], [AssistantTypeId], [IsEnabled], [IsBrandingFooterEnabled],
                            [WorkingHoursStart], [WorkingHoursEnd],
                            [SendDayOfWeek], [SendTimeLocal], [RecipientOverride], [IsRecipientOwnerIncluded], [IncludedFiguresCsv],
                            [VatNoticeLeadDays], [TaskMeetingLookAheadDays],
                            [CreatedAtUtc])
                    VALUES (@BusinessId, @AssistantTypeId, @IsEnabled, @IsBrandingFooterEnabled,
                            @WorkingHoursStart, @WorkingHoursEnd,
                            @SendDayOfWeek, @SendTimeLocal, @RecipientOverride, @IsRecipientOwnerIncluded, @IncludedFiguresCsv,
                            @VatNoticeLeadDays, @TaskMeetingLookAheadDays,
                            GETUTCDATE());";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@AssistantTypeId", entity.AssistantTypeId),
                new SqlParameter("@IsEnabled", entity.IsEnabled),
                new SqlParameter("@IsBrandingFooterEnabled", entity.IsBrandingFooterEnabled),
                new SqlParameter("@WorkingHoursStart", (object?)entity.WorkingHoursStart ?? DBNull.Value),
                new SqlParameter("@WorkingHoursEnd", (object?)entity.WorkingHoursEnd ?? DBNull.Value),
                new SqlParameter("@SendDayOfWeek", (object?)entity.SendDayOfWeek ?? DBNull.Value),
                new SqlParameter("@SendTimeLocal", (object?)entity.SendTimeLocal ?? DBNull.Value),
                new SqlParameter("@RecipientOverride", (object?)entity.RecipientOverride ?? DBNull.Value),
                new SqlParameter("@IsRecipientOwnerIncluded", entity.IsRecipientOwnerIncluded),
                new SqlParameter("@IncludedFiguresCsv", (object?)entity.IncludedFiguresCsv ?? DBNull.Value),
                new SqlParameter("@VatNoticeLeadDays", (object?)entity.VatNoticeLeadDays ?? DBNull.Value),
                new SqlParameter("@TaskMeetingLookAheadDays", (object?)entity.TaskMeetingLookAheadDays ?? DBNull.Value));
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
