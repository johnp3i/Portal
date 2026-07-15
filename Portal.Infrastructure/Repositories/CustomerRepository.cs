using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for Customer entity CRUD operations against the [customer].[Customer] table.
/// </summary>
public class CustomerRepository : GenericStoredProcedureRepository<Customer>
{
    public CustomerRepository(DbContext context) : base(context) { }

    public async Task<List<Customer>> GetAllByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [Name], [ContactPerson], [Email], [TelephoneNumber], [MobileNumber],
                       [AddressLine1], [AddressLine2], [City], [PostalCode], [Country],
                       [IsActive], [IsReminderOptedOut], [CreatedAtUtc], [UpdatedAtUtc]
                FROM [customer].[Customer]
                WHERE [BusinessId] = @BusinessId";

            var results = await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
            return results.OrderBy(c => c.Name).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual async Task<Customer?> GetByIdAndBusinessIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [Name], [ContactPerson], [Email], [TelephoneNumber], [MobileNumber],
                       [AddressLine1], [AddressLine2], [City], [PostalCode], [Country],
                       [IsActive], [IsReminderOptedOut], [CreatedAtUtc], [UpdatedAtUtc]
                FROM [customer].[Customer]
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public virtual async Task<Customer?> GetByIdAndBusinessIdUnfilteredAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [Name], [ContactPerson], [Email], [TelephoneNumber], [MobileNumber],
                       [AddressLine1], [AddressLine2], [City], [PostalCode], [Country],
                       [IsActive], [IsReminderOptedOut], [CreatedAtUtc], [UpdatedAtUtc]
                FROM [customer].[Customer]
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            return await ExecuteSingleRecordStoredProcedureUnfiltered(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<int> InsertAsync(Customer entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [customer].[Customer]
                    ([BusinessId], [Name], [ContactPerson], [Email], [TelephoneNumber], [MobileNumber],
                     [AddressLine1], [AddressLine2], [City], [PostalCode], [Country],
                     [IsActive], [CreatedAtUtc], [UpdatedAtUtc])
                VALUES
                    (@BusinessId, @Name, @ContactPerson, @Email, @TelephoneNumber, @MobileNumber,
                     @AddressLine1, @AddressLine2, @City, @PostalCode, @Country,
                     @IsActive, @CreatedAtUtc, @UpdatedAtUtc);
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
            command.Parameters.Add(new SqlParameter("@Name", entity.Name ?? (object)DBNull.Value));
            command.Parameters.Add(new SqlParameter("@ContactPerson", entity.ContactPerson ?? (object)DBNull.Value));
            command.Parameters.Add(new SqlParameter("@Email", entity.Email ?? (object)DBNull.Value));
            command.Parameters.Add(new SqlParameter("@TelephoneNumber", entity.TelephoneNumber ?? (object)DBNull.Value));
            command.Parameters.Add(new SqlParameter("@MobileNumber", entity.MobileNumber ?? (object)DBNull.Value));
            command.Parameters.Add(new SqlParameter("@AddressLine1", entity.AddressLine1 ?? (object)DBNull.Value));
            command.Parameters.Add(new SqlParameter("@AddressLine2", entity.AddressLine2 ?? (object)DBNull.Value));
            command.Parameters.Add(new SqlParameter("@City", entity.City ?? (object)DBNull.Value));
            command.Parameters.Add(new SqlParameter("@PostalCode", entity.PostalCode ?? (object)DBNull.Value));
            command.Parameters.Add(new SqlParameter("@Country", entity.Country ?? (object)DBNull.Value));
            command.Parameters.Add(new SqlParameter("@IsActive", entity.IsActive));
            command.Parameters.Add(new SqlParameter("@CreatedAtUtc", entity.CreatedAtUtc));
            command.Parameters.Add(new SqlParameter("@UpdatedAtUtc", entity.UpdatedAtUtc));

            var result = await command.ExecuteScalarAsync();
            return (int)result!;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task UpdateAsync(Customer entity)
    {
        try
        {
            const string query = @"
                UPDATE [customer].[Customer]
                SET
                    [Name] = @Name,
                    [ContactPerson] = @ContactPerson,
                    [Email] = @Email,
                    [TelephoneNumber] = @TelephoneNumber,
                    [MobileNumber] = @MobileNumber,
                    [AddressLine1] = @AddressLine1,
                    [AddressLine2] = @AddressLine2,
                    [City] = @City,
                    [PostalCode] = @PostalCode,
                    [Country] = @Country,
                    [IsActive] = @IsActive,
                    [UpdatedAtUtc] = @UpdatedAtUtc
                WHERE [Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@Name", entity.Name ?? (object)DBNull.Value),
                new SqlParameter("@ContactPerson", entity.ContactPerson ?? (object)DBNull.Value),
                new SqlParameter("@Email", entity.Email ?? (object)DBNull.Value),
                new SqlParameter("@TelephoneNumber", entity.TelephoneNumber ?? (object)DBNull.Value),
                new SqlParameter("@MobileNumber", entity.MobileNumber ?? (object)DBNull.Value),
                new SqlParameter("@AddressLine1", entity.AddressLine1 ?? (object)DBNull.Value),
                new SqlParameter("@AddressLine2", entity.AddressLine2 ?? (object)DBNull.Value),
                new SqlParameter("@City", entity.City ?? (object)DBNull.Value),
                new SqlParameter("@PostalCode", entity.PostalCode ?? (object)DBNull.Value),
                new SqlParameter("@Country", entity.Country ?? (object)DBNull.Value),
                new SqlParameter("@IsActive", entity.IsActive),
                new SqlParameter("@UpdatedAtUtc", entity.UpdatedAtUtc)
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
                UPDATE [customer].[Customer]
                SET
                    [IsActive] = 0,
                    [UpdatedAtUtc] = @UpdatedAtUtc
                WHERE [Id] = @Id AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", id),
                new SqlParameter("@BusinessId", businessId),
                new SqlParameter("@UpdatedAtUtc", DateTime.UtcNow)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Gets a paginated list of customers for a business, with optional search term and IsActive filter.
    /// Search matches Name, ContactPerson, or Email using case-insensitive LIKE pattern.
    /// Results are ordered by Name. If the requested page exceeds total pages, returns the last available page.
    /// </summary>
    public virtual async Task<PagedResult<Customer>> GetCustomersPagedAsync(string? searchTerm, bool? isActive, int page, int pageSize, int businessId)
    {
        try
        {
            const string countQuery = @"
                SELECT COUNT(*)
                FROM [customer].[Customer]
                WHERE [customer].[Customer].[BusinessId] = @BusinessId
                  AND (@SearchTerm IS NULL
                       OR [customer].[Customer].[Name] LIKE @SearchPattern
                       OR [customer].[Customer].[ContactPerson] LIKE @SearchPattern
                       OR [customer].[Customer].[Email] LIKE @SearchPattern)
                  AND (@IsActive IS NULL OR [customer].[Customer].[IsActive] = @IsActive)";

            const string dataQuery = @"
                SELECT [customer].[Customer].[Id],
                       [customer].[Customer].[BusinessId],
                       [customer].[Customer].[Name],
                       [customer].[Customer].[ContactPerson],
                       [customer].[Customer].[Email],
                       [customer].[Customer].[TelephoneNumber],
                       [customer].[Customer].[MobileNumber],
                       [customer].[Customer].[IsActive],
                       [customer].[Customer].[CreatedAtUtc]
                FROM [customer].[Customer]
                WHERE [customer].[Customer].[BusinessId] = @BusinessId
                  AND (@SearchTerm IS NULL
                       OR [customer].[Customer].[Name] LIKE @SearchPattern
                       OR [customer].[Customer].[ContactPerson] LIKE @SearchPattern
                       OR [customer].[Customer].[Email] LIKE @SearchPattern)
                  AND (@IsActive IS NULL OR [customer].[Customer].[IsActive] = @IsActive)
                ORDER BY [customer].[Customer].[Name]
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var connection = _context.Database.GetDbConnection();

            try
            {
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                // Build shared parameters
                var searchTermParam = string.IsNullOrWhiteSpace(searchTerm) ? (object)DBNull.Value : searchTerm;
                var searchPatternParam = string.IsNullOrWhiteSpace(searchTerm) ? (object)DBNull.Value : $"%{searchTerm}%";
                var isActiveParam = isActive.HasValue ? (object)isActive.Value : DBNull.Value;

                // Execute count query
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
                    countCommand.Parameters.Add(new SqlParameter("@IsActive", isActiveParam));

                    var countResult = await countCommand.ExecuteScalarAsync();
                    totalCount = countResult != null && countResult != DBNull.Value ? (int)countResult : 0;
                }

                // Compute total pages
                int totalPages = totalCount > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 0;

                // Handle page exceeding total pages: return last available page (or empty if no results)
                if (totalCount == 0)
                {
                    return new PagedResult<Customer>
                    {
                        Items = new List<Customer>(),
                        CurrentPage = 1,
                        PageSize = pageSize,
                        TotalCount = 0
                    };
                }

                if (page > totalPages)
                    page = totalPages;

                if (page < 1)
                    page = 1;

                int offset = (page - 1) * pageSize;

                // Execute data query
                var results = new List<Customer>();
                using (var dataCommand = connection.CreateCommand())
                {
                    dataCommand.CommandText = dataQuery;

                    var transaction = _context.Database.CurrentTransaction;
                    if (transaction != null)
                        dataCommand.Transaction = transaction.GetDbTransaction();

                    dataCommand.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                    dataCommand.Parameters.Add(new SqlParameter("@SearchTerm", searchTermParam));
                    dataCommand.Parameters.Add(new SqlParameter("@SearchPattern", searchPatternParam));
                    dataCommand.Parameters.Add(new SqlParameter("@IsActive", isActiveParam));
                    dataCommand.Parameters.Add(new SqlParameter("@Offset", offset));
                    dataCommand.Parameters.Add(new SqlParameter("@PageSize", pageSize));

                    using var reader = await dataCommand.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        results.Add(new Customer
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("Id")),
                            BusinessId = reader.GetInt32(reader.GetOrdinal("BusinessId")),
                            Name = reader.GetString(reader.GetOrdinal("Name")),
                            ContactPerson = reader.IsDBNull(reader.GetOrdinal("ContactPerson")) ? null : reader.GetString(reader.GetOrdinal("ContactPerson")),
                            Email = reader.IsDBNull(reader.GetOrdinal("Email")) ? null : reader.GetString(reader.GetOrdinal("Email")),
                            TelephoneNumber = reader.IsDBNull(reader.GetOrdinal("TelephoneNumber")) ? null : reader.GetString(reader.GetOrdinal("TelephoneNumber")),
                            MobileNumber = reader.IsDBNull(reader.GetOrdinal("MobileNumber")) ? null : reader.GetString(reader.GetOrdinal("MobileNumber")),
                            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                            CreatedAtUtc = reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc"))
                        });
                    }
                }

                return new PagedResult<Customer>
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
        catch (Exception)
        {
            throw;
        }
    }
}
