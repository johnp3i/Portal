using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for BusinessLogo entity CRUD operations against the [portal].[BusinessLogo] table.
/// </summary>
public class BusinessLogoRepository : GenericStoredProcedureRepository<BusinessLogo>
{
    public BusinessLogoRepository(DbContext context) : base(context) { }

    public async Task<List<BusinessLogo>> GetByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [DisplayName], [FileName], [ContentType],
                       [FileSizeBytes], [PublicUrl], [CreatedAtUtc], [IsPrimary]
                FROM [portal].[BusinessLogo]
                WHERE [BusinessId] = @BusinessId";

            return await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<BusinessLogo?> GetByIdAsync(int id)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [DisplayName], [FileName], [ContentType],
                       [FileSizeBytes], [PublicUrl], [CreatedAtUtc], [IsPrimary]
                FROM [portal].[BusinessLogo]
                WHERE [Id] = @Id";

            return await ExecuteSingleRecordStoredProcedure(query, new SqlParameter("@Id", id));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task InsertAsync(BusinessLogo entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [portal].[BusinessLogo]
                    ([BusinessId], [DisplayName], [FileName], [ContentType], [FileSizeBytes], [PublicUrl], [CreatedAtUtc], [IsPrimary])
                VALUES
                    (@BusinessId, @DisplayName, @FileName, @ContentType, @FileSizeBytes, @PublicUrl, @CreatedAtUtc, @IsPrimary)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@DisplayName", entity.DisplayName),
                new SqlParameter("@FileName", entity.FileName),
                new SqlParameter("@ContentType", entity.ContentType),
                new SqlParameter("@FileSizeBytes", entity.FileSizeBytes),
                new SqlParameter("@PublicUrl", entity.PublicUrl),
                new SqlParameter("@CreatedAtUtc", entity.CreatedAtUtc),
                new SqlParameter("@IsPrimary", entity.IsPrimary)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task DeleteAsync(int id)
    {
        try
        {
            const string query = @"
                DELETE FROM [portal].[BusinessLogo]
                WHERE [Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query, new SqlParameter("@Id", id));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<int> GetCountByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT COUNT(*)
                FROM [portal].[BusinessLogo]
                WHERE [BusinessId] = @BusinessId";

            var result = await _context.Database
                .SqlQueryRaw<int>(query, new SqlParameter("@BusinessId", businessId))
                .ToListAsync();

            return result.FirstOrDefault();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task SetPrimaryAsync(int logoId, int businessId)
    {
        try
        {
            // Clear existing primary
            const string clearQuery = @"
                UPDATE [portal].[BusinessLogo]
                SET [IsPrimary] = 0
                WHERE [BusinessId] = @BusinessId AND [IsPrimary] = 1";

            await _context.Database.ExecuteSqlRawAsync(clearQuery, new SqlParameter("@BusinessId", businessId));

            // Set new primary
            const string setQuery = @"
                UPDATE [portal].[BusinessLogo]
                SET [IsPrimary] = 1
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(setQuery,
                new SqlParameter("@Id", logoId),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }
}
