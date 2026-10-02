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
                       [IsActive], [IsReminderOptedOut], [ContactId], [CreatedAtUtc], [UpdatedAtUtc]
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
                       [IsActive], [IsReminderOptedOut], [ContactId], [CreatedAtUtc], [UpdatedAtUtc]
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
                       [IsActive], [IsReminderOptedOut], [ContactId], [CreatedAtUtc], [UpdatedAtUtc]
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
                     [IsActive], [ContactId], [CreatedAtUtc], [UpdatedAtUtc])
                VALUES
                    (@BusinessId, @Name, @ContactPerson, @Email, @TelephoneNumber, @MobileNumber,
                     @AddressLine1, @AddressLine2, @City, @PostalCode, @Country,
                     @IsActive, @ContactId, @CreatedAtUtc, @UpdatedAtUtc);
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
            command.Parameters.Add(new SqlParameter("@ContactId", entity.ContactId ?? (object)DBNull.Value));
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
    public virtual async Task<PagedResult<CustomerListItemDto>> GetCustomersPagedAsync(
        string? searchTerm, bool? isActive, int page, int pageSize, int businessId,
        bool? createdFromLead = null, bool? hasNoDocuments = null)
    {
        try
        {
            // Per-customer invoice and quotation counts (non-deleted), used for the Documents column
            // and the "no documents yet" filter. Correlated subqueries so a customer with zero of
            // either still returns a row.
            const string invoiceCountExpr = @"
                (SELECT COUNT(*) FROM [invoice].[Invoice]
                 WHERE [invoice].[Invoice].[CustomerId] = [customer].[Customer].[Id]
                   AND [invoice].[Invoice].[IsDeleted] = 0)";
            const string quotationCountExpr = @"
                (SELECT COUNT(*) FROM [quotation].[Quotation]
                 WHERE [quotation].[Quotation].[CustomerId] = [customer].[Customer].[Id]
                   AND [quotation].[Quotation].[IsDeleted] = 0)";

            // Shared WHERE: search + active + createdFromLead (ContactId present/absent) + hasNoDocuments
            // (no non-deleted invoices AND no non-deleted quotations).
            var whereClause = $@"
                WHERE [customer].[Customer].[BusinessId] = @BusinessId
                  AND (@SearchTerm IS NULL
                       OR [customer].[Customer].[Name] LIKE @SearchPattern
                       OR [customer].[Customer].[ContactPerson] LIKE @SearchPattern
                       OR [customer].[Customer].[Email] LIKE @SearchPattern)
                  AND (@IsActive IS NULL OR [customer].[Customer].[IsActive] = @IsActive)
                  AND (@CreatedFromLead IS NULL
                       OR (@CreatedFromLead = 1 AND [customer].[Customer].[ContactId] IS NOT NULL)
                       OR (@CreatedFromLead = 0 AND [customer].[Customer].[ContactId] IS NULL))
                  AND (@HasNoDocuments IS NULL
                       OR (@HasNoDocuments = 1 AND {invoiceCountExpr} = 0 AND {quotationCountExpr} = 0)
                       OR (@HasNoDocuments = 0 AND ({invoiceCountExpr} > 0 OR {quotationCountExpr} > 0)))";

            var countQuery = $@"
                SELECT COUNT(*)
                FROM [customer].[Customer]
                {whereClause}";

            var dataQuery = $@"
                SELECT [customer].[Customer].[Id],
                       [customer].[Customer].[Name],
                       [customer].[Customer].[Email],
                       [customer].[Customer].[TelephoneNumber],
                       [customer].[Customer].[City],
                       [customer].[Customer].[IsActive],
                       [customer].[Customer].[ContactId],
                       {invoiceCountExpr} AS [InvoiceCount],
                       {quotationCountExpr} AS [QuotationCount]
                FROM [customer].[Customer]
                {whereClause}
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
                var createdFromLeadParam = createdFromLead.HasValue ? (object)createdFromLead.Value : DBNull.Value;
                var hasNoDocumentsParam = hasNoDocuments.HasValue ? (object)hasNoDocuments.Value : DBNull.Value;

                void AddSharedParams(System.Data.Common.DbCommand cmd)
                {
                    cmd.Parameters.Add(new SqlParameter("@BusinessId", businessId));
                    cmd.Parameters.Add(new SqlParameter("@SearchTerm", searchTermParam));
                    cmd.Parameters.Add(new SqlParameter("@SearchPattern", searchPatternParam));
                    cmd.Parameters.Add(new SqlParameter("@IsActive", isActiveParam));
                    cmd.Parameters.Add(new SqlParameter("@CreatedFromLead", createdFromLeadParam));
                    cmd.Parameters.Add(new SqlParameter("@HasNoDocuments", hasNoDocumentsParam));
                }

                // Execute count query
                int totalCount;
                using (var countCommand = connection.CreateCommand())
                {
                    countCommand.CommandText = countQuery;

                    var transaction = _context.Database.CurrentTransaction;
                    if (transaction != null)
                        countCommand.Transaction = transaction.GetDbTransaction();

                    AddSharedParams(countCommand);

                    var countResult = await countCommand.ExecuteScalarAsync();
                    totalCount = countResult != null && countResult != DBNull.Value ? (int)countResult : 0;
                }

                // Handle page exceeding total pages: return last available page (or empty if no results)
                if (totalCount == 0)
                {
                    return new PagedResult<CustomerListItemDto>
                    {
                        Items = new List<CustomerListItemDto>(),
                        CurrentPage = 1,
                        PageSize = pageSize,
                        TotalCount = 0
                    };
                }

                int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
                if (page > totalPages)
                    page = totalPages;
                if (page < 1)
                    page = 1;

                int offset = (page - 1) * pageSize;

                // Execute data query
                var results = new List<CustomerListItemDto>();
                using (var dataCommand = connection.CreateCommand())
                {
                    dataCommand.CommandText = dataQuery;

                    var transaction = _context.Database.CurrentTransaction;
                    if (transaction != null)
                        dataCommand.Transaction = transaction.GetDbTransaction();

                    AddSharedParams(dataCommand);
                    dataCommand.Parameters.Add(new SqlParameter("@Offset", offset));
                    dataCommand.Parameters.Add(new SqlParameter("@PageSize", pageSize));

                    using var reader = await dataCommand.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        results.Add(new CustomerListItemDto
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("Id")),
                            Name = reader.GetString(reader.GetOrdinal("Name")),
                            Email = reader.IsDBNull(reader.GetOrdinal("Email")) ? null : reader.GetString(reader.GetOrdinal("Email")),
                            TelephoneNumber = reader.IsDBNull(reader.GetOrdinal("TelephoneNumber")) ? null : reader.GetString(reader.GetOrdinal("TelephoneNumber")),
                            City = reader.IsDBNull(reader.GetOrdinal("City")) ? null : reader.GetString(reader.GetOrdinal("City")),
                            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                            ContactId = reader.IsDBNull(reader.GetOrdinal("ContactId")) ? null : reader.GetInt32(reader.GetOrdinal("ContactId")),
                            InvoiceCount = reader.GetInt32(reader.GetOrdinal("InvoiceCount")),
                            QuotationCount = reader.GetInt32(reader.GetOrdinal("QuotationCount"))
                        });
                    }
                }

                return new PagedResult<CustomerListItemDto>
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
