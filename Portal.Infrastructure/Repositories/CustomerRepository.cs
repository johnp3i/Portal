using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

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
                       [IsActive], [CreatedAtUtc], [UpdatedAtUtc]
                FROM [customer].[Customer]
                WHERE [BusinessId] = @BusinessId";

            return await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<Customer?> GetByIdAndBusinessIdAsync(int id, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [Name], [ContactPerson], [Email], [TelephoneNumber], [MobileNumber],
                       [AddressLine1], [AddressLine2], [City], [PostalCode], [Country],
                       [IsActive], [CreatedAtUtc], [UpdatedAtUtc]
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

    public async Task InsertAsync(Customer entity)
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
                     @IsActive, @CreatedAtUtc, @UpdatedAtUtc)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@BusinessId", entity.BusinessId),
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
                new SqlParameter("@CreatedAtUtc", entity.CreatedAtUtc),
                new SqlParameter("@UpdatedAtUtc", entity.UpdatedAtUtc)
            );
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
}
