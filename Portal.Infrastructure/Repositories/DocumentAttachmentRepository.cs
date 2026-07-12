using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for DocumentAttachment entity CRUD operations against the [document].[DocumentAttachment] table.
/// </summary>
public class DocumentAttachmentRepository : GenericStoredProcedureRepository<DocumentAttachment>
{
    public DocumentAttachmentRepository(DbContext context) : base(context) { }

    /// <summary>
    /// Inserts a new attachment metadata record and returns the generated Id.
    /// </summary>
    public async Task<int> InsertAsync(DocumentAttachment entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [document].[DocumentAttachment]
                    ([BusinessId], [EntityType], [EntityId], [FileName], [OriginalFileName],
                     [ContentType], [StoragePath], [FileSizeBytes], [UploadedByUserId])
                VALUES
                    (@BusinessId, @EntityType, @EntityId, @FileName, @OriginalFileName,
                     @ContentType, @StoragePath, @FileSizeBytes, @UploadedByUserId);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var result = await _context.Database.SqlQueryRaw<int>(query,
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@EntityType", entity.EntityType),
                new SqlParameter("@EntityId", entity.EntityId),
                new SqlParameter("@FileName", entity.FileName),
                new SqlParameter("@OriginalFileName", entity.OriginalFileName),
                new SqlParameter("@ContentType", entity.ContentType),
                new SqlParameter("@StoragePath", entity.StoragePath),
                new SqlParameter("@FileSizeBytes", entity.FileSizeBytes),
                new SqlParameter("@UploadedByUserId", entity.UploadedByUserId)
            ).ToListAsync();

            return result.FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets a single non-deleted attachment by Id scoped to a business.
    /// </summary>
    public async Task<DocumentAttachment?> GetByIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [EntityType], [EntityId], [FileName], [OriginalFileName],
                       [ContentType], [StoragePath], [FileSizeBytes], [UploadedByUserId], [IsDeleted], [DeletedAtUtc], [CreatedAtUtc]
                FROM [document].[DocumentAttachment]
                WHERE DocumentAttachment.Id = @Id
                  AND DocumentAttachment.BusinessId = @BusinessId
                  AND DocumentAttachment.IsDeleted = 0";

            return await ExecuteSingleRecordStoredProcedureUnfiltered(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets all non-deleted attachments for a specific entity, ordered by CreatedAtUtc descending.
    /// </summary>
    public async Task<List<DocumentAttachment>> GetByEntityAsync(int businessId, string entityType, int entityId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [EntityType], [EntityId], [FileName], [OriginalFileName],
                       [ContentType], [StoragePath], [FileSizeBytes], [UploadedByUserId], [IsDeleted], [DeletedAtUtc], [CreatedAtUtc]
                FROM [document].[DocumentAttachment]
                WHERE DocumentAttachment.BusinessId = @BusinessId
                  AND DocumentAttachment.EntityType = @EntityType
                  AND DocumentAttachment.EntityId = @EntityId
                  AND DocumentAttachment.IsDeleted = 0
                ORDER BY DocumentAttachment.CreatedAtUtc DESC";

            return await ExecuteStoredProcedureUnfiltered(query,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@EntityType", entityType),
                new SqlParameter("@EntityId", entityId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets the count of non-deleted attachments for a specific entity.
    /// </summary>
    public async Task<int> GetCountAsync(int businessId, string entityType, int entityId)
    {
        try
        {
            const string query = @"
                SELECT COUNT(*)
                FROM [document].[DocumentAttachment]
                WHERE DocumentAttachment.BusinessId = @BusinessId
                  AND DocumentAttachment.EntityType = @EntityType
                  AND DocumentAttachment.EntityId = @EntityId
                  AND DocumentAttachment.IsDeleted = 0";

            var result = await _context.Database.SqlQueryRaw<int>(query,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@EntityType", entityType),
                new SqlParameter("@EntityId", entityId)
            ).ToListAsync();

            return result.FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets attachment counts for multiple entities in a single query (batch lookup for list views).
    /// Returns a dictionary of EntityId → Count.
    /// </summary>
    public async Task<Dictionary<int, int>> GetCountsForEntitiesAsync(int businessId, string entityType, int[] entityIds)
    {
        try
        {
            if (entityIds == null || entityIds.Length == 0)
                return new Dictionary<int, int>();

            // Build parameterized IN clause
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@EntityType", entityType)
            };

            var idParams = new List<string>();
            for (var i = 0; i < entityIds.Length; i++)
            {
                var paramName = $"@EntityId{i}";
                idParams.Add(paramName);
                parameters.Add(new SqlParameter(paramName, entityIds[i]));
            }

            var query = $@"
                SELECT DocumentAttachment.EntityId AS [Key], COUNT(*) AS [Value]
                FROM [document].[DocumentAttachment]
                WHERE DocumentAttachment.BusinessId = @BusinessId
                  AND DocumentAttachment.EntityType = @EntityType
                  AND DocumentAttachment.EntityId IN ({string.Join(", ", idParams)})
                  AND DocumentAttachment.IsDeleted = 0
                GROUP BY DocumentAttachment.EntityId";

            var results = await _context.Database.SqlQueryRaw<KeyValueResult>(query, parameters.ToArray()).ToListAsync();

            return results.ToDictionary(r => r.Key, r => r.Value);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Soft-deletes an attachment by setting IsDeleted = 1 and recording the deletion timestamp.
    /// </summary>
    public async Task SoftDeleteAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [document].[DocumentAttachment]
                SET [IsDeleted] = 1,
                    [DeletedAtUtc] = GETUTCDATE()
                WHERE DocumentAttachment.Id = @Id
                  AND DocumentAttachment.BusinessId = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets all non-deleted attachments for a business with optional filtering and paging.
    /// </summary>
    public async Task<List<DocumentAttachment>> GetAllPagedAsync(
        int businessId, string? entityType, string? contentTypeFilter,
        string? uploadedByUserId, DateTime? dateFrom, DateTime? dateTo,
        int page, int pageSize)
    {
        try
        {
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@Offset", (page - 1) * pageSize),
                new SqlParameter("@PageSize", pageSize)
            };

            var whereClause = "DocumentAttachment.BusinessId = @BusinessId AND DocumentAttachment.IsDeleted = 0";

            if (!string.IsNullOrEmpty(entityType))
            {
                whereClause += " AND DocumentAttachment.EntityType = @EntityType";
                parameters.Add(new SqlParameter("@EntityType", entityType));
            }

            if (!string.IsNullOrEmpty(contentTypeFilter))
            {
                whereClause += " AND DocumentAttachment.ContentType = @ContentType";
                parameters.Add(new SqlParameter("@ContentType", contentTypeFilter));
            }

            if (!string.IsNullOrEmpty(uploadedByUserId))
            {
                whereClause += " AND DocumentAttachment.UploadedByUserId = @UploadedByUserId";
                parameters.Add(new SqlParameter("@UploadedByUserId", uploadedByUserId));
            }

            if (dateFrom.HasValue)
            {
                whereClause += " AND DocumentAttachment.CreatedAtUtc >= @DateFrom";
                parameters.Add(new SqlParameter("@DateFrom", dateFrom.Value));
            }

            if (dateTo.HasValue)
            {
                whereClause += " AND DocumentAttachment.CreatedAtUtc < @DateTo";
                parameters.Add(new SqlParameter("@DateTo", dateTo.Value.AddDays(1)));
            }

            var query = $@"
                SELECT [Id], [BusinessId], [EntityType], [EntityId], [FileName], [OriginalFileName],
                       [ContentType], [StoragePath], [FileSizeBytes], [UploadedByUserId], [IsDeleted], [DeletedAtUtc], [CreatedAtUtc]
                FROM [document].[DocumentAttachment]
                WHERE {whereClause}
                ORDER BY DocumentAttachment.CreatedAtUtc DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            return await ExecuteStoredProcedureUnfiltered(query, parameters.ToArray());
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets total count of non-deleted attachments for a business with optional filtering (for pagination).
    /// </summary>
    public async Task<int> GetAllCountAsync(
        int businessId, string? entityType, string? contentTypeFilter,
        string? uploadedByUserId, DateTime? dateFrom, DateTime? dateTo)
    {
        try
        {
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@BusinessId", businessId)
            };

            var whereClause = "DocumentAttachment.BusinessId = @BusinessId AND DocumentAttachment.IsDeleted = 0";

            if (!string.IsNullOrEmpty(entityType))
            {
                whereClause += " AND DocumentAttachment.EntityType = @EntityType";
                parameters.Add(new SqlParameter("@EntityType", entityType));
            }

            if (!string.IsNullOrEmpty(contentTypeFilter))
            {
                whereClause += " AND DocumentAttachment.ContentType = @ContentType";
                parameters.Add(new SqlParameter("@ContentType", contentTypeFilter));
            }

            if (!string.IsNullOrEmpty(uploadedByUserId))
            {
                whereClause += " AND DocumentAttachment.UploadedByUserId = @UploadedByUserId";
                parameters.Add(new SqlParameter("@UploadedByUserId", uploadedByUserId));
            }

            if (dateFrom.HasValue)
            {
                whereClause += " AND DocumentAttachment.CreatedAtUtc >= @DateFrom";
                parameters.Add(new SqlParameter("@DateFrom", dateFrom.Value));
            }

            if (dateTo.HasValue)
            {
                whereClause += " AND DocumentAttachment.CreatedAtUtc < @DateTo";
                parameters.Add(new SqlParameter("@DateTo", dateTo.Value.AddDays(1)));
            }

            var query = $@"
                SELECT COUNT(*)
                FROM [document].[DocumentAttachment]
                WHERE {whereClause}";

            var result = await _context.Database.SqlQueryRaw<int>(query, parameters.ToArray()).ToListAsync();
            return result.FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets summary statistics for the attachments index KPI cards.
    /// </summary>
    public async Task<(int totalFiles, long totalSizeBytes, int entitiesWithFiles, int thisMonthCount)> GetSummaryAsync(int businessId)
    {
        try
        {
            var firstOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

            var countQuery = @"
                SELECT COUNT(*) FROM [document].[DocumentAttachment]
                WHERE DocumentAttachment.BusinessId = @BusinessId AND DocumentAttachment.IsDeleted = 0";
            var totalFiles = (await _context.Database.SqlQueryRaw<int>(countQuery,
                new SqlParameter("@BusinessId", businessId)).ToListAsync()).FirstOrDefault();

            var sizeQuery = @"
                SELECT ISNULL(SUM(DocumentAttachment.FileSizeBytes), 0) FROM [document].[DocumentAttachment]
                WHERE DocumentAttachment.BusinessId = @BusinessId AND DocumentAttachment.IsDeleted = 0";
            var totalSize = (await _context.Database.SqlQueryRaw<long>(sizeQuery,
                new SqlParameter("@BusinessId", businessId)).ToListAsync()).FirstOrDefault();

            var entitiesQuery = @"
                SELECT COUNT(DISTINCT CONCAT(DocumentAttachment.EntityType, '-', DocumentAttachment.EntityId))
                FROM [document].[DocumentAttachment]
                WHERE DocumentAttachment.BusinessId = @BusinessId AND DocumentAttachment.IsDeleted = 0";
            var entitiesWithFiles = (await _context.Database.SqlQueryRaw<int>(entitiesQuery,
                new SqlParameter("@BusinessId", businessId)).ToListAsync()).FirstOrDefault();

            var monthQuery = @"
                SELECT COUNT(*) FROM [document].[DocumentAttachment]
                WHERE DocumentAttachment.BusinessId = @BusinessId AND DocumentAttachment.IsDeleted = 0
                  AND DocumentAttachment.CreatedAtUtc >= @FirstOfMonth";
            var thisMonth = (await _context.Database.SqlQueryRaw<int>(monthQuery,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@FirstOfMonth", firstOfMonth)).ToListAsync()).FirstOrDefault();

            return (totalFiles, totalSize, entitiesWithFiles, thisMonth);
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}

/// <summary>
/// Helper class for mapping GROUP BY results from batch count query.
/// </summary>
public class KeyValueResult
{
    public int Key { get; set; }
    public int Value { get; set; }
}
