using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for PlatformConfig entity operations against the [dbo].[PlatformConfig] table.
/// Provides case-insensitive key lookup and upsert (insert or update) functionality.
/// </summary>
public class PlatformConfigRepository : GenericStoredProcedureRepository<PlatformConfig>
{
    public PlatformConfigRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Gets a platform configuration record by key using case-insensitive comparison.
    /// Returns null if the key does not exist.
    /// </summary>
    public virtual async Task<PlatformConfig?> GetByKeyAsync(string key)
    {
        try
        {
            const string query = @"
                SELECT [dbo].[PlatformConfig].[Key],
                       [dbo].[PlatformConfig].[Value],
                       [dbo].[PlatformConfig].[Description],
                       [dbo].[PlatformConfig].[LastModifiedAtUtc]
                FROM [dbo].[PlatformConfig]
                WHERE LOWER([dbo].[PlatformConfig].[Key]) = LOWER(@Key)";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Key", key ?? (object)DBNull.Value));
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Inserts or updates a platform configuration record.
    /// If the key exists (case-insensitive), updates the Value and sets LastModifiedAtUtc to GETUTCDATE().
    /// If the key does not exist, inserts a new record with LastModifiedAtUtc set to GETUTCDATE().
    /// </summary>
    public virtual async Task UpsertAsync(string key, string value)
    {
        try
        {
            const string query = @"
                MERGE [dbo].[PlatformConfig] AS Target
                USING (SELECT @Key AS [Key]) AS Source
                ON LOWER(Target.[Key]) = LOWER(Source.[Key])
                WHEN MATCHED THEN
                    UPDATE SET [Value] = @Value,
                               [LastModifiedAtUtc] = GETUTCDATE()
                WHEN NOT MATCHED THEN
                    INSERT ([Key], [Value], [LastModifiedAtUtc])
                    VALUES (@Key, @Value, GETUTCDATE());";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Key", key ?? (object)DBNull.Value),
                new SqlParameter("@Value", value ?? (object)DBNull.Value));
        }
        catch (Exception)
        {
            throw;
        }
    }
}
