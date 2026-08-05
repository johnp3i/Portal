using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for Supplier entity CRUD operations against the [purchase].[Supplier] table.
/// </summary>
public class SupplierRepository : GenericStoredProcedureRepository<Supplier>
{
    public SupplierRepository(DbContext context) : base(context) { }

    public async Task<List<Supplier>> GetAllByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [Name], [IsActive], [IsSystemGenerated], [CreatedAtUtc]
                FROM [purchase].[Supplier]
                WHERE Supplier.BusinessId = @BusinessId";

            return await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task<Supplier?> GetByIdAndBusinessIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [Name], [IsActive], [IsSystemGenerated], [CreatedAtUtc]
                FROM [purchase].[Supplier]
                WHERE Supplier.Id = @Id AND Supplier.BusinessId = @BusinessId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task<int> InsertAsync(Supplier entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [purchase].[Supplier]
                    ([BusinessId], [Name], [IsActive], [CreatedAtUtc])
                VALUES
                    (@BusinessId, @Name, @IsActive, @CreatedAtUtc);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var idParam = new SqlParameter("@BusinessId", entity.BusinessId);
            var nameParam = new SqlParameter("@Name", entity.Name ?? (object)DBNull.Value);
            var isActiveParam = new SqlParameter("@IsActive", entity.IsActive);
            var createdParam = new SqlParameter("@CreatedAtUtc", entity.CreatedAtUtc);

            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = query;
            command.Parameters.Add(idParam);
            command.Parameters.Add(nameParam);
            command.Parameters.Add(isActiveParam);
            command.Parameters.Add(createdParam);

            if (command.Connection!.State != System.Data.ConnectionState.Open)
                await command.Connection.OpenAsync();

            var result = await command.ExecuteScalarAsync();
            var insertedId = result != null ? Convert.ToInt32(result) : 0;
            entity.Id = insertedId;
            return insertedId;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task UpdateAsync(Supplier entity)
    {
        try
        {
            const string query = @"
                UPDATE [purchase].[Supplier]
                SET
                    [Name] = @Name
                WHERE Supplier.Id = @Id AND Supplier.BusinessId = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@Name", entity.Name ?? (object)DBNull.Value)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task DeactivateAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [purchase].[Supplier]
                SET
                    [IsActive] = 0
                WHERE Supplier.Id = @Id AND Supplier.BusinessId = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Checks if a supplier is system-generated (protected from deletion/deactivation).
    /// </summary>
    public async Task<bool> IsSystemGeneratedAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT COUNT(*)
                FROM [purchase].[Supplier]
                WHERE [purchase].[Supplier].[Id] = @Id
                  AND [purchase].[Supplier].[BusinessId] = @BusinessId
                  AND [purchase].[Supplier].[IsSystemGenerated] = 1";

            var result = await _context.Database
                .SqlQueryRaw<int>(query,
                    new SqlParameter("@Id", id),
                    new SqlParameter("@BusinessId", businessId))
                .ToListAsync();

            return result.FirstOrDefault() > 0;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<(List<Supplier> Items, int TotalCount)> GetPagedByBusinessIdAsync(
        int businessId,
        string? searchTerm,
        int offset,
        int pageSize)
    {
        try
        {
            const string query = @"
                SELECT [purchase].[Supplier].[Id],
                       [purchase].[Supplier].[BusinessId],
                       [purchase].[Supplier].[Name],
                       [purchase].[Supplier].[IsActive],
                       [purchase].[Supplier].[IsSystemGenerated],
                       [purchase].[Supplier].[CreatedAtUtc],
                       COUNT(*) OVER() AS [TotalCount]
                FROM [purchase].[Supplier]
                WHERE [purchase].[Supplier].[BusinessId] = @BusinessId
                  AND [purchase].[Supplier].[IsSystemGenerated] = 0
                  AND (@SearchTerm IS NULL OR [purchase].[Supplier].[Name] LIKE '%' + @SearchTerm + '%')
                ORDER BY [purchase].[Supplier].[Name] ASC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var results = new List<Supplier>();
            int totalCount = 0;
            var connection = _context.Database.GetDbConnection();

            // Escape SQL wildcards in search term
            string? escapedSearchTerm = searchTerm != null
                ? searchTerm.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]")
                : null;

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;
                command.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                command.Parameters.Add(new SqlParameter("@SearchTerm", (object?)escapedSearchTerm ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Offset", offset));
                command.Parameters.Add(new SqlParameter("@PageSize", pageSize));

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    totalCount = reader.GetInt32(reader.GetOrdinal("TotalCount"));
                    results.Add(new Supplier
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                        BusinessId = reader.GetInt32(reader.GetOrdinal("BusinessId")),
                        Name = reader.GetString(reader.GetOrdinal("Name")),
                        IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                        IsSystemGenerated = reader.GetBoolean(reader.GetOrdinal("IsSystemGenerated")),
                        CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"))
                    });
                }
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }

            return (results, totalCount);
        }
        catch (Exception)
        {
            throw;
        }
    }
}
