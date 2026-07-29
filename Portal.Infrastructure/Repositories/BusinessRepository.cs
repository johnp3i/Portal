using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Repositories;

/// <summary>
/// Repository for Business entity CRUD operations against the [portal].[Business] table.
/// </summary>
public class BusinessRepository : GenericStoredProcedureRepository<Business>
{
    public BusinessRepository(DbContext context) : base(context) { }

    public async Task<List<Business>> GetAllAsync()
    {
        try
        {
            const string query = "SELECT [Id], [Name], [IsActive], [IsDemoAccount], [IsPaymentInstructionsEnabled], [IsAutoReceiptEnabled], [IsAutoInvoiceSignatureEnabled], [IsOnboardingDismissed], [CreatedAtUtc], [UpdatedAtUtc] FROM [portal].[Business]";
            return await ExecuteStoredProcedure(query);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<Business?> GetByIdAsync(int id)
    {
        try
        {
            const string query = "SELECT [Id], [Name], [IsActive], [IsDemoAccount], [IsPaymentInstructionsEnabled], [IsAutoReceiptEnabled], [IsAutoInvoiceSignatureEnabled], [IsOnboardingDismissed], [CreatedAtUtc], [UpdatedAtUtc] FROM [portal].[Business] WHERE [Id] = @Id";
            return await ExecuteSingleRecordStoredProcedure(query, new SqlParameter("@Id", id));
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task InsertAsync(Business entity)
    {
        try
        {
            const string query = @"
                INSERT INTO [portal].[Business]
                    ([Name], [IsActive], [CreatedAtUtc], [UpdatedAtUtc])
                VALUES
                    (@Name, @IsActive, @CreatedAtUtc, @UpdatedAtUtc)";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Name", entity.Name ?? (object)DBNull.Value),
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

    public async Task UpdateAsync(Business entity)
    {
        try
        {
            const string query = @"
                UPDATE [portal].[Business]
                SET
                    [Name] = @Name,
                    [IsActive] = @IsActive,
                    [UpdatedAtUtc] = @UpdatedAtUtc
                WHERE [Id] = @Id";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@Id", entity.Id),
                new SqlParameter("@Name", entity.Name ?? (object)DBNull.Value),
                new SqlParameter("@IsActive", entity.IsActive),
                new SqlParameter("@UpdatedAtUtc", entity.UpdatedAtUtc)
            );
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<bool> IsNameUniqueAsync(string name, int? excludeId)
    {
        try
        {
            const string query = @"
                SELECT COUNT(*)
                FROM [portal].[Business]
                WHERE LOWER([Name]) = LOWER(@Name)
                    AND (@ExcludeId IS NULL OR [Id] <> @ExcludeId)";

            var nameParam = new SqlParameter("@Name", name ?? (object)DBNull.Value);
            var excludeIdParam = new SqlParameter("@ExcludeId", excludeId ?? (object)DBNull.Value);

            var result = await _context.Database
                .SqlQueryRaw<int>(query, nameParam, excludeIdParam)
                .ToListAsync();

            return result.FirstOrDefault() == 0;
        }
        catch (Exception)
        {
            throw;
        }
    }
}
