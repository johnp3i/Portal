using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for QuotationContact entity CRUD operations against the [quotation].[QuotationContact] table.
/// </summary>
public class QuotationContactRepository : GenericStoredProcedureRepository<QuotationContact>
{
    public QuotationContactRepository(DbContext context) : base(context) { }

    public async Task<List<QuotationContact>> GetByBusinessIdAsync(int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [UserId], [Name], [Email], [TelephoneNumber], [IsActive], [CreatedAtUtc]
                FROM [quotation].[QuotationContact]
                WHERE [BusinessId] = @BusinessId AND [IsActive] = 1";

            return await ExecuteStoredProcedure(query, new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<QuotationContact?> GetByIdAsync(int id)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [UserId], [Name], [Email], [TelephoneNumber], [IsActive], [CreatedAtUtc]
                FROM [quotation].[QuotationContact]
                WHERE [Id] = @Id";

            return await ExecuteSingleRecordStoredProcedure(query, new SqlParameter("@Id", id));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<QuotationContact?> GetByUserIdAsync(string userId, int businessId)
    {
        try
        {
            const string query = @"
                SELECT [Id], [BusinessId], [UserId], [Name], [Email], [TelephoneNumber], [IsActive], [CreatedAtUtc]
                FROM [quotation].[QuotationContact]
                WHERE [UserId] = @UserId AND [BusinessId] = @BusinessId AND [IsActive] = 1";

            return await ExecuteSingleRecordStoredProcedure(query,
                new SqlParameter("@UserId", userId),
                new SqlParameter("@BusinessId", businessId));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task InsertAsync(QuotationContact entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [quotation].[QuotationContact]
                    ([BusinessId], [UserId], [Name], [Email], [TelephoneNumber], [IsActive], [CreatedAtUtc])
                VALUES
                    (@BusinessId, @UserId, @Name, @Email, @TelephoneNumber, @IsActive, @CreatedAtUtc)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@BusinessId", entity.BusinessId),
                new SqlParameter("@UserId", entity.UserId ?? (object)DBNull.Value),
                new SqlParameter("@Name", entity.Name),
                new SqlParameter("@Email", entity.Email ?? (object)DBNull.Value),
                new SqlParameter("@TelephoneNumber", entity.TelephoneNumber ?? (object)DBNull.Value),
                new SqlParameter("@IsActive", entity.IsActive),
                new SqlParameter("@CreatedAtUtc", entity.CreatedAtUtc)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task UpdateAsync(QuotationContact entity)
    {
        try
        {
            const string query = @"
                UPDATE [quotation].[QuotationContact]
                SET
                    [Name] = @Name,
                    [Email] = @Email,
                    [TelephoneNumber] = @TelephoneNumber
                WHERE [Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@Name", entity.Name),
                new SqlParameter("@Email", entity.Email ?? (object)DBNull.Value),
                new SqlParameter("@TelephoneNumber", entity.TelephoneNumber ?? (object)DBNull.Value)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task DeactivateAsync(int id)
    {
        try
        {
            const string query = @"
                UPDATE [quotation].[QuotationContact]
                SET [IsActive] = 0
                WHERE [Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query, new SqlParameter("@Id", id));
        }
        catch (Exception)
        {
            throw;
        }
    }
}
