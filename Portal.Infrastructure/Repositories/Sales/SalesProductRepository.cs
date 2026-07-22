using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities.Sales;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Repositories.Sales;

/// <summary>
/// Repository for [sales].[Product] entity CRUD operations.
/// </summary>
public class SalesProductRepository : GenericStoredProcedureRepository<SalesProduct>
{
    public SalesProductRepository(DbContext context) : base(context) { }

    public async Task<int> InsertAsync(SalesProduct entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [sales].[Product]
                    ([BusinessId], [Name], [Description], [IsActive], [CreatedAtUtc])
                VALUES
                    (@BusinessId, @Name, @Description, @IsActive, @CreatedAtUtc);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = query;

            var transaction = _context.Database.CurrentTransaction;
            if (transaction != null)
                command.Transaction = transaction.GetDbTransaction();

            command.Parameters.Add(new SqlParameter("@BusinessId", entity.BusinessId));
            command.Parameters.Add(new SqlParameter("@Name", entity.Name));
            command.Parameters.Add(new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value));
            command.Parameters.Add(new SqlParameter("@IsActive", true));
            command.Parameters.Add(new SqlParameter("@CreatedAtUtc", DateTime.UtcNow));

            var result = await command.ExecuteScalarAsync();
            return (int)result!;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task UpdateAsync(SalesProduct entity)
    {
        try
        {
            const string query = @"
                UPDATE [sales].[Product]
                SET [Name] = @Name,
                    [Description] = @Description
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@Name", entity.Name),
                new SqlParameter("@Description", entity.Description ?? (object)DBNull.Value)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task DeactivateAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [sales].[Product]
                SET [IsActive] = 0
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId)
            );
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<SalesProduct?> GetByIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [Name], [Description], [IsActive], [CreatedAtUtc]
                FROM [sales].[Product]
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

    public async Task<List<SalesProduct>> GetAllActiveAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [Name], [Description], [IsActive], [CreatedAtUtc]
                FROM [sales].[Product]
                WHERE [BusinessId] = @BusinessId AND [IsActive] = 1";

            var results = await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
            return results.OrderBy(x => x.Name).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<PagedResult<SalesProduct>> GetPagedAsync(string? searchTerm, int page, int pageSize, int businessId)
    {
        try
        {
            const string countQuery = @"
                SELECT COUNT(*)
                FROM [sales].[Product]
                WHERE [sales].[Product].[BusinessId] = @BusinessId
                  AND (@SearchTerm IS NULL OR [sales].[Product].[Name] LIKE @SearchPattern)";

            const string dataQuery = @"
                SELECT [Id], [BusinessId], [Name], [Description], [IsActive], [CreatedAtUtc]
                FROM [sales].[Product]
                WHERE [sales].[Product].[BusinessId] = @BusinessId
                  AND (@SearchTerm IS NULL OR [sales].[Product].[Name] LIKE @SearchPattern)
                ORDER BY [sales].[Product].[Name]
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            var searchTermParam = string.IsNullOrWhiteSpace(searchTerm) ? (object)DBNull.Value : searchTerm;
            var searchPatternParam = string.IsNullOrWhiteSpace(searchTerm) ? (object)DBNull.Value : $"%{searchTerm}%";

            int totalCount;
            using (var countCommand = connection.CreateCommand())
            {
                countCommand.CommandText = countQuery;
                var transaction = _context.Database.CurrentTransaction;
                if (transaction != null)
                    countCommand.Transaction = transaction.GetDbTransaction();

                countCommand.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                countCommand.Parameters.Add(new SqlParameter("@SearchTerm", searchTermParam));
                countCommand.Parameters.Add(new SqlParameter("@SearchPattern", searchPatternParam));

                var countResult = await countCommand.ExecuteScalarAsync();
                totalCount = countResult != null && countResult != DBNull.Value ? (int)countResult : 0;
            }

            if (totalCount == 0)
            {
                return new PagedResult<SalesProduct>
                {
                    Items = new List<SalesProduct>(),
                    CurrentPage = 1,
                    PageSize = pageSize,
                    TotalCount = 0
                };
            }

            int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            if (page > totalPages) page = totalPages;
            if (page < 1) page = 1;
            int offset = (page - 1) * pageSize;

            var results = await ExecuteStoredProcedure(dataQuery,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@SearchTerm", searchTermParam),
                new SqlParameter("@SearchPattern", searchPatternParam),
                new SqlParameter("@Offset", offset),
                new SqlParameter("@PageSize", pageSize));

            return new PagedResult<SalesProduct>
            {
                Items = results,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
