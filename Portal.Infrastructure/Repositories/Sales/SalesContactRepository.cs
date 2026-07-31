using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities.Sales;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Repositories.Sales;

/// <summary>
/// Repository for [sales].[Contact] entity CRUD operations.
/// </summary>
public class SalesContactRepository : GenericStoredProcedureRepository<SalesContact>
{
    public SalesContactRepository(DbContext context) : base(context) { }

    public async Task<int> InsertAsync(SalesContact entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [sales].[Contact]
                    ([BusinessId], [FirstName], [LastName], [Email], [PhoneNumber],
                     [CompanyName], [JobTitle], [Country], [Notes], [IsActive], [CreatedAtUtc])
                VALUES
                    (@BusinessId, @FirstName, @LastName, @Email, @PhoneNumber,
                     @CompanyName, @JobTitle, @Country, @Notes, @IsActive, @CreatedAtUtc);
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

                command.Parameters.Add(new SqlParameter("@BusinessId", entity.BusinessId));
                command.Parameters.Add(new SqlParameter("@FirstName", entity.FirstName));
                command.Parameters.Add(new SqlParameter("@LastName", entity.LastName ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Email", entity.Email ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@PhoneNumber", entity.PhoneNumber ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@CompanyName", entity.CompanyName ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@JobTitle", entity.JobTitle ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Country", entity.Country ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@IsActive", entity.IsActive));
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

    public async Task UpdateAsync(SalesContact entity)
    {
        try
        {
            const string query = @"
                UPDATE [sales].[Contact]
                SET
                    [FirstName] = @FirstName,
                    [LastName] = @LastName,
                    [Email] = @Email,
                    [PhoneNumber] = @PhoneNumber,
                    [CompanyName] = @CompanyName,
                    [JobTitle] = @JobTitle,
                    [Country] = @Country,
                    [Notes] = @Notes
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@FirstName", entity.FirstName),
                new SqlParameter("@LastName", entity.LastName ?? (object)DBNull.Value),
                new SqlParameter("@Email", entity.Email ?? (object)DBNull.Value),
                new SqlParameter("@PhoneNumber", entity.PhoneNumber ?? (object)DBNull.Value),
                new SqlParameter("@CompanyName", entity.CompanyName ?? (object)DBNull.Value),
                new SqlParameter("@JobTitle", entity.JobTitle ?? (object)DBNull.Value),
                new SqlParameter("@Country", entity.Country ?? (object)DBNull.Value),
                new SqlParameter("@Notes", entity.Notes ?? (object)DBNull.Value)
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
                UPDATE [sales].[Contact]
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

    public async Task ActivateAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                UPDATE [sales].[Contact]
                SET [IsActive] = 1
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

    public async Task<SalesContact?> GetByIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [FirstName], [LastName], [Email], [PhoneNumber],
                       [CompanyName], [JobTitle], [Country], [Notes], [IsActive], [CreatedAtUtc]
                FROM [sales].[Contact]
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

    public async Task<SalesContact?> CheckDuplicateEmailAsync(string email, int businessId, int? excludeId = null)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [FirstName], [LastName], [Email], [PhoneNumber],
                       [CompanyName], [JobTitle], [Country], [Notes], [IsActive], [CreatedAtUtc]
                FROM [sales].[Contact]
                WHERE [BusinessId] = @BusinessId
                  AND [Email] = @Email
                  AND (@ExcludeId IS NULL OR [Id] <> @ExcludeId)";

            return await ExecuteSingleRecordStoredProcedureUnfiltered(query,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@Email", email),
                new SqlParameter("@ExcludeId", excludeId ?? (object)DBNull.Value));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<SalesContact?> CheckDuplicatePhoneAsync(string phoneNumber, int businessId, int? excludeId = null)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [FirstName], [LastName], [Email], [PhoneNumber],
                       [CompanyName], [JobTitle], [Country], [Notes], [IsActive], [CreatedAtUtc]
                FROM [sales].[Contact]
                WHERE [BusinessId] = @BusinessId
                  AND [PhoneNumber] = @PhoneNumber
                  AND (@ExcludeId IS NULL OR [Id] <> @ExcludeId)";

            return await ExecuteSingleRecordStoredProcedureUnfiltered(query,
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@PhoneNumber", phoneNumber),
                new SqlParameter("@ExcludeId", excludeId ?? (object)DBNull.Value));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<PagedResult<SalesContact>> GetPagedAsync(string? searchTerm, int page, int pageSize, int businessId)
    {
        try
        {
            const string countQuery = @"
                SELECT COUNT(*)
                FROM [sales].[Contact]
                WHERE [sales].[Contact].[BusinessId] = @BusinessId
                  AND (@SearchTerm IS NULL
                       OR [sales].[Contact].[FirstName] LIKE @SearchPattern
                       OR [sales].[Contact].[LastName] LIKE @SearchPattern
                       OR [sales].[Contact].[Email] LIKE @SearchPattern
                       OR [sales].[Contact].[CompanyName] LIKE @SearchPattern
                       OR [sales].[Contact].[PhoneNumber] LIKE @SearchPattern)";

            const string dataQuery = @"
                SELECT [Id], [BusinessId], [FirstName], [LastName], [Email], [PhoneNumber],
                       [CompanyName], [JobTitle], [Country], [Notes], [IsActive], [CreatedAtUtc]
                FROM [sales].[Contact]
                WHERE [sales].[Contact].[BusinessId] = @BusinessId
                  AND (@SearchTerm IS NULL
                       OR [sales].[Contact].[FirstName] LIKE @SearchPattern
                       OR [sales].[Contact].[LastName] LIKE @SearchPattern
                       OR [sales].[Contact].[Email] LIKE @SearchPattern
                       OR [sales].[Contact].[CompanyName] LIKE @SearchPattern
                       OR [sales].[Contact].[PhoneNumber] LIKE @SearchPattern)
                ORDER BY [sales].[Contact].[FirstName], [sales].[Contact].[LastName]
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var connection = _context.Database.GetDbConnection();

            try
            {
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
                    return new PagedResult<SalesContact>
                    {
                        Items = new List<SalesContact>(),
                        CurrentPage = 1,
                        PageSize = pageSize,
                        TotalCount = 0
                    };
                }

                int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
                if (page > totalPages) page = totalPages;
                if (page < 1) page = 1;
                int offset = (page - 1) * pageSize;

                var results = new List<SalesContact>();
                using (var dataCommand = connection.CreateCommand())
                {
                    dataCommand.CommandText = dataQuery;
                    var transaction = _context.Database.CurrentTransaction;
                    if (transaction != null)
                        dataCommand.Transaction = transaction.GetDbTransaction();

                    dataCommand.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                    dataCommand.Parameters.Add(new SqlParameter("@SearchTerm", searchTermParam));
                    dataCommand.Parameters.Add(new SqlParameter("@SearchPattern", searchPatternParam));
                    dataCommand.Parameters.Add(new SqlParameter("@Offset", offset));
                    dataCommand.Parameters.Add(new SqlParameter("@PageSize", pageSize));

                    using var reader = await dataCommand.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        results.Add(new SalesContact
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("Id")),
                            BusinessId = reader.GetInt32(reader.GetOrdinal("BusinessId")),
                            FirstName = reader.GetString(reader.GetOrdinal("FirstName")),
                            LastName = reader.IsDBNull(reader.GetOrdinal("LastName")) ? null : reader.GetString(reader.GetOrdinal("LastName")),
                            Email = reader.IsDBNull(reader.GetOrdinal("Email")) ? null : reader.GetString(reader.GetOrdinal("Email")),
                            PhoneNumber = reader.IsDBNull(reader.GetOrdinal("PhoneNumber")) ? null : reader.GetString(reader.GetOrdinal("PhoneNumber")),
                            CompanyName = reader.IsDBNull(reader.GetOrdinal("CompanyName")) ? null : reader.GetString(reader.GetOrdinal("CompanyName")),
                            JobTitle = reader.IsDBNull(reader.GetOrdinal("JobTitle")) ? null : reader.GetString(reader.GetOrdinal("JobTitle")),
                            Country = reader.IsDBNull(reader.GetOrdinal("Country")) ? null : reader.GetString(reader.GetOrdinal("Country")),
                            Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? null : reader.GetString(reader.GetOrdinal("Notes")),
                            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                            CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"))
                        });
                    }
                }

                return new PagedResult<SalesContact>
                {
                    Items = results,
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                };
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
}
