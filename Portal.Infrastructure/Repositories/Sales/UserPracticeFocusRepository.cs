using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities.Sales;

namespace Portal.Infrastructure.Repositories.Sales;

/// <summary>
/// Repository for [sales].[UserPracticeFocus] — a user's single standing "practice focus" on the
/// private My Development dashboard. One row per user per business; every read and write is scoped
/// to the tenant AND the owner.
/// </summary>
public class UserPracticeFocusRepository : GenericStoredProcedureRepository<UserPracticeFocus>
{
    private const string Columns =
        "[Id], [BusinessId], [UserId], [Focus], [CreatedAtUtc], [UpdatedAtUtc]";

    public UserPracticeFocusRepository(DbContext context) : base(context) { }

    /// <summary>The user's current focus, or null if none set.</summary>
    public async Task<UserPracticeFocus?> GetAsync(int businessId, string userId)
    {
        try
        {
            var query = $@"
                SELECT {Columns}
                FROM [sales].[UserPracticeFocus]
                WHERE [BusinessId] = @BusinessId AND [UserId] = @UserId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@UserId", userId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Idempotent set of the user's focus. Honours the unique (BusinessId, UserId) key: updates the
    /// existing row or inserts the first one. An empty/whitespace focus clears it (row removed).
    /// </summary>
    public async Task UpsertAsync(int businessId, string userId, string? focus)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(focus))
            {
                await ClearAsync(businessId, userId);
                return;
            }

            var trimmed = focus.Trim();
            if (trimmed.Length > 1000) trimmed = trimmed[..1000];

            var existing = await GetAsync(businessId, userId);
            if (existing == null)
            {
                const string insert = @"
                    INSERT INTO [sales].[UserPracticeFocus]
                        ([BusinessId], [UserId], [Focus], [CreatedAtUtc])
                    VALUES
                        (@BusinessId, @UserId, @Focus, @CreatedAtUtc)";

                await _context.Database.ExecuteSqlRawAsync(insert,
                    new SqlParameter("@BusinessId", businessId),
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@Focus", trimmed),
                    new SqlParameter("@CreatedAtUtc", DateTime.UtcNow));
            }
            else
            {
                const string update = @"
                    UPDATE [sales].[UserPracticeFocus]
                    SET [Focus] = @Focus, [UpdatedAtUtc] = @UpdatedAtUtc
                    WHERE [BusinessId] = @BusinessId AND [UserId] = @UserId";

                await _context.Database.ExecuteSqlRawAsync(update,
                    new SqlParameter("@BusinessId", businessId),
                    new SqlParameter("@UserId", userId),
                    new SqlParameter("@Focus", trimmed),
                    new SqlParameter("@UpdatedAtUtc", DateTime.UtcNow));
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private async Task ClearAsync(int businessId, string userId)
    {
        try
        {
            const string query = @"
                DELETE FROM [sales].[UserPracticeFocus]
                WHERE [BusinessId] = @BusinessId AND [UserId] = @UserId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@UserId", userId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
