using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for [dbo].[FeatureAnnouncements] and [dbo].[UserAnnouncementDismissals] operations.
/// </summary>
public class AnnouncementRepository : GenericStoredProcedureRepository<FeatureAnnouncement>
{
    public AnnouncementRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Returns all active, published, non-expired announcements.
    /// Plan tier filtering is done at the service layer.
    /// </summary>
    public async Task<List<FeatureAnnouncement>> GetVisibleAsync(DateTime utcNow)
    {
        try
        {
            const string query = @"
                SELECT [Id], [Title], [Summary], [DetailHtml], [ModuleKey],
                       [CtaLabel], [CtaUrl], [TargetPlanTier], [IsActive],
                       [PublishedAtUtc], [ExpiresAtUtc], [CreatedAtUtc]
                FROM [dbo].[FeatureAnnouncements]
                WHERE [dbo].[FeatureAnnouncements].[IsActive] = 1
                  AND [dbo].[FeatureAnnouncements].[PublishedAtUtc] <= @UtcNow
                  AND ([dbo].[FeatureAnnouncements].[ExpiresAtUtc] IS NULL
                       OR [dbo].[FeatureAnnouncements].[ExpiresAtUtc] > @UtcNow)
                ORDER BY [dbo].[FeatureAnnouncements].[PublishedAtUtc] DESC";

            return await ExecuteStoredProcedureUnfiltered(query,
                new SqlParameter("@UtcNow", utcNow));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns all announcements for admin management (includes inactive/expired).
    /// </summary>
    public async Task<List<FeatureAnnouncement>> GetAllAsync()
    {
        try
        {
            const string query = @"
                SELECT [Id], [Title], [Summary], [DetailHtml], [ModuleKey],
                       [CtaLabel], [CtaUrl], [TargetPlanTier], [IsActive],
                       [PublishedAtUtc], [ExpiresAtUtc], [CreatedAtUtc]
                FROM [dbo].[FeatureAnnouncements]
                ORDER BY [dbo].[FeatureAnnouncements].[PublishedAtUtc] DESC";

            return await ExecuteStoredProcedureUnfiltered(query);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns a single announcement by Id.
    /// </summary>
    public async Task<FeatureAnnouncement?> GetByIdAsync(int id)
    {
        try
        {
            const string query = @"
                SELECT [Id], [Title], [Summary], [DetailHtml], [ModuleKey],
                       [CtaLabel], [CtaUrl], [TargetPlanTier], [IsActive],
                       [PublishedAtUtc], [ExpiresAtUtc], [CreatedAtUtc]
                FROM [dbo].[FeatureAnnouncements]
                WHERE [dbo].[FeatureAnnouncements].[Id] = @Id";

            return await ExecuteSingleRecordStoredProcedureUnfiltered(query,
                new SqlParameter("@Id", id));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Inserts a new announcement, returns generated Id.
    /// </summary>
    public async Task<int> InsertAsync(FeatureAnnouncement entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [dbo].[FeatureAnnouncements]
                    ([Title], [Summary], [DetailHtml], [ModuleKey], [CtaLabel], [CtaUrl],
                     [TargetPlanTier], [IsActive], [PublishedAtUtc], [ExpiresAtUtc], [CreatedAtUtc])
                VALUES
                    (@Title, @Summary, @DetailHtml, @ModuleKey, @CtaLabel, @CtaUrl,
                     @TargetPlanTier, @IsActive, @PublishedAtUtc, @ExpiresAtUtc, @CreatedAtUtc);
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

                command.Parameters.Add(new SqlParameter("@Title", entity.Title));
                command.Parameters.Add(new SqlParameter("@Summary", entity.Summary));
                command.Parameters.Add(new SqlParameter("@DetailHtml", entity.DetailHtml));
                command.Parameters.Add(new SqlParameter("@ModuleKey", entity.ModuleKey ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@CtaLabel", entity.CtaLabel ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@CtaUrl", entity.CtaUrl ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@TargetPlanTier", entity.TargetPlanTier ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@IsActive", entity.IsActive));
                command.Parameters.Add(new SqlParameter("@PublishedAtUtc", entity.PublishedAtUtc));
                command.Parameters.Add(new SqlParameter("@ExpiresAtUtc", entity.ExpiresAtUtc ?? (object)DBNull.Value));
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

    /// <summary>
    /// Updates an existing announcement.
    /// </summary>
    public async Task UpdateAsync(FeatureAnnouncement entity)
    {
        try
        {
            const string query = @"
                UPDATE [dbo].[FeatureAnnouncements]
                SET [Title] = @Title,
                    [Summary] = @Summary,
                    [DetailHtml] = @DetailHtml,
                    [ModuleKey] = @ModuleKey,
                    [CtaLabel] = @CtaLabel,
                    [CtaUrl] = @CtaUrl,
                    [TargetPlanTier] = @TargetPlanTier,
                    [IsActive] = @IsActive,
                    [PublishedAtUtc] = @PublishedAtUtc,
                    [ExpiresAtUtc] = @ExpiresAtUtc
                WHERE [dbo].[FeatureAnnouncements].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@Title", entity.Title),
                new SqlParameter("@Summary", entity.Summary),
                new SqlParameter("@DetailHtml", entity.DetailHtml),
                new SqlParameter("@ModuleKey", entity.ModuleKey ?? (object)DBNull.Value),
                new SqlParameter("@CtaLabel", entity.CtaLabel ?? (object)DBNull.Value),
                new SqlParameter("@CtaUrl", entity.CtaUrl ?? (object)DBNull.Value),
                new SqlParameter("@TargetPlanTier", entity.TargetPlanTier ?? (object)DBNull.Value),
                new SqlParameter("@IsActive", entity.IsActive),
                new SqlParameter("@PublishedAtUtc", entity.PublishedAtUtc),
                new SqlParameter("@ExpiresAtUtc", entity.ExpiresAtUtc ?? (object)DBNull.Value)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Returns all dismissal records for a user.
    /// </summary>
    public async Task<List<UserAnnouncementDismissal>> GetDismissalsForUserAsync(string userId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [UserId], [FeatureAnnouncementId], [DismissedAtUtc], [CreatedAtUtc]
                FROM [dbo].[UserAnnouncementDismissals]
                WHERE [dbo].[UserAnnouncementDismissals].[UserId] = @UserId";

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

                command.Parameters.Add(new SqlParameter("@UserId", userId));

                var results = new List<UserAnnouncementDismissal>();
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new UserAnnouncementDismissal
                    {
                        Id = reader.GetInt32(0),
                        UserId = reader.GetString(1),
                        FeatureAnnouncementId = reader.GetInt32(2),
                        DismissedAtUtc = reader.GetDateTime(3),
                        CreatedAtUtc = reader.GetDateTime(4)
                    });
                }

                return results;
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
    /// Inserts a dismissal record (idempotent — skips if already exists).
    /// </summary>
    public async Task DismissAsync(string userId, int announcementId)
    {
        try
        {
            const string query = @"
                IF NOT EXISTS (
                    SELECT 1 FROM [dbo].[UserAnnouncementDismissals]
                    WHERE [UserId] = @UserId AND [FeatureAnnouncementId] = @AnnouncementId
                )
                BEGIN
                    INSERT INTO [dbo].[UserAnnouncementDismissals]
                        ([UserId], [FeatureAnnouncementId], [DismissedAtUtc], [CreatedAtUtc])
                    VALUES
                        (@UserId, @AnnouncementId, @DismissedAtUtc, @CreatedAtUtc)
                END";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@UserId", userId),
                new SqlParameter("@AnnouncementId", announcementId),
                new SqlParameter("@DismissedAtUtc", DateTime.UtcNow),
                new SqlParameter("@CreatedAtUtc", DateTime.UtcNow)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Bulk inserts dismissal records for multiple announcements (idempotent per each).
    /// </summary>
    public async Task DismissAllAsync(string userId, List<int> announcementIds)
    {
        try
        {
            foreach (var announcementId in announcementIds)
            {
                await DismissAsync(userId, announcementId);
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Toggles the IsActive flag for an announcement.
    /// </summary>
    public async Task ToggleActiveAsync(int id, bool isActive)
    {
        try
        {
            const string query = @"
                UPDATE [dbo].[FeatureAnnouncements]
                SET [IsActive] = @IsActive
                WHERE [dbo].[FeatureAnnouncements].[Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@IsActive", isActive)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
